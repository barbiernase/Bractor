using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Mapping;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Basis-Klasse für Aggregate-Actors.
/// 
/// WICHTIG: Der Handler wird NACH dem Laden des States erstellt,
/// damit Handler, Decider und Applier alle auf das gleiche State-Objekt zeigen.
/// 
/// WICHTIG: Muss IMMER context.Respond() aufrufen, sonst macht Proto.Actor Retries!
/// 
/// ★ OneOf-Migration: Decider wirft keine Exceptions mehr für Domänen-Ablehnungen.
///   Stattdessen yieldet er ITransientEvent (z.B. BestandNichtAusreichend).
///   Der Actor trennt persistent/transient und sendet Ablehnungen per Targeted Delivery.
///   CommandFailed bleibt für technische Fehler (catch-Block).
///
/// ★ Phase 4: IVersionTracker (optional) für Redis-basierte Stale Detection.
/// ★ Phase 4: CommandResult.NewVersion wird nach Apply gesetzt.
/// </summary>
public abstract class AggregateActorBase<TState> : IActor
    where TState : class, IState, new()
{
    private readonly IAggregateHandlerFactory _handlerFactory;
    private readonly IEventStoreRepository _eventStore;
    private readonly IVersionTracker? _versionTracker;
    private readonly BrokerPublisher? _publisher;
    private readonly ILogger? _logger;

    // ★ Snapshot-Naht (docs/snapshot-konzept.md): abgeleiteter, nicht-autoritativer Cache. Null → aus,
    //   Voll-Replay wie bisher. Der Schema-String invalidiert stale Snapshots; der Schwellwert steuert das
    //   best-effort-Schreiben. Alle drei kommen optional aus DI (unregistriert → Snapshots schlicht inaktiv).
    private readonly ISnapshotStore? _snapshots;
    private readonly string? _snapshotSchemaVersion;
    private readonly int _snapshotThreshold;
    private int _lastSnapshotVersion;   // Stream-Position des zuletzt (gefalteten/geschriebenen) Snapshots

    // ★ Audit-Fix #5: ein deterministisch werfender Applier vergiftet das Aggregat (keine Rehydration möglich)
    //   → beobachtbar machen (Dead-Letter), aber nur EINMAL je Aktivierung statt per-Command-Spam.
    private readonly IDeadLetterSink? _deadLetters;
    private bool _vergiftetGemeldet;

    private IAggregateHandler? _handler;  // Wird nach State-Load erstellt!
    private TState? _state;
    private Guid _id;
    private bool _initialized = false;

    // Framework-Inbox (Exactly-once): bereits verarbeitete CommandIds des idempotenten Pfads (Emittiert).
    // Aus den co-committeten KommandoVerarbeitet-Marken des Streams gefaltet — der Fachcode kennt das nie.
    // ★ Audit-Fix #4/H3: HART gedeckelt (letzte K) statt monoton wachsend — siehe BoundedInbox.
    // ★ Treiber-Fold (EM-1): eine ZWEITE Menge der fachlich ABGELEHNTEN Vorgänge (co-committete
    //   KommandoAbgelehnt-Marken). Sie hält die Re-Delivery eines abgelehnten Commands konsistent auf
    //   Ablehnung (NIE Success:true) — nötig, weil der Ablehnungs-Pfad jetzt eine durable Marke schreibt,
    //   die der Prozess-Manager als Fehlschlag faltet (§4-Kopplung: Marke + Zwei-Mengen-Inbox + Fold-Achse).
    private readonly int _inboxCap;
    private BoundedInbox _verarbeiteteCommandIds;
    private BoundedInbox _abgelehnteCommandIds;

    protected AggregateActorBase(
        IAggregateHandlerFactory handlerFactory,
        IEventStoreRepository eventStore,
        IVersionTracker? versionTracker = null,
        BrokerPublisher? publisher = null,
        ILogger? logger = null,
        ISnapshotStore? snapshots = null,
        string? snapshotSchemaVersion = null,
        SnapshotOptions? snapshotOptions = null,
        IDeadLetterSink? deadLetters = null)
    {
        _handlerFactory = handlerFactory;
        _eventStore = eventStore;
        _versionTracker = versionTracker;
        _publisher = publisher;
        _logger = logger;
        _snapshots = snapshots;
        _snapshotSchemaVersion = snapshotSchemaVersion;
        _snapshotThreshold = snapshotOptions?.Threshold ?? 200;
        _inboxCap = snapshotOptions?.InboxCap ?? 10_000;
        _verarbeiteteCommandIds = new BoundedInbox(_inboxCap);
        _abgelehnteCommandIds = new BoundedInbox(_inboxCap);
        _deadLetters = deadLetters;
    }

    public async Task ReceiveAsync(IContext context)
    {
        try
        {
            switch (context.Message)
            {
                case Started:
                    _logger?.LogDebug("[Actor] Started");
                    break;

                case CommandEnvelope envelope:
                    if (!_initialized)
                    {
                        await InitializeAsync(envelope.AggregateId);
                    }
                    await HandleCommandAsync(context, envelope);
                    break;

                case Stopping:
                    _logger?.LogDebug("[Actor] Stopping for ID: {Id}", _id);
                    // ★ Snapshot bei Deaktivierung (best-effort): wenn seit dem letzten Snapshot Events
                    //   kamen, den aktuellen State festhalten. Fire-and-forget — Stopping darf nicht blocken.
                    TryWriteSnapshot();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Actor] Error in ReceiveAsync");

            // WICHTIG: Auch bei Fehler antworten, sonst Retry!
            if (context.Message is CommandEnvelope envelope)
            {
                // CommandFailed für technische Fehler
                await TryPublishCommandFailedAsync(envelope, ex.Message);
                
                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message,
                    AggregateId = _id
                });
            }
        }
    }

    private async Task InitializeAsync(Guid aggregateId)
    {
        _id = aggregateId;

        // ★ EIN Tail-Fold statt zwei Voll-Reads (Snapshot-Konzept §6): State UND Inbox aus einem
        //   Durchlauf; mit Snapshot seedet der Fold und liest nur den Rest ab snapshot.Version+1.
        RehydrationResult<TState> rehydrated;
        try
        {
            rehydrated = await Infrastructure.Aggregate.AggregateRehydrator.LoadAsync<TState>(
                _id, _snapshots, _eventStore, _handlerFactory, _snapshotSchemaVersion, default, _inboxCap);
        }
        catch (Exception ex)
        {
            // ★ Audit-Fix #5: Rehydration scheiterte — typischerweise ein deterministisch werfender Applier auf
            //   einem bereits DURABLEN Event. Damit ist das Aggregat bis zu einem Domänen-Fix NICHT aktivierbar:
            //   JEDES Command scheitert hier. Das Framework kann einen kaputten Applier nicht heilen, aber es
            //   macht den Brick BEOBACHTBAR — ein distinkter, durabler Dead-Letter an EINER Stelle statt in
            //   per-Command-CommandFailed-Spam. Nur EINMAL je Aktivierung; danach weiterwerfen, damit der
            //   bestehende Fluss (äußerer catch → Success=false; H1-Post-Append-catch → Rehydrate-Fehlerzweig)
            //   unverändert bleibt.
            await TryMeldeVergiftetAsync(ex);
            throw;
        }

        _state = rehydrated.State;
        _verarbeiteteCommandIds = rehydrated.ProcessedCommandIds;
        _abgelehnteCommandIds = rehydrated.RejectedCommandIds;
        _lastSnapshotVersion = rehydrated.SnapshotVersion;

        // Handler zeigt auf denselben geseedeten State (Decider/Applier mutieren _state).
        _handler = _handlerFactory.CreateHandler(_state);

        _initialized = true;
        _logger?.LogDebug("[Actor] Initialized with ID {Id}, Version {Version} (Snapshot ab v{Snapshot})",
            _id, _state.Version, _lastSnapshotVersion);
    }

    /// <summary>
    /// ★ P2: Zwei getrennte Schreiber-Eingänge (EM-2) statt eines überladenen Sentinels. Der Actor
    /// dispatcht exhaustiv auf den <see cref="CommandModus"/>; die Behandlung ist am Typ ablesbar,
    /// nicht an einem magischen -1.
    /// </summary>
    private async Task HandleCommandAsync(IContext context, CommandEnvelope cmdEnvelope)
    {
        switch (cmdEnvelope.Modus)
        {
            case CommandModus.Client c:
                await HandleClientCommand(context, cmdEnvelope, c.ExpectedVersion);
                break;
            case CommandModus.Emittiert:
                await HandleEmittedCommand(context, cmdEnvelope);
                break;
        }
    }

    /// <summary>Externer Client-Command: OCC gegen die behauptete Version.</summary>
    private Task HandleClientCommand(IContext context, CommandEnvelope cmdEnvelope, int expectedVersion)
        => HandleCommandCoreAsync(context, cmdEnvelope, istIdempotent: false, expectedVersion);

    /// <summary>Interner Emitter (Reaktion/Prozess/Pipeline): KEINE Version — Idempotenz via det. CommandId + Inbox.</summary>
    private Task HandleEmittedCommand(IContext context, CommandEnvelope cmdEnvelope)
        => HandleCommandCoreAsync(context, cmdEnvelope, istIdempotent: true, expectedVersion: _state!.Version);

    private async Task HandleCommandCoreAsync(
        IContext context, CommandEnvelope cmdEnvelope, bool istIdempotent, int expectedVersion)
    {
        try
        {
            _logger?.LogDebug("[Actor] Processing {Command}", cmdEnvelope.Payload.GetType().Name);

            // Validierung: Falsche AggregateId
            if (cmdEnvelope.AggregateId != _id)
            {
                _logger?.LogWarning("[Actor] Command for wrong aggregate (id {Id})", _id);
                await TryPublishCommandFailedAsync(cmdEnvelope, "Command sent to wrong aggregate");
                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = "Command sent to wrong aggregate",
                    AggregateId = _id
                });
                return;
            }

            // ★ P2: expectedVersion + istIdempotent kommen jetzt vom Eingang (Client vs. Emittiert),
            //   nicht aus einem Sentinel in der Message (EM-2). Client → behauptete Version (OCC);
            //   Emittiert → aufgelöste aktuelle Version (Single-Writer) → nie ein OCC-Konflikt, aber
            //   die Marten-OCC weist eine echte Doppelaktivierung weiterhin ab.

            // Validierung: Concurrency Check (Actor-seitig, schnell) — nur bei echter Assertion (Client).
            if (_state!.Version != expectedVersion)
            {
                var errorMsg = $"Concurrency conflict: expected version {expectedVersion}, actual {_state.Version}";
                _logger?.LogDebug("[Actor] {ErrorMsg}", errorMsg);
                await TryPublishCommandFailedAsync(cmdEnvelope, errorMsg);
                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = errorMsg,
                    AggregateId = _id,
                    NewVersion = _state.Version  // Pipeline braucht die aktuelle Version für Retry
                });
                return;
            }

            // ★ Framework-Inbox: der idempotente Pfad (Emittiert; Reaktion/Prozess) dedupliziert nach
            //   CommandId — ein wiederholt zugestellter Command (at-least-once) verpufft als Noop, OHNE dass
            //   der Fachcode eine Dedup-Zeile trägt. Der Client-/OCC-Pfad ist unberührt.
            if (istIdempotent && _verarbeiteteCommandIds.Contains(cmdEnvelope.CommandId))
            {
                context.Respond(new CommandResult { Success = true, AggregateId = _id, NewVersion = _state.Version });
                return;
            }

            // ★ Treiber-Fold (EM-1, §4-Kopplung): eine Re-Delivery eines bereits fachlich ABGELEHNTEN Vorgangs
            //   muss KONSISTENT eine Ablehnung liefern — NIE Success:true. Sonst könnte ein zweiter Zustellversuch
            //   (Poll/Draht-Duplikat) nach zwischenzeitlich geändertem State plötzlich erfolgreich sein und einen
            //   Effekt erzeugen, während der Prozess-Manager den Schritt schon als gescheitert kompensiert. Der
            //   getippte Ablehnungs-Grund ist nicht mehr rekonstruierbar (nur die CommandId reist in der Inbox) —
            //   der Manager braucht ihn hier auch nicht: seine Fehlschlag-Wahrheit ist die durable KommandoAbgelehnt-
            //   Marke, die er faltet. Ein generischer Ablehnungs-Ausgang genügt (Success:false).
            if (istIdempotent && _abgelehnteCommandIds.Contains(cmdEnvelope.CommandId))
            {
                context.Respond(new CommandResult
                {
                    Success = false,
                    AggregateId = _id,
                    ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)",
                    NewVersion = _state.Version
                });
                return;
            }

            // 1. Events erzeugen (Decider) — wirft keine Exceptions mehr für Domänen-Ablehnungen
            var allEvents = _handler!.HandleCommand(cmdEnvelope.Payload).ToList();

            // 2. Noop — keine Events = idempotent (z.B. DeaktiviereLagerartikel bei bereits inaktivem Artikel)
            if (!allEvents.Any())
            {
                // ★ Audit-Fix K2 (Prozess-Livelock): Auf dem idempotenten Pfad (Reaktion/Prozess) hinterlässt
                //   AUCH ein Noop eine durable KommandoVerarbeitet-Marke. Der Prozess-Manager erkennt „Transition
                //   erledigt" NUR über ein Ziel-Event mit CausationId == vorgang; ohne Marke bliebe ein Noop-Ziel
                //   ewig „pending" → der Manager (und der §3-Backstop) feuerten endlos re-firing → Livelock, nie
                //   ProzessBeendet. Die Marke macht „getan" beobachtbar. Der OCC-Client-Pfad bleibt unberührt.
                if (istIdempotent)
                    await CoCommitInboxMarkeAsync(cmdEnvelope, expectedVersion);

                context.Respond(new CommandResult
                {
                    Success = true,
                    AggregateId = _id,
                    Events = allEvents,
                    NewVersion = _state.Version
                });
                return;
            }

            // 3. ★ Trennung: persistente Events vs. Ablehnungen (ITransientEvent)
            var persistentEvents = allEvents.Where(e => e is not ITransientEvent).ToList();
            var rejections = allEvents.Where(e => e is ITransientEvent).ToList();

            // ★ Audit-Fix #3 (Fail-fast statt stillem Drop): Eine Ablehnung (ITransientEvent) ist an der
            //   Command-Grenze ein EINZELNES, EXKLUSIVES Ergebnis — CommandResult trägt genau einen Ausgang
            //   (Success + ein RejectionEvent). Ein Decider, der eine Ablehnung MIT persistenten Events oder
            //   MEHRERE Ablehnungen zurückgibt, verletzt die tragende Entweder-Oder-Invariante des Decider-
            //   Modells (jeder Decider lebt sie über `yield break`). Früher lief das still schief: bei
            //   persistent+transient landete es im Erfolgspfad (Events committet, Ablehnung spurlos), bei
            //   mehreren Ablehnungen stellte `rejections.First()` nur die erste zu. Jetzt: laut werfen
            //   (→ äußerer catch: CommandFailed + Success=false), damit der Programmierfehler nicht
            //   schweigend durchläuft.
            if (rejections.Any() && (persistentEvents.Any() || rejections.Count > 1))
            {
                throw new InvalidOperationException(
                    $"Decider für {cmdEnvelope.Payload.GetType().Name} lieferte ein ungültiges gemischtes " +
                    $"Ergebnis: {persistentEvents.Count} persistente(s) Event(s) + {rejections.Count} " +
                    $"Ablehnung(en). Eine Ablehnung (ITransientEvent) muss das einzige zurückgegebene Event sein.");
            }

            // 4. ★ Reine Ablehnung — genau EIN transientes Event, keine persistenten (der Guard oben
            //    garantiert: hier steht exakt eine Ablehnung).
            //    Kein Append, kein Apply, keine Version-Änderung.
            //    Targeted Delivery an den aufrufenden Client.
            if (rejections.Any() && !persistentEvents.Any())
            {
                var rejection = rejections.First();
                _logger?.LogDebug("[Actor] Rejected: {Rejection}", rejection.GetType().Name);

                // ★ Treiber-Fold (EM-1) — Wende der früheren K2-Entscheidung: auf dem EMITTIERTEN Pfad
                //   (Reaktion/Prozess) wird die Ablehnung jetzt DURABEL als KommandoAbgelehnt co-committet. Der
                //   Prozess-Manager faltet sie als eigene Achse (AbgelehntDa) zu SchrittGescheitert — er braucht
                //   die Ziel-Quittung nicht mehr (der EINE Emit-Weg). Der frühere Falsch-Erfolg-Fallstrick (eine
                //   RE-Zustellung liefert per Inbox-Dedup Success:true → Manager verzweigt auf Erfolg) ist durch
                //   die ZWEITE Inbox-Menge geschlossen: ein abgelehnter Vorgang bleibt bei Re-Delivery auf
                //   Success:false (siehe Dedup-Zweig oben). Marke + Zwei-Mengen-Inbox + Fold-Achse zusammen (§4).
                //   Der Client-/OCC-Pfad bleibt marken-frei (Targeted Delivery, kein Prozess dahinter).
                if (istIdempotent)
                    await CoCommitAblehnungsMarkeAsync(cmdEnvelope, expectedVersion, rejection.GetType().Name);

                // Targeted Delivery — nur an den Aufrufer
                await TryPublishRejectionAsync(cmdEnvelope, rejection);

                context.Respond(new CommandResult
                {
                    Success = false,
                    AggregateId = _id,
                    RejectionEvent = rejection,
                    ErrorMessage = rejection.ToString()
                });
                return;
            }

            // 5. Erfolg — persistente Events verarbeiten.
            //    ★ Phase 1: Metadaten mit ins Log stempeln (nach Log-Read verfügbar).
            //    ★ Inbox: auf dem idempotenten Pfad die KommandoVerarbeitet-Marke CO-COMMITTEN (EINE
            //      Transaktion mit dem Effekt) → exactly-once-wirksam, crash-fest (Effekt + Marke atomar).
            var zuSchreiben = istIdempotent
                ? persistentEvents.Append((IEvent)new Infrastructure.Aggregate.KommandoVerarbeitet(cmdEnvelope.CommandId)).ToList()
                : persistentEvents;

            await _eventStore.AppendEventsAsync(
                _id,
                expectedVersion,
                zuSchreiben,
                correlationId: cmdEnvelope.CorrelationId,
                causationId: cmdEnvelope.CommandId.ToString(),
                aggregateType: typeof(TState).Name);

            // ★ Audit-Fix H1: Ab HIER ist der Command DURABEL (der Append hat committet). Alles Folgende
            //   (Applier auf den In-Memory-State, Redis, Snapshot, Publish) ist nur die abgeleitete
            //   In-Memory-Repräsentation + best-effort-Nebenwirkung. Wirft davon etwas, darf der Command NICHT
            //   als Fehlschlag quittiert werden (falsches Negativ → der Prozess kompensiert einen erfolgreichen
            //   Schritt, ein Client-Retry doppelt den Effekt auf dem nicht-dedupenden OCC-Pfad) UND der Actor
            //   darf nicht mit halb-mutiertem _state weiterlaufen (sonst dauerhafte spurious Concurrency-Conflicts
            //   bis zum Recycling). Darum: der äußere catch fängt nur PRE-Append-Fehler; ein POST-Append-Fehler
            //   rehydriert den State frisch aus dem (nun aktuellen) Log und quittiert WAHR (Erfolg).
            try
            {
                // 6. Events applyen (Applier) — mutiert _state. Die Marke wird NICHT angewandt (der Applier
                //    kennt sie nicht), aber in der Version mitgezählt (Stream-Position konsistent).
                // ★ Audit-Fix #5 (Symmetrie Live == Rehydration): ein IProzessIntern-Persistent-Event wird
                //   NICHT angewandt (nur die Version mitgezählt) — exakt wie die Rehydration (AggregateRehydrator).
                foreach (var evt in persistentEvents)
                {
                    if (evt is not IProzessIntern) _handler.ApplyEvent(evt);
                    _state.Version++;
                }
                if (istIdempotent)
                {
                    _state.Version++;                                   // die co-committete Marke
                    _verarbeiteteCommandIds.Add(cmdEnvelope.CommandId);
                }

                // 7. ★ Version-Tracking in Redis (nicht-kritisch)
                await TryTrackVersionAsync();

                // 7b. ★ Snapshot bei Schwellwert (best-effort, out-of-band): verkürzt künftige Rehydration.
                TrySnapshotOnThreshold();

                // 8. Events broadcast-publishen (wenn Publisher vorhanden)
                if (_publisher != null)
                {
                    await PublishEventsAsync(persistentEvents, cmdEnvelope, expectedVersion);
                }

                _logger?.LogDebug("[Actor] ✔ Processed. New version: {Version}", _state.Version);

                // 9. WICHTIG: Erfolg antworten mit NewVersion!
                context.Respond(new CommandResult
                {
                    Success = true,
                    AggregateId = _id,
                    Events = persistentEvents,
                    NewVersion = _state.Version
                });
            }
            catch (Exception postAppendEx)
            {
                // Der Effekt ist bereits durabel → WAHR = Erfolg. Den (evtl. halb-mutierten) In-Memory-State
                // verwerfen und frisch aus dem Log rehydrieren, damit der Actor nicht vergiftet weiterläuft.
                _logger?.LogError(postAppendEx,
                    "[Actor] Fehler NACH dem durablen Append ({Command}) — der Command ist erfolgreich; rehydriere den In-Memory-State.",
                    cmdEnvelope.Payload.GetType().Name);

                _initialized = false;
                try { await InitializeAsync(_id); }
                catch (Exception rehydrateEx)
                {
                    // Rehydration schlug fehl → _initialized bleibt false → die nächste Nachricht initialisiert sauber neu.
                    _logger?.LogError(rehydrateEx,
                        "[Actor] Re-Rehydration nach Post-Append-Fehler fehlgeschlagen — die nächste Nachricht initialisiert neu.");
                }

                context.Respond(new CommandResult
                {
                    Success = true,
                    AggregateId = _id,
                    NewVersion = _state?.Version ?? (expectedVersion + zuSchreiben.Count)
                });
            }
        }
        catch (Exception ex)
        {
            // Catch-Block ist jetzt nur noch für technische Fehler
            // (DB-Timeout, Null-Referenz, etc. — nicht für Domänen-Ablehnungen)
            _logger?.LogError(ex, "[Actor] Technical error: {Message}", ex.Message);

            await TryPublishCommandFailedAsync(cmdEnvelope, ex.Message);
            
            context.Respond(new CommandResult 
            { 
                Success = false, 
                ErrorMessage = ex.Message,
                AggregateId = _id
            });
        }
    }

    /// <summary>
    /// Co-committet AUSSCHLIESSLICH die Framework-Inbox-Marke (idempotenter Pfad, Noop/Ablehnung) — ein
    /// durabler Beleg „dieser Vorgang wurde verarbeitet", ohne Domänen-Effekt. Gleiche native Transaktion
    /// wie ein normaler Append; die Marke ist <see cref="IProzessIntern"/> → beim Domänen-Falten übersprungen,
    /// in der Version aber mitgezählt (Stream-Position bündig). Basis ist die aufgelöste aktuelle Version
    /// (Single-Writer), sodass die Marten-OCC eine echte Doppelaktivierung weiterhin abweist.
    /// </summary>
    private async Task CoCommitInboxMarkeAsync(CommandEnvelope cmdEnvelope, int expectedVersion)
    {
        await _eventStore.AppendEventsAsync(
            _id,
            expectedVersion,
            new IEvent[] { new Infrastructure.Aggregate.KommandoVerarbeitet(cmdEnvelope.CommandId) },
            correlationId: cmdEnvelope.CorrelationId,
            causationId: cmdEnvelope.CommandId.ToString(),
            aggregateType: typeof(TState).Name);

        _state!.Version++;
        _verarbeiteteCommandIds.Add(cmdEnvelope.CommandId);
    }

    /// <summary>
    /// Co-committet AUSSCHLIESSLICH die Framework-ABLEHNUNGS-Marke (idempotenter Pfad, fachliche Ablehnung) —
    /// ein durabler Beleg „dieser Vorgang wurde abgelehnt" MIT dem Grund, ohne Domänen-Effekt. Gleiche native
    /// Transaktion wie ein normaler Append; die Marke ist <see cref="IProzessIntern"/> → beim Domänen-Falten
    /// übersprungen, in der Version aber mitgezählt (Stream-Position bündig). Der Prozess-Manager faltet sie zu
    /// <c>SchrittGescheitert</c> (Treiber-Fold/EM-1). Basis ist die aufgelöste aktuelle Version (Single-Writer).
    /// </summary>
    private async Task CoCommitAblehnungsMarkeAsync(CommandEnvelope cmdEnvelope, int expectedVersion, string grund)
    {
        await _eventStore.AppendEventsAsync(
            _id,
            expectedVersion,
            new IEvent[] { new Infrastructure.Aggregate.KommandoAbgelehnt(cmdEnvelope.CommandId, grund) },
            correlationId: cmdEnvelope.CorrelationId,
            causationId: cmdEnvelope.CommandId.ToString(),
            aggregateType: typeof(TState).Name);

        _state!.Version++;
        _abgelehnteCommandIds.Add(cmdEnvelope.CommandId);
    }

    /// <summary>
    /// ★ Audit-Fix #5: meldet ein „vergiftetes" Aggregat (Rehydration schlägt deterministisch fehl, i.d.R. ein
    /// werfender Applier) EINMAL je Aktivierung durabel in die DLQ — beobachtbar statt in per-Command-Spam
    /// begraben. Best-effort; wirft nie in den Aufrufer zurück (der re-wirft ohnehin den Original-Fehler).
    /// </summary>
    private async Task TryMeldeVergiftetAsync(Exception ursache)
    {
        if (_deadLetters is null || _vergiftetGemeldet) return;
        _vergiftetGemeldet = true;
        try
        {
            await _deadLetters.WriteAsync(new DeadLetter
            {
                Id = Guid.NewGuid(),
                Quelle = "aggregate-rehydrate",
                CommandType = "(Rehydration)",
                AggregateId = _id,
                AggregateType = typeof(TState).Name,
                Grund = $"Aggregat nicht rehydrierbar (Applier/Fold wirft deterministisch): {ursache.GetType().Name}: {ursache.Message}",
                Versuche = 0,
                ErfasstUtc = DateTimeOffset.UtcNow,
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Actor] Vergiftet-Dead-Letter-Write fehlgeschlagen für {Type}/{Id}.",
                typeof(TState).Name, _id);
        }
    }

    // ─── Snapshot (docs/snapshot-konzept.md §7): capture-in-turn + fire-and-forget ───

    /// <summary>
    /// Schreibt einen Snapshot, wenn seit dem letzten mindestens <c>_snapshotThreshold</c> Events kamen.
    /// Best-effort, außerhalb der Command-Transaktion (Invariante 6). Ein Fehler ist folgenlos.
    /// </summary>
    private void TrySnapshotOnThreshold()
    {
        if (_snapshots is null || _snapshotThreshold <= 0 || _state is null) return;
        if (_state.Version - _lastSnapshotVersion < _snapshotThreshold) return;
        WriteSnapshotDetached();
    }

    /// <summary>
    /// Schreibt einen Snapshot bei Deaktivierung, wenn seit dem letzten neue Events kamen. Best-effort.
    /// </summary>
    private void TryWriteSnapshot()
    {
        if (_snapshots is null || _state is null) return;
        if (_state.Version <= _lastSnapshotVersion) return;
        WriteSnapshotDetached();
    }

    /// <summary>
    /// Friert den State (Deep-Copy) und die Inbox IN-TURN ein — der spätere detached DB-Write kann das
    /// weiter mutierende <c>_state</c> nicht mehr korrumpieren (Single-Writer-Turn, Capture-Race-Probe).
    /// Dann fire-and-forget: der Command-Turn wartet nicht auf den DB-Write.
    /// </summary>
    private void WriteSnapshotDetached()
    {
        var snapshot = new Snapshot<TState>
        {
            Id = _id,
            Version = _state!.Version,
            SchemaVersion = _snapshotSchemaVersion ?? "",
            State = Clone(_state),
            ProcessedCommandIds = _verarbeiteteCommandIds.ToArray(),
            RejectedCommandIds = _abgelehnteCommandIds.ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _lastSnapshotVersion = _state.Version;

        try
        {
            _ = _snapshots!.SaveAsync(snapshot, default)
                .ContinueWith(t =>
                    _logger?.LogWarning(t.Exception,
                        "Snapshot für {Aggregate}/{Id} v{Version} fehlgeschlagen — folgenlos, " +
                        "die nächste Rehydration heilt.", typeof(TState).Name, _id, snapshot.Version),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Snapshot-Write konnte nicht gestartet werden — folgenlos.");
        }
    }

    /// <summary>Deep-Copy des States per JSON-Round-Trip (Bibliotheks-Serializer, kein Eigen-Reflection).</summary>
    private static TState Clone(TState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        return System.Text.Json.JsonSerializer.Deserialize<TState>(json)!;
    }

    /// <summary>
    /// ★ Phase 4: Aktualisiert die Version in Redis.
    /// Nicht-kritisch: Redis-Fehler unterbrechen den Command-Flow NICHT.
    /// </summary>
    private async Task TryTrackVersionAsync()
    {
        if (_versionTracker == null)
            return;

        try
        {
            await _versionTracker.TrackAsync(
                _id,
                typeof(TState).Name,
                _state!.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Version tracking failed for {AggregateType}/{AggregateId}. " +
                "Redis will catch up on next successful command.",
                typeof(TState).Name, _id);
        }
    }

    private Task PublishEventsAsync(List<IEvent> events, CommandEnvelope cmdEnvelope, int baseVersion)
    {
        // ★ Phase 1: Version PRO Event (Spec 4.2) — der Stream stand vor dem Batch auf
        //   baseVersion (aufgelöst: bei Sentinel = aktuelle Actor-Version); jedes Event trägt
        //   seine eigene Position, nie den Batch-Endwert.
        var envelopes = EventEnvelopeFactory.BuildPerEvent(
            events,
            _id,
            typeof(TState).Name,
            baseVersion: baseVersion,
            correlationId: cmdEnvelope.CorrelationId,
            causationId: cmdEnvelope.CommandId.ToString(),
            userId: cmdEnvelope.UserId);

        // ★ Perf: Publish ist reine Benachrichtigung — der Command ist bereits im Log committet
        //   (Schritt 5). Zustellung ist best-effort (Invariante 2); die durablen Konsumenten
        //   heilen über Log-Read + Poll-Backstop. Darum NICHT im Single-Writer-Turn auf die
        //   Shard-Acks warten, sondern fire-and-forget feuern — der Turn kehrt sofort zurück
        //   und der Per-Stream-Command-Durchsatz hängt nicht mehr an 6 Broker-Round-Trips/Event.
        foreach (var eventEnvelope in envelopes)
        {
            _publisher!.PublishFireAndForget(eventEnvelope);

            // ★ Phase 1 (1c): zusätzlich das typisierte Signal veröffentlichen (Spec 4.3).
            //   Best-effort — ein Signal ist nur ein Weckruf und DARF verloren gehen
            //   (Invariante 2); der Poll-Backstop holt es nach.
            var signal = GeneratedSignalFactory.Create(
                eventEnvelope.Payload, eventEnvelope.AggregateId, eventEnvelope.AggregateVersion);
            if (signal != null)
            {
                _publisher.PublishFireAndForget(new SignalEnvelope
                {
                    Signal = signal,
                    CorrelationId = eventEnvelope.CorrelationId,
                    UserId = eventEnvelope.UserId
                });
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ★ NEU: Publiziert ein Ablehnungs-Event per Targeted Delivery an den Aufrufer.
    /// Verwendet OriginSessionId für die direkte Zustellung.
    /// Das Ablehnungs-Event ist das typisierte Domänen-Event (z.B. BestandNichtAusreichend).
    /// </summary>
    private async Task TryPublishRejectionAsync(CommandEnvelope cmdEnvelope, IEvent rejection)
    {
        if (_publisher == null || string.IsNullOrEmpty(cmdEnvelope.OriginSessionId))
        {
            _logger?.LogDebug("[Actor] Cannot publish rejection: no publisher or no OriginSessionId");
            return;
        }

        try
        {
            var envelope = new EventEnvelope
            {
                AggregateId = _id,
                AggregateVersion = _state?.Version ?? 0,
                AggregateType = typeof(TState).Name,
                CorrelationId = cmdEnvelope.CorrelationId,
                CausationId = cmdEnvelope.CommandId.ToString(),
                UserId = cmdEnvelope.UserId,
                Payload = rejection,
                TargetSubscriberId = cmdEnvelope.OriginSessionId  // Targeted Delivery!
            };

            await _publisher.PublishAsync(envelope);
            _logger?.LogDebug("[Actor] Published rejection {Rejection} to '{Session}'", rejection.GetType().Name, cmdEnvelope.OriginSessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Actor] Failed to publish rejection");
        }
    }

    /// <summary>
    /// Publiziert ein CommandFailed Event für technische Fehler.
    /// Verwendet Targeted Delivery über die OriginSessionId.
    /// ★ Jetzt nur noch für technische Fehler — Domänen-Ablehnungen nutzen TryPublishRejectionAsync.
    /// </summary>
    private async Task TryPublishCommandFailedAsync(CommandEnvelope cmdEnvelope, string reason)
    {
        if (_publisher == null || string.IsNullOrEmpty(cmdEnvelope.OriginSessionId))
        {
            _logger?.LogDebug("[Actor] Cannot publish CommandFailed: no publisher or no OriginSessionId");
            return;
        }

        try
        {
            var failedEvent = new EventEnvelope
            {
                AggregateId = _id,
                AggregateVersion = _state?.Version ?? 0,
                AggregateType = typeof(TState).Name,
                CorrelationId = cmdEnvelope.CorrelationId,
                CausationId = cmdEnvelope.CommandId.ToString(),
                UserId = cmdEnvelope.UserId,
                Payload = new CommandFailed(
                    cmdEnvelope.Payload.GetType().Name,
                    reason,
                    _id.ToString()),
                TargetSubscriberId = cmdEnvelope.OriginSessionId
            };

            await _publisher.PublishAsync(failedEvent);
            _logger?.LogDebug("[Actor] Published CommandFailed to '{Session}'", cmdEnvelope.OriginSessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Actor] Failed to publish CommandFailed");
        }
    }
}