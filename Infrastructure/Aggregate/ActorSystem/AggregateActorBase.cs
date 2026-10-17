using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Mapping;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

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

    // ★ Snapshot-Naht (docs/04-konsum-und-prozess-maschine.md §4.7): abgeleiteter, nicht-autoritativer Cache. Null → aus,
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

    // PF-5: Leerlauf bis zur Passivierung (Snapshot beim Stoppen, die nächste Nachricht rehydriert).
    private readonly TimeSpan _leerlauf;

    private IAggregateHandler? _handler;  // Wird nach State-Load erstellt!
    private TState? _state;
    private Guid _id;
    private bool _initialized = false;

    // Framework-Inbox (Exactly-once, docs/04 §4.4): die Wahrheit ist die Inbox-Tabelle des Stores — je Command eine Quittung in derselben
    //   Transaktion wie seine Events, eindeutig je CommandId. Hier nur ihr Cache: die CommandIds, die diese Aktivierung schon entschieden oder
    //   nachgeschlagen hat (gedeckelt, BoundedInbox), getrennt nach gewirkt und abgelehnt. Er erspart der häufigen Wiederholung kurz nach dem
    //   ersten Mal den Weg in die Datenbank; was er nicht kennt, fängt der eindeutige Schlüssel. Durable Fakten — er überlebt eine
    //   Rehydration, nur nicht die Passivierung.
    private readonly BoundedInbox _verarbeiteteCommandIds;
    private readonly BoundedInbox _abgelehnteCommandIds;

    // ★ Bündel (docs/03 §3.1): während ein Commit läuft, nimmt der Actor weitere Commands an (ReenterAfter) und sammelt sie; nach dem Commit
    //   entscheidet er sie nacheinander im Speicher — jeden auf dem Stand nach dem vorigen — und hängt sie als Teile EINES Appends an (je Teil
    //   die Metadaten und die Quittung seines Commands). Geantwortet wird jedem erst nach diesem Commit. Scheitert er, lädt der Actor frisch und
    //   behandelt jeden Command des Bündels einzeln (der Einzel-Pfad mit Nachschlagen in der Inbox).
    private readonly int _buendelGroesse;
    private readonly Queue<(CommandEnvelope Env, PID? An)> _wartend = new();
    private bool _imCommit;
    // Wem der Einzel-Pfad antwortet (der Absender des Commands, den er gerade behandelt — nicht unbedingt die aktuelle Nachricht).
    private PID? _antwortAn;

    protected AggregateActorBase(
        IAggregateHandlerFactory handlerFactory,
        IEventStoreRepository eventStore,
        IVersionTracker? versionTracker = null,
        BrokerPublisher? publisher = null,
        ILogger? logger = null,
        ISnapshotStore? snapshots = null,
        string? snapshotSchemaVersion = null,
        SnapshotOptions? snapshotOptions = null,
        IDeadLetterSink? deadLetters = null,
        Infrastructure.Lebenszyklus.Passivierung? passivierung = null)
    {
        _leerlauf = (passivierung ?? Infrastructure.Lebenszyklus.Passivierung.Standard).Aggregat;
        _handlerFactory = handlerFactory;
        _eventStore = eventStore;
        _versionTracker = versionTracker;
        _publisher = publisher;
        _logger = logger;
        _snapshots = snapshots;
        _snapshotSchemaVersion = snapshotSchemaVersion;
        _snapshotThreshold = snapshotOptions?.Threshold ?? 200;
        var inboxCache = snapshotOptions?.InboxCache ?? SnapshotOptions.StandardInboxCache;
        _buendelGroesse = Math.Max(1, snapshotOptions?.BuendelGroesse ?? SnapshotOptions.StandardBuendelGroesse);
        _verarbeiteteCommandIds = new BoundedInbox(inboxCache);
        _abgelehnteCommandIds = new BoundedInbox(inboxCache);
        _deadLetters = deadLetters;
    }

    public async Task ReceiveAsync(IContext context)
    {
        // Kein Passivieren, solange ein Commit läuft oder Commands warten — sie bekämen sonst nie eine Antwort.
        if (context.Message is ReceiveTimeout && (_imCommit || _wartend.Count > 0)) return;
        if (Infrastructure.Lebenszyklus.Passivierung.Behandle(context, _leerlauf)) return;
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
                    _wartend.Enqueue((envelope, context.Sender));
                    if (!_imCommit) await StarteBuendelAsync(context);
                    break;

                case Stopping:
                    _logger?.LogDebug("[Actor] Stopping for ID: {Id}", _id);
                    // ★ Snapshot bei Deaktivierung (best-effort): wenn seit dem letzten Snapshot Events
                    //   kamen, den aktuellen State festhalten. Fire-and-forget — Stopping darf nicht blocken. Nie mitten in einem Bündel:
                    //   dann trüge der State noch nicht committete Entscheidungen.
                    if (!_imCommit) TryWriteSnapshot();
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

        // ★ EIN Tail-Fold (docs/04 §4.7): mit Snapshot seedet der Fold und liest nur den Rest ab snapshot.Version+1.
        RehydrationResult<TState> rehydrated;
        try
        {
            rehydrated = await Infrastructure.Aggregate.AggregateRehydrator.LoadAsync<TState>(
                _id, _snapshots, _eventStore, _handlerFactory, _snapshotSchemaVersion, default);
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
        for (var versuch = 1; ; versuch++)
        {
            try
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
                return;
            }
            catch (AppendGescheitertException ex) when (versuch == 1)
            {
                // G-3: der Append ist sicher NICHT geschrieben, der Actor hat frisch rehydriert (etwa nach einem Versionskonflikt durch einen
                //   zweiten Schreiber). Einmal neu entscheiden — auf dem aktuellen Stand; ein Client-Command mit veralteter Version scheitert
                //   dabei regulär an der Vorprüfung.
                _logger?.LogInformation(ex.InnerException, "[Actor] {Command} nach gescheitertem Append auf frischem Stand neu entschieden",
                    cmdEnvelope.Payload.GetType().Name);
            }
            catch (AppendGescheitertException ex)
            {
                await TryPublishCommandFailedAsync(cmdEnvelope, ex.InnerException?.Message ?? ex.Message);
                Antworte(context, new CommandResult { Success = false, ErrorMessage = ex.InnerException?.Message ?? ex.Message, AggregateId = _id });
                return;
            }
        }
    }

    /// <summary>
    /// Was ein Append über den Command weiß (G-3, docs/11 §11.2): geschrieben; nach einem mehrdeutigen Commit schon gewirkt (die Events
    /// dieses Versuchs stehen im Log) bzw. abgelehnt; oder früher gewirkt — eine Wiederholung, deren Events dieses Versuchs NICHT im Log stehen.
    /// </summary>
    private enum AppendErgebnis { Geschrieben, SchonGewirkt, SchonAbgelehnt, FrueherGewirkt }

    /// <summary>Ein Append ist sicher nicht geschrieben; der Actor ist frisch rehydriert.</summary>
    private sealed class AppendGescheitertException(Exception innen) : Exception("Append gescheitert", innen);

    /// <summary>
    /// Der eine Append-Weg des Actors (Effekt, Noop-Marke, Ablehnungs-Marke — jeweils mit der Quittung des Commands in derselben
    /// Transaktion). Kennt die Inbox die CommandId schon (eine Wiederholung, die der Cache nicht kannte), ist nichts geschrieben und der Stand
    /// bleibt gültig: der Ausgang des ersten Mals wird quittiert. Scheitert der Append sonst — Versionskonflikt durch einen zweiten Schreiber,
    /// ein Commit, dessen Quittung verloren ging, ein DB-Fehler —, ist der In-Memory-Stand nicht mehr verlässlich: der Actor rehydriert frisch
    /// aus dem Log (G-3) und schlägt in der Inbox nach. Steht die CommandId dort, hat der Command gewirkt bzw. ist abgelehnt — der mehrdeutige
    /// Commit wird kein falsches Negativ (P10). Sonst ist sicher nichts geschrieben: <see cref="AppendGescheitertException"/>, der Aufrufer
    /// entscheidet einmal neu.
    /// </summary>
    private async Task<AppendErgebnis> AppendeAsync(CommandEnvelope cmdEnvelope, int expectedVersion, IReadOnlyList<IEvent> events,
        KommandoAusgang ausgang)
    {
        try
        {
            await _eventStore.AppendEventsAsync(
                _id,
                expectedVersion,
                events,
                correlationId: cmdEnvelope.CorrelationId,
                causationId: cmdEnvelope.CommandId.ToString(),
                aggregateType: typeof(TState).Name,
                akteur: cmdEnvelope.UserId,   // Kausalkette: der Akteur reist mit den Events zu Pipelines/Prozessen/Fristen
                beilagen: [new Quittung(cmdEnvelope.CommandId, ausgang)]);
            return AppendErgebnis.Geschrieben;
        }
        catch (KommandoSchonEntschiedenException)
        {
            return await NachgeschlagenAsync(cmdEnvelope.CommandId, wiederholung: true)
                ?? throw new InvalidOperationException($"Inbox meldet {cmdEnvelope.CommandId} als entschieden, kennt aber keinen Ausgang");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Actor] Append für {Command} an {Type}/{Id} gescheitert — rehydriere frisch und sehe in der Inbox nach",
                cmdEnvelope.Payload.GetType().Name, typeof(TState).Name, _id);
            _initialized = false;
            await InitializeAsync(_id);   // wirft sie, bleibt _initialized false: die nächste Nachricht initialisiert neu
            return await NachgeschlagenAsync(cmdEnvelope.CommandId, wiederholung: false) ?? throw new AppendGescheitertException(ex);
        }
    }

    /// <summary>
    /// Der Ausgang eines Commands laut Inbox-Tabelle (und danach im Cache), oder null, wenn sie ihn nicht kennt. <paramref name="wiederholung"/>:
    /// sicher nicht dieser Versuch hat gewirkt, sondern ein früherer.
    /// </summary>
    private async Task<AppendErgebnis?> NachgeschlagenAsync(Guid commandId, bool wiederholung)
    {
        switch (await _eventStore.AusgangAsync(commandId, default))
        {
            case KommandoAusgang.Gewirkt:
                _verarbeiteteCommandIds.Add(commandId);
                return wiederholung ? AppendErgebnis.FrueherGewirkt : AppendErgebnis.SchonGewirkt;
            case KommandoAusgang.Abgelehnt:
                _abgelehnteCommandIds.Add(commandId);
                return AppendErgebnis.SchonAbgelehnt;
            default:
                return null;
        }
    }

    /// <summary>Die Quittung für einen Command, der laut Log schon entschieden ist (nach einem mehrdeutigen Commit).</summary>
    private void QuittiereEntschieden(IContext context, AppendErgebnis ergebnis)
        => Antworte(context, ergebnis == AppendErgebnis.SchonAbgelehnt
            ? new CommandResult { Success = false, Abgelehnt = true, AggregateId = _id, ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)", NewVersion = _state!.Version }
            : new CommandResult { Success = true, AggregateId = _id, NewVersion = _state!.Version });

    /// <summary>Antwortet dem Absender des Commands, den der Einzel-Pfad gerade behandelt.</summary>
    private void Antworte(IContext context, CommandResult ergebnis)
    {
        if (_antwortAn is { } an) context.Send(an, ergebnis);
    }

    private static IDisposable SpurVon(CommandEnvelope env)
        // Spur (docs/konzept-werkbank.md §7.1): die Events dieses Commands schreibt decide:<Aggregat>/<Command>, ausgelöst von dem Log-Eintrag,
        //   den der Absender in den Envelope gelegt hat; der Absender selbst (Fluss-Knoten, Regel, Reaktion, Akteur) steht als „ueber" daneben.
        => Spur.Von(env.Ursache, WirkerId.FuerDecide(typeof(TState).Name, env.Payload.GetType().Name), env.Wirker);

    // ─── Bündel (docs/03 §3.1) ───

    /// <summary>Was der Actor für einen Command eines Bündels entschieden hat — geantwortet wird erst nach dem Commit des Bündels.</summary>
    private sealed class Entscheidung(CommandEnvelope env, PID? an)
    {
        public CommandEnvelope Env { get; } = env;
        public PID? An { get; } = an;
        public Spur.Kopf? Spur { get; set; }
        /// <summary>Ohne Schreiben entschieden (Cache, Versionskonflikt, falsches Aggregat, technischer Fehler).</summary>
        public CommandResult? Sofort { get; set; }
        /// <summary>Technischer Fehler — dazu geht <c>CommandFailed</c> an die Sitzung.</summary>
        public string? Fehler { get; set; }
        /// <summary>Dieselbe CommandId stand weiter vorn im Bündel: dieselbe Antwort.</summary>
        public int? WieVorgaenger { get; set; }
        public AppendTeil? Teil { get; set; }
        public KommandoAusgang Ausgang { get; set; }
        public List<IEvent> Persistent { get; } = new();
        public IEvent? Ablehnung { get; set; }
        public int VersionVorher { get; set; }
        public int VersionNachher { get; set; }
        public CommandResult? Ergebnis { get; set; }
        public bool Beantwortet { get; set; }
    }

    /// <summary>
    /// Bildet Bündel aus den wartenden Commands und bringt sie auf den Weg: entscheiden (im Speicher, jeder auf dem Stand nach dem vorigen),
    /// EIN Append mit einem Teil je schreibendem Command, die Fortsetzung nach dem Commit läuft wieder im Actor (<c>ReenterAfter</c>) — dazwischen
    /// nimmt er weitere Commands an. Ohne Schreiben (nur Cache-Treffer und Konflikte) antwortet er sofort und nimmt das nächste Bündel.
    /// </summary>
    private async Task StarteBuendelAsync(IContext context)
    {
        while (_wartend.Count > 0 && !_imCommit)
        {
            var buendel = new List<(CommandEnvelope Env, PID? An)>();
            while (buendel.Count < _buendelGroesse && _wartend.TryDequeue(out var w)) buendel.Add(w);
            try
            {
                if (!_initialized) await InitializeAsync(_id);
            }
            catch (Exception ex)
            {
                // Nicht rehydrierbar (vergiftet): jeder Command bekommt den Fehler, die nächste Nachricht versucht es erneut.
                foreach (var (env, an) in buendel)
                {
                    await TryPublishCommandFailedAsync(env, ex.Message);
                    if (an is not null) context.Send(an, new CommandResult { Success = false, ErrorMessage = ex.Message, AggregateId = _id });
                }
                continue;
            }
            var basis = _state!.Version;

            List<Entscheidung> entscheidungen;
            try
            {
                entscheidungen = await EntscheideAsync(buendel);
            }
            catch (Exception ex)
            {
                // Unerwartet (ein Applier wirft beim Anwenden vor dem Commit): der State im Speicher ist nicht mehr verlässlich.
                _logger?.LogWarning(ex, "[Actor] Bündel von {Anzahl} Commands an {Type}/{Id} nicht entscheidbar — einzeln", buendel.Count,
                    typeof(TState).Name, _id);
                await EinzelnAsync(context, buendel);
                continue;
            }

            var teile = entscheidungen.Where(e => e.Teil is not null).Select(e => e.Teil!).ToList();
            if (teile.Count == 0)
            {
                await AbschliessenAsync(context, entscheidungen);
                continue;
            }

            _imCommit = true;
            var commit = _eventStore.AppendTeileAsync(_id, basis, teile, typeof(TState).Name);
            context.ReenterAfter(commit, async t =>
            {
                _imCommit = false;
                if (t.IsCompletedSuccessfully)
                {
                    try
                    {
                        await AbschliessenAsync(context, entscheidungen);
                    }
                    catch (Exception ex)
                    {
                        // Der Commit ist durch (P10): nichts davon darf ein Fehler werden. Den State frisch laden; wer noch keine Antwort hat, bekommt
                        //   den durablen Ausgang über eine Wiederholung (Inbox).
                        _logger?.LogError(ex, "[Actor] Fehler NACH dem Commit eines Bündels an {Type}/{Id} — rehydriere", typeof(TState).Name, _id);
                        _initialized = false;
                        foreach (var e in entscheidungen.Where(e => !e.Beantwortet && e.An is not null))
                            context.Send(e.An!, e.Ergebnis ?? e.Sofort ?? new CommandResult
                            {
                                Success = e.Ausgang == KommandoAusgang.Gewirkt, Abgelehnt = e.Ausgang == KommandoAusgang.Abgelehnt, AggregateId = _id,
                                NewVersion = e.VersionNachher,
                            });
                    }
                }
                else
                {
                    // Versionskonflikt durch einen zweiten Schreiber, eine Wiederholung, die der Cache nicht kannte, ein mehrdeutiger Commit,
                    //   ein DB-Fehler: alles im Bündel ist hinfällig — frisch laden, jeden Command einzeln (mit Nachschlagen in der Inbox).
                    _logger?.LogInformation(t.Exception?.GetBaseException(), "[Actor] Bündel-Append ({Anzahl} Commands) an {Type}/{Id} gescheitert — einzeln",
                        buendel.Count, typeof(TState).Name, _id);
                    await EinzelnAsync(context, buendel);
                }
                await StarteBuendelAsync(context);
            });
            return;
        }
    }

    /// <summary>Entscheidet die Commands eines Bündels nacheinander; ein schreibender Command wendet seine Events gleich an (spekulativ bis zum Commit).</summary>
    private async Task<List<Entscheidung>> EntscheideAsync(List<(CommandEnvelope Env, PID? An)> buendel)
    {
        var ergebnis = new List<Entscheidung>(buendel.Count);
        var imBuendel = new Dictionary<Guid, int>();
        foreach (var (env, an) in buendel)
        {
            var e = new Entscheidung(env, an);
            using (SpurVon(env))
            {
                e.Spur = Spur.Aktuell;
                if (imBuendel.TryGetValue(env.CommandId, out var vor)) e.WieVorgaenger = vor;
                else
                {
                    imBuendel[env.CommandId] = ergebnis.Count;
                    await EntscheideAsync(e);
                }
            }
            ergebnis.Add(e);
        }
        return ergebnis;
    }

    /// <summary>Dieselbe Entscheidung wie der Einzel-Pfad (<see cref="HandleCommandCoreAsync"/>), ohne Append: Vorprüfung, Decide, Teil.</summary>
    private async Task EntscheideAsync(Entscheidung e)
    {
        var env = e.Env;
        if (env.AggregateId != _id)
        {
            e.Fehler = "Command sent to wrong aggregate";
            e.Sofort = new CommandResult { Success = false, ErrorMessage = e.Fehler, AggregateId = _id };
            return;
        }

        var expectedVersion = env.Modus is CommandModus.Client c ? c.ExpectedVersion : _state!.Version;
        switch (CommandVorpruefung.Prüfe(true, _verarbeiteteCommandIds.Contains(env.CommandId), _abgelehnteCommandIds.Contains(env.CommandId),
                    _state!.Version, expectedVersion))
        {
            case CommandVorpruefung.Ergebnis.IdempotenterErfolg:
                e.Sofort = new CommandResult { Success = true, AggregateId = _id, NewVersion = _state.Version };
                return;
            case CommandVorpruefung.Ergebnis.IdempotenteAblehnung:
                e.Sofort = new CommandResult
                {
                    Success = false, Abgelehnt = true, AggregateId = _id, ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)", NewVersion = _state.Version,
                };
                return;
            case CommandVorpruefung.Ergebnis.VersionsKonflikt:
                if (await NachgeschlagenAsync(env.CommandId, wiederholung: true) is { } schon)
                {
                    e.Sofort = schon == AppendErgebnis.SchonAbgelehnt
                        ? new CommandResult { Success = false, Abgelehnt = true, AggregateId = _id, ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)", NewVersion = _state.Version }
                        : new CommandResult { Success = true, AggregateId = _id, NewVersion = _state.Version };
                    return;
                }
                e.Fehler = $"Concurrency conflict: expected version {expectedVersion}, actual {_state.Version}";
                e.Sofort = new CommandResult { Success = false, ErrorMessage = e.Fehler, AggregateId = _id, NewVersion = _state.Version };
                return;
        }

        List<IEvent> alle;
        try
        {
            alle = _handler!.HandleCommand(env.Payload).ToList();
        }
        catch (Exception ex)
        {
            e.Fehler = ex.Message;
            e.Sofort = new CommandResult { Success = false, ErrorMessage = ex.Message, AggregateId = _id };
            return;
        }
        var persistent = alle.Where(x => x is not ITransientEvent).ToList();
        var ablehnungen = alle.Where(x => x is ITransientEvent).ToList();

        if (ablehnungen.Count > 0 && (persistent.Count > 0 || ablehnungen.Count > 1))
        {
            e.Fehler = $"Decider für {env.Payload.GetType().Name} lieferte ein ungültiges gemischtes Ergebnis: {persistent.Count} persistente(s) " +
                       $"Event(s) + {ablehnungen.Count} Ablehnung(en). Eine Ablehnung (ITransientEvent) muss das einzige zurückgegebene Event sein.";
            e.Sofort = new CommandResult { Success = false, ErrorMessage = e.Fehler, AggregateId = _id };
            return;
        }

        IReadOnlyList<IEvent> schreibe;
        e.VersionVorher = _state.Version;
        if (alle.Count == 0)
        {
            // Noop: die Marke macht „getan" für den Prozess-Fold beobachtbar (Audit-Fix K2).
            e.Ausgang = KommandoAusgang.Gewirkt;
            schreibe = new IEvent[] { new KommandoVerarbeitet(env.CommandId) };
            _state.Version++;
        }
        else if (ablehnungen.Count == 1)
        {
            e.Ausgang = KommandoAusgang.Abgelehnt;
            e.Ablehnung = ablehnungen[0];
            schreibe = new IEvent[] { new KommandoAbgelehnt(env.CommandId, e.Ablehnung.GetType().Name) };
            _state.Version++;
        }
        else
        {
            e.Ausgang = KommandoAusgang.Gewirkt;
            e.Persistent.AddRange(persistent);
            schreibe = persistent;
            foreach (var evt in persistent)
            {
                if (evt is not IProzessIntern) _handler.ApplyEvent(evt);
                _state.Version++;
            }
        }
        e.VersionNachher = _state.Version;
        e.Teil = new AppendTeil(schreibe, env.CorrelationId, env.CommandId.ToString(), env.UserId, e.Spur,
            [new Quittung(env.CommandId, e.Ausgang)]);
    }

    /// <summary>
    /// Nach dem Commit des Bündels (oder ohne Schreiben): Cache, Signale, Ablehnungen an die Sitzung, Antworten — in der Reihenfolge der Commands.
    /// Alles hier ist abgeleitet bzw. best-effort; der Commit ist durch.
    /// </summary>
    private async Task AbschliessenAsync(IContext context, List<Entscheidung> entscheidungen)
    {
        var geschrieben = false;
        foreach (var e in entscheidungen)
        {
            using (Spur.Von(e.Spur?.Ursache, e.Spur?.Wirker, e.Spur?.Ueber))
            {
                if (e.WieVorgaenger is int vor)
                {
                    e.Ergebnis = entscheidungen[vor].Ergebnis;
                }
                else if (e.Sofort is { } sofort)
                {
                    if (e.Fehler is { } fehler) await TryPublishCommandFailedAsync(e.Env, fehler);
                    e.Ergebnis = sofort;
                }
                else if (e.Ausgang == KommandoAusgang.Abgelehnt)
                {
                    geschrieben = true;
                    _abgelehnteCommandIds.Add(e.Env.CommandId);
                    // Spur: eine Ablehnung als verlierbarer Hinweis an die Werkbank.
                    Infrastructure.Einblick.SpurHinweise.Melde(Infrastructure.Einblick.SpurHinweise.Ablehnung, e.Spur?.Wirker, e.Env.Ursache,
                        e.Env.CorrelationId, $"{e.Env.Payload.GetType().Name} abgelehnt: {e.Ablehnung!.GetType().Name}");
                    await TryPublishRejectionAsync(e.Env, e.Ablehnung);
                    e.Ergebnis = new CommandResult
                    {
                        Success = false, Abgelehnt = true, AggregateId = _id, RejectionEvent = e.Ablehnung, ErrorMessage = e.Ablehnung.ToString(),
                        NewVersion = e.VersionNachher, ErsteVersion = e.VersionVorher + 1,
                    };
                }
                else
                {
                    geschrieben = true;
                    _verarbeiteteCommandIds.Add(e.Env.CommandId);
                    if (e.Persistent.Count > 0 && _publisher != null)
                    {
                        try { await PublishEventsAsync(e.Persistent, e.Env, e.VersionVorher); }
                        catch (Exception ex) { _logger?.LogWarning(ex, "[Actor] Signale nach dem Commit nicht veröffentlicht"); }
                    }
                    e.Ergebnis = new CommandResult
                    {
                        Success = true, AggregateId = _id, Events = e.Persistent, NewVersion = e.VersionNachher, ErsteVersion = e.VersionVorher + 1,
                    };
                }
            }
            if (e.An is { } an) context.Send(an, e.Ergebnis!);
            e.Beantwortet = true;
        }
        if (geschrieben)
        {
            await TryTrackVersionAsync();
            TrySnapshotOnThreshold();
        }
    }

    /// <summary>
    /// Der Rückfall nach einem gescheiterten Bündel: frisch laden, dann jeden Command einzeln über den Einzel-Pfad (Vorprüfung, Decide, Append,
    /// Nachschlagen in der Inbox nach einem gescheiterten Append — G-3). Ein Command, den das Bündel schon geschrieben hatte (mehrdeutiger
    /// Commit), scheitert dort an seiner Quittung und bekommt den Ausgang des ersten Mals.
    /// </summary>
    private async Task EinzelnAsync(IContext context, List<(CommandEnvelope Env, PID? An)> buendel)
    {
        _initialized = false;
        foreach (var (env, an) in buendel)
        {
            _antwortAn = an;
            try
            {
                if (!_initialized) await InitializeAsync(_id);
                using (SpurVon(env))
                    await HandleCommandAsync(context, env);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Actor] Einzel-Pfad für {Command} gescheitert", env.Payload.GetType().Name);
                await TryPublishCommandFailedAsync(env, ex.Message);
                Antworte(context, new CommandResult { Success = false, ErrorMessage = ex.Message, AggregateId = _id });
            }
            finally
            {
                _antwortAn = null;
            }
        }
    }

    /// <summary>
    /// Externer Client-Command: OCC gegen die behauptete Version. ★ Jetzt AUCH idempotent (docs/08 §8.4) —
    /// der Client darf nach verlorenem Ack denselben Command (deterministische CommandId) erneut senden;
    /// die Framework-Inbox erkennt die Wiederholung VOR der OCC-Prüfung und liefert idempotent denselben
    /// Ausgang (Erfolg/Ablehnung), statt eines mehrdeutigen „Concurrency conflict". Ein frischer Command
    /// (neue CommandId) fällt weiterhin durch zur OCC-Assertion.
    /// </summary>
    private Task HandleClientCommand(IContext context, CommandEnvelope cmdEnvelope, int expectedVersion)
        => HandleCommandCoreAsync(context, cmdEnvelope, istIdempotent: true, expectedVersion);

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
                Antworte(context, new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = "Command sent to wrong aggregate",
                    AggregateId = _id
                });
                return;
            }

            // ★ Vorab-Entscheidung (docs/08 §8.4) — Inbox-Dedup VOR der OCC-Prüfung (Reihenfolge in der reinen,
            //   Ebene-1-getesteten CommandVorpruefung.Prüfe gekapselt). Ein wiederholt zugestellter Command
            //   (at-least-once — Emittiert IMMER, Client bei Retry nach verlorenem Ack) liefert
            //   idempotent DASSELBE Ergebnis; ein frischer Command fällt durch zur OCC-Assertion.
            switch (CommandVorpruefung.Prüfe(
                        istIdempotent,
                        _verarbeiteteCommandIds.Contains(cmdEnvelope.CommandId),
                        _abgelehnteCommandIds.Contains(cmdEnvelope.CommandId),
                        _state!.Version,
                        expectedVersion))
            {
                case CommandVorpruefung.Ergebnis.IdempotenterErfolg:
                    Antworte(context, new CommandResult { Success = true, AggregateId = _id, NewVersion = _state.Version });
                    return;

                case CommandVorpruefung.Ergebnis.IdempotenteAblehnung:
                    Antworte(context, new CommandResult
                    {
                        Success = false,
                        Abgelehnt = true,
                        AggregateId = _id,
                        ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)",
                        NewVersion = _state.Version
                    });
                    return;

                case CommandVorpruefung.Ergebnis.VersionsKonflikt:
                    // Eine Client-Wiederholung, die der Cache nicht (mehr) kennt, sieht hier eine weitergerückte Version — ob sie schon
                    //   entschieden ist, weiß nur die Inbox. Nur auf diesem Weg und nach einem gescheiterten Append fragt der Actor sie.
                    if (await NachgeschlagenAsync(cmdEnvelope.CommandId, wiederholung: true) is { } schon)
                    {
                        QuittiereEntschieden(context, schon);
                        return;
                    }
                    var errorMsg = $"Concurrency conflict: expected version {expectedVersion}, actual {_state.Version}";
                    _logger?.LogDebug("[Actor] {ErrorMsg}", errorMsg);
                    await TryPublishCommandFailedAsync(cmdEnvelope, errorMsg);
                    Antworte(context, new CommandResult
                    {
                        Success = false,
                        ErrorMessage = errorMsg,
                        AggregateId = _id,
                        NewVersion = _state.Version  // Pipeline braucht die aktuelle Version für Retry
                    });
                    return;

                case CommandVorpruefung.Ergebnis.Fortfahren:
                    break;
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
                if (istIdempotent && await CoCommitInboxMarkeAsync(cmdEnvelope, expectedVersion) is var noop and not AppendErgebnis.Geschrieben)
                {
                    QuittiereEntschieden(context, noop);
                    return;
                }

                Antworte(context, new CommandResult
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
                // Spur: eine Ablehnung steht (auf dem Client-Pfad) nicht im Log — als verlierbarer Hinweis an die Werkbank.
                Infrastructure.Einblick.SpurHinweise.Melde(Infrastructure.Einblick.SpurHinweise.Ablehnung, Spur.Aktuell?.Wirker, cmdEnvelope.Ursache,
                    cmdEnvelope.CorrelationId, $"{cmdEnvelope.Payload.GetType().Name} abgelehnt: {rejection.GetType().Name}");

                // ★ Treiber-Fold (EM-1) — Wende der früheren K2-Entscheidung: auf dem EMITTIERTEN Pfad
                //   (Reaktion/Prozess) wird die Ablehnung jetzt DURABEL als KommandoAbgelehnt co-committet. Der
                //   Prozess-Manager faltet sie als eigene Achse (AbgelehntDa) zu SchrittGescheitert — er braucht
                //   die Ziel-Quittung nicht mehr (der EINE Emit-Weg). Der frühere Falsch-Erfolg-Fallstrick (eine
                //   RE-Zustellung liefert per Inbox-Dedup Success:true → Manager verzweigt auf Erfolg) ist durch
                //   die ZWEITE Inbox-Menge geschlossen: ein abgelehnter Vorgang bleibt bei Re-Delivery auf
                //   Success:false (siehe Dedup-Zweig oben). Marke + Zwei-Mengen-Inbox + Fold-Achse zusammen (§4).
                //   Auch der Client-Pfad schreibt die Marke (er ist idempotent, docs/08 §8.4); die Ablehnung geht dort zusätzlich
                //   targeted an den Aufrufer.
                if (istIdempotent && await CoCommitAblehnungsMarkeAsync(cmdEnvelope, expectedVersion, rejection.GetType().Name) is var marke
                    and not AppendErgebnis.Geschrieben)
                {
                    QuittiereEntschieden(context, marke);
                    return;
                }

                // Targeted Delivery — nur an den Aufrufer
                await TryPublishRejectionAsync(cmdEnvelope, rejection);

                Antworte(context, new CommandResult
                {
                    Success = false,
                    Abgelehnt = true,
                    AggregateId = _id,
                    RejectionEvent = rejection,
                    ErrorMessage = rejection.ToString()
                });
                return;
            }

            // 5. Erfolg — persistente Events verarbeiten.
            //    ★ Phase 1: Metadaten mit ins Log stempeln (nach Log-Read verfügbar).
            //    ★ Inbox: die Quittung reist in DERSELBEN Transaktion wie der Effekt (Inbox-Tabelle) → exactly-once-wirksam, crash-fest.
            //      Eine Marke im Stream braucht die Wirkung nicht: ihr erstes Event trägt causation_id = CommandId, das liest der Prozess-Fold.
            var append = await AppendeAsync(cmdEnvelope, expectedVersion, persistentEvents, KommandoAusgang.Gewirkt);
            if (append != AppendErgebnis.Geschrieben)
            {
                // Mehrdeutiger Commit (G-3): die Events stehen im Log, der Actor hat sie beim Rehydrieren schon gefaltet. Die Signale
                //   gehen trotzdem hinaus (verlierbar, der Poll trüge sie sonst nach); quittiert wird der durable Ausgang.
                if (append == AppendErgebnis.SchonGewirkt && _publisher != null)
                {
                    try { await PublishEventsAsync(persistentEvents, cmdEnvelope, expectedVersion); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "[Actor] Signale nach mehrdeutigem Commit nicht veröffentlicht"); }
                }
                QuittiereEntschieden(context, append);
                return;
            }

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
                // 6. Events applyen (Applier) — mutiert _state.
                // ★ Audit-Fix #5 (Symmetrie Live == Rehydration): ein IProzessIntern-Persistent-Event wird
                //   NICHT angewandt (nur die Version mitgezählt) — exakt wie die Rehydration (AggregateRehydrator).
                foreach (var evt in persistentEvents)
                {
                    if (evt is not IProzessIntern) _handler.ApplyEvent(evt);
                    _state.Version++;
                }
                _verarbeiteteCommandIds.Add(cmdEnvelope.CommandId);

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
                Antworte(context, new CommandResult
                {
                    Success = true,
                    AggregateId = _id,
                    Events = persistentEvents,
                    NewVersion = _state.Version,
                    ErsteVersion = expectedVersion + 1,
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

                Antworte(context, new CommandResult
                {
                    Success = true,
                    AggregateId = _id,
                    NewVersion = _state?.Version ?? (expectedVersion + persistentEvents.Count)
                });
            }
        }
        catch (Exception ex) when (ex is not AppendGescheitertException)
        {
            // Catch-Block ist jetzt nur noch für technische Fehler
            // (DB-Timeout, Null-Referenz, etc. — nicht für Domänen-Ablehnungen)
            _logger?.LogError(ex, "[Actor] Technical error: {Message}", ex.Message);

            await TryPublishCommandFailedAsync(cmdEnvelope, ex.Message);
            
            Antworte(context, new CommandResult 
            { 
                Success = false, 
                ErrorMessage = ex.Message,
                AggregateId = _id
            });
        }
    }

    /// <summary>
    /// Co-committet AUSSCHLIESSLICH die Noop-Marke mit der Quittung (idempotenter Pfad) — ein durabler Beleg „dieser Vorgang wurde
    /// verarbeitet", ohne Domänen-Effekt. Die Quittung allein dedupliziert; die Marke im Stream braucht der Prozess-Fold, der den Ausgang
    /// über die Kausalität im Ziel-Stream erkennt (eine Wirkung hat dort ihr Event, ein Noop nur diese Marke). Die Marke ist
    /// <see cref="IProzessIntern"/> → beim Domänen-Falten übersprungen, in der Version aber mitgezählt (Stream-Position bündig). Basis ist die
    /// aufgelöste aktuelle Version (Single-Writer), sodass die Marten-OCC eine echte Doppelaktivierung weiterhin abweist.
    /// </summary>
    private async Task<AppendErgebnis> CoCommitInboxMarkeAsync(CommandEnvelope cmdEnvelope, int expectedVersion)
    {
        var ergebnis = await AppendeAsync(cmdEnvelope, expectedVersion,
            new IEvent[] { new Infrastructure.Aggregate.KommandoVerarbeitet(cmdEnvelope.CommandId) }, KommandoAusgang.Gewirkt);
        if (ergebnis != AppendErgebnis.Geschrieben) return ergebnis;   // schon im frisch rehydrierten Stand

        _state!.Version++;
        _verarbeiteteCommandIds.Add(cmdEnvelope.CommandId);
        return ergebnis;
    }

    /// <summary>
    /// Co-committet AUSSCHLIESSLICH die Framework-ABLEHNUNGS-Marke (idempotenter Pfad, fachliche Ablehnung) —
    /// ein durabler Beleg „dieser Vorgang wurde abgelehnt" MIT dem Grund, ohne Domänen-Effekt. Gleiche native
    /// Transaktion wie ein normaler Append; die Marke ist <see cref="IProzessIntern"/> → beim Domänen-Falten
    /// übersprungen, in der Version aber mitgezählt (Stream-Position bündig). Der Prozess-Manager faltet sie zu
    /// <c>SchrittGescheitert</c> (Treiber-Fold/EM-1). Basis ist die aufgelöste aktuelle Version (Single-Writer).
    /// </summary>
    private async Task<AppendErgebnis> CoCommitAblehnungsMarkeAsync(CommandEnvelope cmdEnvelope, int expectedVersion, string grund)
    {
        var ergebnis = await AppendeAsync(cmdEnvelope, expectedVersion,
            new IEvent[] { new Infrastructure.Aggregate.KommandoAbgelehnt(cmdEnvelope.CommandId, grund) }, KommandoAusgang.Abgelehnt);
        if (ergebnis != AppendErgebnis.Geschrieben) return ergebnis;   // schon im frisch rehydrierten Stand

        _state!.Version++;
        _abgelehnteCommandIds.Add(cmdEnvelope.CommandId);
        return ergebnis;
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

    // ─── Snapshot (docs/04-konsum-und-prozess-maschine.md §4.7): capture-in-turn + fire-and-forget ───

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
    /// Friert den State (Deep-Copy) IN-TURN ein — der spätere detached DB-Write kann das
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
        // ★ Phase 1: Version PRO Event — der Stream stand vor dem Batch auf
        //   baseVersion (aufgelöst: bei Sentinel = aktuelle Actor-Version); jedes Event trägt
        //   seine eigene Position, nie den Batch-Endwert.
        var envelopes = EventEnvelopeFactory.BuildPerEvent(
            events,
            _id,
            typeof(TState).Name,
            baseVersion: baseVersion,
            correlationId: cmdEnvelope.CorrelationId,
            causationId: cmdEnvelope.CommandId.ToString(),
            userId: cmdEnvelope.UserId,
            // Der Actor-State steht hier bereits auf dem Head NACH Domain-Events + co-committer
            // Marke — genau die ExpectedVersion, die der nächste Client-Command tragen muss.
            streamHeadVersion: _state.Version,
            spur: Spur.Aktuell);

        // ★ Perf: Publish ist reine Benachrichtigung — der Command ist bereits im Log committet
        //   (Schritt 5). Zustellung ist best-effort (Invariante 2); die durablen Konsumenten
        //   heilen über Log-Read + Poll-Backstop. Darum NICHT im Single-Writer-Turn auf die
        //   Shard-Acks warten, sondern fire-and-forget feuern — der Turn kehrt sofort zurück
        //   und der Per-Stream-Command-Durchsatz hängt nicht mehr an 6 Broker-Round-Trips/Event.
        foreach (var eventEnvelope in envelopes)
        {
            _publisher!.PublishFireAndForget(eventEnvelope);

            // ★ Phase 1 (1c): zusätzlich das typisierte Signal veröffentlichen (docs/03 §3.6).
            //   Best-effort — ein Signal ist nur ein Weckruf und DARF verloren gehen
            //   (Invariante 2); der Poll-Backstop holt es nach.
            var signal = Anwendung.Signale.Create(
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