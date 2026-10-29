using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Lebenszyklus;
using Infrastructure.Mapping;
using Infrastructure.Emit;
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
///   Der Actor trennt persistent/transient und meldet Ablehnungen gezielt an die Sitzung (verlierbar, ohne zu warten).
///   CommandFailed bleibt für technische Fehler (catch-Block).
///
/// ★ Das <see cref="CommandResult"/> trägt nur Ausgang, Versionen und Ablehnung; die Events stehen im Log.
/// ★ Direkte Abfrage (Plan Pub/Sub-Neustrukturierung §2.6): eine <see cref="AggregatFrage"/> beantwortet die <c>Frage</c> des Deciders im Turn, auf dem
///   committeten Stand — exakt dem, auf dem der nächste Command entschieden würde; läuft ein Commit, wartet sie bis danach.
/// ★ Lebenszyklus aus dem Rahmen (<see cref="RahmenActor"/>): Passivierung nur, wenn kein Commit läuft und kein Command wartet.
/// </summary>
public abstract class AggregateActorBase<TState> : RahmenActor
    where TState : class, IState, new()
{
    private readonly IAggregateHandlerFactory _handlerFactory;
    private readonly IEventStoreRepository _eventStore;
    private readonly Infrastructure.Verteilen.Verteilung? _verteilung;
    // Das Hinweis-Netz (Plan Pub/Sub-Neustrukturierung §2.9): ein Hinweis aus dem Decide (eine Ablehnung), den eine Funktion hört, geht direkt an
    //   ihre Adresse — ohne Log, ohne Broker, höchstens einmal. Null = keine Funktion hört Hinweise.
    private readonly Infrastructure.Funktionen.HinweisNetz? _hinweisNetz;
    private readonly ILogger? _logger;

    // ★ Snapshot-Naht (docs/04-konsum-und-prozess-maschine.md §4.8): abgeleiteter, nicht-autoritativer Cache. Null → aus,
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
    // Die Id kommt aus der Cluster-Identität (Plan §1.7, Phase 1), nicht aus dem ersten Envelope: ein Command an eine andere Id wird abgelehnt,
    //   bevor etwas geladen ist — sonst lüde der Actor dieser Identität den fremden Stream.
    private Guid _id;
    private bool _identitaetGueltig;
    private bool _initialized = false;

    // Framework-Inbox (Exactly-once, docs/04 §4.6): die Wahrheit ist die Inbox-Tabelle des Stores — je Command eine Quittung in derselben
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

    // ★ Direkte Abfrage (Plan §2.6): während eines Commits trägt der State die Entscheidungen des Bündels, die noch scheitern können — eine Frage
    //   in dieser Zeit wartet hier und wird nach dem Commit beantwortet, bevor das nächste Bündel entschieden wird.
    private readonly Queue<(AggregatFrage Frage, PID? An)> _fragen = new();

    protected AggregateActorBase(
        IAggregateHandlerFactory handlerFactory,
        IEventStoreRepository eventStore,
        Infrastructure.Verteilen.Verteilung? verteilung = null,
        ILogger? logger = null,
        ISnapshotStore? snapshots = null,
        string? snapshotSchemaVersion = null,
        SnapshotOptions? snapshotOptions = null,
        IDeadLetterSink? deadLetters = null,
        Passivierung? passivierung = null,
        Infrastructure.Funktionen.HinweisNetz? hinweisNetz = null)
        // PF-5: Leerlauf bis zur Passivierung (Snapshot beim Stoppen, die nächste Nachricht rehydriert).
        : base(logger, (passivierung ?? Passivierung.Standard).Aggregat)
    {
        _hinweisNetz = hinweisNetz;
        _handlerFactory = handlerFactory;
        _eventStore = eventStore;
        _verteilung = verteilung;
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

    /// <summary>Kein Passivieren, solange ein Commit läuft oder Commands warten — sie bekämen sonst nie eine Antwort.</summary>
    protected override bool Beschaeftigt => base.Beschaeftigt || _imCommit || _wartend.Count > 0 || _fragen.Count > 0;

    protected override Task BeginneAsync(IContext context)
    {
        _identitaetGueltig = Guid.TryParse(context.ClusterIdentity()?.Identity, out _id);
        _logger?.LogDebug("[Actor] Started {Type}/{Id}", typeof(TState).Name, _id);
        return Task.CompletedTask;
    }

    protected override async Task BeimStoppenAsync(IContext context)
    {
        _logger?.LogDebug("[Actor] Stopping for ID: {Id}", _id);
        // ★ Snapshot bei Deaktivierung (best-effort): wenn seit dem letzten Snapshot Events
        //   kamen, den aktuellen State festhalten — abgewartet mit Frist. Nie mitten in einem Bündel: dann trüge der State noch nicht
        //   committete Entscheidungen.
        if (!_imCommit) await SnapshotBeimStoppenAsync();
    }

    protected override async Task BehandleAsync(IContext context)
    {
        if (context.Message is AggregatFrage frage)
        {
            await FrageAsync(context, frage);
            return;
        }
        if (context.Message is not CommandEnvelope envelope) return;
        try
        {
            if (!_identitaetGueltig || envelope.AggregateId != _id)
            {
                LehneFremdeIdAb(context, envelope);
                return;
            }
            if (!_initialized)
            {
                await InitializeAsync();
            }
            _wartend.Enqueue((envelope, context.Sender));
            if (!_imCommit) await StarteBuendelAsync(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Actor] Error in ReceiveAsync");

            // WICHTIG: Auch bei Fehler antworten, sonst Retry! Der Fehler ist beantwortet — kein Neustart.
            MeldeCommandFailed(envelope, ex.Message);
            context.Respond(new CommandResult { Success = false, ErrorMessage = ex.Message });
        }
    }

    /// <summary>
    /// Ein Command, dessen Ziel nicht die Identität dieses Actors ist (falsch geroutet oder eine fremde Id): abgelehnt, bevor geladen wird —
    /// technischer Fehler an den Absender und gezielt an die Sitzung, nichts gelesen, nichts geschrieben.
    /// </summary>
    private void LehneFremdeIdAb(IContext context, CommandEnvelope envelope)
    {
        var grund = $"Command an {typeof(TState).Name}/{envelope.AggregateId} erreichte die Identität '{context.ClusterIdentity()?.Identity}' — abgelehnt";
        _logger?.LogWarning("[Actor] {Grund}", grund);
        MeldeCommandFailed(envelope, grund);
        context.Respond(new CommandResult { Success = false, ErrorMessage = grund });
    }

    // ─── Direkte Abfrage (Plan Pub/Sub-Neustrukturierung §2.6) ───

    /// <summary>
    /// Eine Frage an dieses Aggregat: im Turn beantwortet, auf dem committeten Stand. Läuft ein Commit, wartet sie bis danach (der State trägt
    /// dann Entscheidungen, die noch scheitern können). Eine Frage an eine fremde Identität (falsch geroutet) wird nicht beantwortet, bevor etwas
    /// geladen ist — sonst lüde der Actor dieser Identität den fremden Stream.
    /// </summary>
    private async Task FrageAsync(IContext context, AggregatFrage frage)
    {
        if (!_identitaetGueltig || frage.Ziel != _id)
        {
            var grund = $"Query an {typeof(TState).Name}/{frage.Ziel} erreichte die Identität '{context.ClusterIdentity()?.Identity}' — nicht beantwortet";
            _logger?.LogWarning("[Actor] {Grund}", grund);
            context.Respond(new AggregatAntwort(null, 0, grund));
            return;
        }
        if (_imCommit)
        {
            _fragen.Enqueue((frage, context.Sender));
            return;
        }
        context.Respond(await BeantworteAsync(frage));
    }

    /// <summary>Die Antwort der Frage des Deciders und die Version des Stands, auf dem sie gegeben ist; ein Fehler (nicht rehydrierbar, die Frage
    /// warf) wird die Antwort — er bricht nichts.</summary>
    private async Task<AggregatAntwort> BeantworteAsync(AggregatFrage frage)
    {
        try
        {
            if (!_initialized) await InitializeAsync();
            return new AggregatAntwort(_handler!.Frage(frage.Query), _state!.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Actor] {Query} an {Type}/{Id} nicht beantwortet", frage.Query.GetType().Name, typeof(TState).Name, _id);
            return new AggregatAntwort(null, _state?.Version ?? 0, ex.Message);
        }
    }

    /// <summary>Die Fragen, die während des Commits kamen — nach ihm, auf dem committeten Stand, bevor das nächste Bündel entschieden wird.</summary>
    private async Task BeantworteWartendeAsync(IContext context)
    {
        while (_fragen.TryDequeue(out var w))
        {
            var antwort = await BeantworteAsync(w.Frage);
            if (w.An is not null) context.Send(w.An, antwort);
        }
    }

    private async Task InitializeAsync()
    {
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
                MeldeCommandFailed(cmdEnvelope, ex.InnerException?.Message ?? ex.Message);
                Antworte(context, new CommandResult { Success = false, ErrorMessage = ex.InnerException?.Message ?? ex.Message });
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
    /// Der eine Append-Weg des Actors: die Events eines Commands mit seiner Quittung in derselben Transaktion — bei einem Noop oder einer
    /// Ablehnung nur die Quittung, gültig, wenn der Stream noch auf dem Stand der Entscheidung steht (Plan Pub/Sub-Neustrukturierung Phase 5:
    /// keine Marken im Log, die Inbox-Tabelle hält „gewirkt/abgelehnt“). Kennt die Inbox die CommandId schon (eine Wiederholung, die der Cache nicht kannte), ist nichts geschrieben und der Stand
    /// bleibt gültig: der Ausgang des ersten Mals wird quittiert. Scheitert der Append sonst — Versionskonflikt durch einen zweiten Schreiber,
    /// ein Commit, dessen Quittung verloren ging, ein DB-Fehler —, ist der In-Memory-Stand nicht mehr verlässlich: der Actor rehydriert frisch
    /// aus dem Log (G-3) und schlägt in der Inbox nach. Steht die CommandId dort, hat der Command gewirkt bzw. ist abgelehnt — der mehrdeutige
    /// Commit wird kein falsches Negativ (P10). Sonst ist sicher nichts geschrieben: <see cref="AppendGescheitertException"/>, der Aufrufer
    /// entscheidet einmal neu.
    /// </summary>
    private async Task<AppendErgebnis> AppendeAsync(CommandEnvelope cmdEnvelope, int expectedVersion, IReadOnlyList<IEvent> events,
        KommandoAusgang ausgang, IReadOnlyList<Beilage>? zusatz = null)
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
                beilagen: [new Quittung(cmdEnvelope.CommandId, ausgang), .. zusatz ?? Array.Empty<Beilage>()]);
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
            await InitializeAsync();   // wirft sie, bleibt _initialized false: die nächste Nachricht initialisiert neu
            return await NachgeschlagenAsync(cmdEnvelope.CommandId, wiederholung: false) ?? throw new AppendGescheitertException(ex);
        }
    }

    /// <summary>
    /// Die Fristen einer Entscheidung (Plan Pub/Sub-Neustrukturierung Phase 3): je <see cref="FristGesetzt"/> die Beilage für das
    /// Fälligkeits-Verzeichnis der Uhr an der Version, die das Event im Stream bekommt (ab <paramref name="versionVorher"/>) — in derselben
    /// Transaktion wie die Events. Eine Frist zielt auf einen Command DIESES Aggregats (R5); eine fremde ist ein Fehler, nichts wird geschrieben.
    /// </summary>
    private IReadOnlyList<Beilage> Fristen(IReadOnlyList<IEvent> events, int versionVorher, string? akteur)
    {
        List<Beilage>? fristen = null;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is not FristGesetzt f) continue;
            if (f.Befehl.Ziel != _id)
                throw new InvalidOperationException($"{typeof(TState).Name}: die Frist {f.Befehl.GetType().Name} zielt auf {f.Befehl.Ziel}, nicht auf dieses "
                    + $"Aggregat ({_id}) — eine Frist geht an einen Command des eigenen Aggregats (R5).");
            var form = Infrastructure.Funktionen.Befehlsform.Schreibe(f.Befehl);
            (fristen ??= new()).Add(new FristFaellig(versionVorher + 1 + i, form.Typ, form.Daten, f.Nach, ImAuftrag.IstAkteur(akteur) ? akteur : null));
        }
        return fristen ?? (IReadOnlyList<Beilage>)Array.Empty<Beilage>();
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
            ? new CommandResult { Success = false, Abgelehnt = true, ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)", NewVersion = _state!.Version }
            : new CommandResult { Success = true, NewVersion = _state!.Version });

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
                if (!_initialized) await InitializeAsync();
            }
            catch (Exception ex)
            {
                // Nicht rehydrierbar (vergiftet): jeder Command bekommt den Fehler, die nächste Nachricht versucht es erneut.
                foreach (var (env, an) in buendel)
                {
                    MeldeCommandFailed(env, ex.Message);
                    if (an is not null) context.Send(an, new CommandResult { Success = false, ErrorMessage = ex.Message });
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
                                Success = e.Ausgang == KommandoAusgang.Gewirkt, Abgelehnt = e.Ausgang == KommandoAusgang.Abgelehnt,
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
                await BeantworteWartendeAsync(context);
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
        var expectedVersion = env.Modus is CommandModus.Client c ? c.ExpectedVersion : _state!.Version;
        switch (CommandVorpruefung.Prüfe(true, _verarbeiteteCommandIds.Contains(env.CommandId), _abgelehnteCommandIds.Contains(env.CommandId),
                    _state!.Version, expectedVersion))
        {
            case CommandVorpruefung.Ergebnis.IdempotenterErfolg:
                e.Sofort = new CommandResult { Success = true, NewVersion = _state.Version };
                return;
            case CommandVorpruefung.Ergebnis.IdempotenteAblehnung:
                e.Sofort = new CommandResult
                {
                    Success = false, Abgelehnt = true, ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)", NewVersion = _state.Version,
                };
                return;
            case CommandVorpruefung.Ergebnis.VersionsKonflikt:
                if (await NachgeschlagenAsync(env.CommandId, wiederholung: true) is { } schon)
                {
                    e.Sofort = schon == AppendErgebnis.SchonAbgelehnt
                        ? new CommandResult { Success = false, Abgelehnt = true, ErrorMessage = "Vorgang bereits abgelehnt (Framework-Inbox)", NewVersion = _state.Version }
                        : new CommandResult { Success = true, NewVersion = _state.Version };
                    return;
                }
                e.Fehler = $"Concurrency conflict: expected version {expectedVersion}, actual {_state.Version}";
                e.Sofort = new CommandResult { Success = false, ErrorMessage = e.Fehler, NewVersion = _state.Version };
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
            e.Sofort = new CommandResult { Success = false, ErrorMessage = ex.Message };
            return;
        }
        var persistent = alle.Where(x => x is not ITransientEvent).ToList();
        var ablehnungen = alle.Where(x => x is ITransientEvent).ToList();

        if (ablehnungen.Count > 0 && (persistent.Count > 0 || ablehnungen.Count > 1))
        {
            e.Fehler = $"Decider für {env.Payload.GetType().Name} lieferte ein ungültiges gemischtes Ergebnis: {persistent.Count} persistente(s) " +
                       $"Event(s) + {ablehnungen.Count} Ablehnung(en). Eine Ablehnung (ITransientEvent) muss das einzige zurückgegebene Event sein.";
            e.Sofort = new CommandResult { Success = false, ErrorMessage = e.Fehler };
            return;
        }

        IReadOnlyList<IEvent> schreibe = Array.Empty<IEvent>();
        IReadOnlyList<Beilage> fristen = Array.Empty<Beilage>();
        e.VersionVorher = _state.Version;
        if (alle.Count == 0)
        {
            // Noop: nur die Quittung (gewirkt), die Version bleibt.
            e.Ausgang = KommandoAusgang.Gewirkt;
        }
        else if (ablehnungen.Count == 1)
        {
            // Ablehnung: nur die Quittung (abgelehnt), die Version bleibt; der Hinweis geht nach dem Commit an Sitzung und Hörer.
            e.Ausgang = KommandoAusgang.Abgelehnt;
            e.Ablehnung = ablehnungen[0];
        }
        else
        {
            try { fristen = Fristen(persistent, _state.Version, env.UserId); }
            catch (Exception ex)
            {
                e.Fehler = ex.Message;
                e.Sofort = new CommandResult { Success = false, ErrorMessage = ex.Message };
                return;
            }
            e.Ausgang = KommandoAusgang.Gewirkt;
            e.Persistent.AddRange(persistent);
            schreibe = persistent;
            foreach (var evt in persistent)
            {
                if (evt is not IRahmenEvent) _handler.ApplyEvent(evt);
                _state.Version++;
            }
        }
        e.VersionNachher = _state.Version;
        e.Teil = new AppendTeil(schreibe, env.CorrelationId, env.CommandId.ToString(), env.UserId, e.Spur,
            [new Quittung(env.CommandId, e.Ausgang), .. fristen]);
    }

    /// <summary>
    /// Nach dem Commit des Bündels (oder ohne Schreiben): Cache, Pushes an Clients, Ablehnungen an die Sitzung, Antworten — in der Reihenfolge der Commands.
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
                    if (e.Fehler is { } fehler) MeldeCommandFailed(e.Env, fehler);
                    e.Ergebnis = sofort;
                }
                else if (e.Ausgang == KommandoAusgang.Abgelehnt)
                {
                    geschrieben = true;
                    _abgelehnteCommandIds.Add(e.Env.CommandId);
                    // Spur: eine Ablehnung als verlierbarer Hinweis an die Werkbank.
                    Infrastructure.Einblick.SpurHinweise.Melde(Infrastructure.Einblick.SpurHinweise.Ablehnung, e.Spur?.Wirker, e.Env.Ursache,
                        e.Env.CorrelationId, $"{e.Env.Payload.GetType().Name} abgelehnt: {e.Ablehnung!.GetType().Name}");
                    MeldeAblehnung(e.Env, e.Ablehnung);
                    e.Ergebnis = new CommandResult
                    {
                        Success = false, Abgelehnt = true, RejectionEvent = e.Ablehnung, ErrorMessage = e.Ablehnung.ToString(),
                        NewVersion = e.VersionNachher,
                    };
                }
                else
                {
                    geschrieben = true;
                    _verarbeiteteCommandIds.Add(e.Env.CommandId);
                    if (e.Persistent.Count > 0 && _verteilung != null)
                    {
                        try { await PublishEventsAsync(e.Persistent, e.Env, e.VersionVorher); }
                        catch (Exception ex) { _logger?.LogWarning(ex, "[Actor] Pushes nach dem Commit nicht veröffentlicht"); }
                    }
                    e.Ergebnis = new CommandResult
                    {
                        Success = true, NewVersion = e.VersionNachher, ErsteVersion = e.Persistent.Count > 0 ? e.VersionVorher + 1 : 0,
                    };
                }
            }
            if (e.An is { } an) context.Send(an, e.Ergebnis!);
            e.Beantwortet = true;
        }
        if (geschrieben)
        {
            TrySnapshotOnThreshold(context);
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
                if (!_initialized) await InitializeAsync();
                using (SpurVon(env))
                    await HandleCommandAsync(context, env);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Actor] Einzel-Pfad für {Command} gescheitert", env.Payload.GetType().Name);
                MeldeCommandFailed(env, ex.Message);
                Antworte(context, new CommandResult { Success = false, ErrorMessage = ex.Message });
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
        => HandleCommandCoreAsync(context, cmdEnvelope, expectedVersion);

    /// <summary>Interner Emitter (Funktion, Uhr): KEINE Version — Idempotenz via det. CommandId + Inbox.</summary>
    private Task HandleEmittedCommand(IContext context, CommandEnvelope cmdEnvelope)
        => HandleCommandCoreAsync(context, cmdEnvelope, expectedVersion: _state!.Version);

    private async Task HandleCommandCoreAsync(IContext context, CommandEnvelope cmdEnvelope, int expectedVersion)
    {
        try
        {
            _logger?.LogDebug("[Actor] Processing {Command}", cmdEnvelope.Payload.GetType().Name);

            // ★ Vorab-Entscheidung (docs/08 §8.4) — Inbox-Dedup VOR der OCC-Prüfung (Reihenfolge in der reinen,
            //   Ebene-1-getesteten CommandVorpruefung.Prüfe gekapselt). Ein wiederholt zugestellter Command
            //   (at-least-once — Emittiert IMMER, Client bei Retry nach verlorenem Ack) liefert
            //   idempotent DASSELBE Ergebnis; ein frischer Command fällt durch zur OCC-Assertion.
            switch (CommandVorpruefung.Prüfe(
                        true,
                        _verarbeiteteCommandIds.Contains(cmdEnvelope.CommandId),
                        _abgelehnteCommandIds.Contains(cmdEnvelope.CommandId),
                        _state!.Version,
                        expectedVersion))
            {
                case CommandVorpruefung.Ergebnis.IdempotenterErfolg:
                    Antworte(context, new CommandResult { Success = true, NewVersion = _state.Version });
                    return;

                case CommandVorpruefung.Ergebnis.IdempotenteAblehnung:
                    Antworte(context, new CommandResult
                    {
                        Success = false,
                        Abgelehnt = true,
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
                    MeldeCommandFailed(cmdEnvelope, errorMsg);
                    Antworte(context, new CommandResult
                    {
                        Success = false,
                        ErrorMessage = errorMsg,
                        NewVersion = _state.Version  // Pipeline braucht die aktuelle Version für Retry
                    });
                    return;

                case CommandVorpruefung.Ergebnis.Fortfahren:
                    break;
            }

            // 1. Events erzeugen (Decider) — wirft keine Exceptions mehr für Domänen-Ablehnungen
            var allEvents = _handler!.HandleCommand(cmdEnvelope.Payload).ToList();

            // 2. Noop — keine Events (z.B. DeaktiviereLagerartikel bei bereits inaktivem Artikel): nur die Quittung, die Version bleibt. Die Inbox
            //    kennt die CommandId, eine Wiederholung bleibt ein Noop.
            if (!allEvents.Any())
            {
                if (await QuittiereOhneEventsAsync(cmdEnvelope, expectedVersion, KommandoAusgang.Gewirkt) is var noop and not AppendErgebnis.Geschrieben)
                {
                    QuittiereEntschieden(context, noop);
                    return;
                }

                Antworte(context, new CommandResult
                {
                    Success = true,
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
            //    garantiert: hier steht exakt eine Ablehnung). Kein Event, kein Apply, keine Version-Änderung — nur die Quittung (abgelehnt):
            //    eine Wiederholung derselben CommandId bekommt dieselbe Ablehnung (Zwei-Mengen-Inbox), der Sender sieht „abgelehnt“ als
            //    entschieden. Ist die Ablehnung für einen Ablauf wichtig, ist sie ein Fakt (Event) — das entscheidet der Modellierer über den Typ.
            if (rejections.Any() && !persistentEvents.Any())
            {
                var rejection = rejections.First();
                _logger?.LogDebug("[Actor] Rejected: {Rejection}", rejection.GetType().Name);

                if (await QuittiereOhneEventsAsync(cmdEnvelope, expectedVersion, KommandoAusgang.Abgelehnt) is var schon
                    and not AppendErgebnis.Geschrieben)
                {
                    QuittiereEntschieden(context, schon);
                    return;
                }

                // Spur: eine Ablehnung steht nicht im Log — als verlierbarer Hinweis an die Werkbank.
                Infrastructure.Einblick.SpurHinweise.Melde(Infrastructure.Einblick.SpurHinweise.Ablehnung, Spur.Aktuell?.Wirker, cmdEnvelope.Ursache,
                    cmdEnvelope.CorrelationId, $"{cmdEnvelope.Payload.GetType().Name} abgelehnt: {rejection.GetType().Name}");
                // Der Hinweis an die Sitzung des Aufrufers und an die Funktionen, die ihn hören (verlierbar).
                MeldeAblehnung(cmdEnvelope, rejection);

                Antworte(context, new CommandResult
                {
                    Success = false,
                    Abgelehnt = true,
                    RejectionEvent = rejection,
                    ErrorMessage = rejection.ToString(),
                    NewVersion = _state.Version,
                });
                return;
            }

            // 5. Erfolg — persistente Events verarbeiten.
            //    ★ Phase 1: Metadaten mit ins Log stempeln (nach Log-Read verfügbar).
            //    ★ Inbox: die Quittung reist in DERSELBEN Transaktion wie der Effekt (Inbox-Tabelle) → exactly-once-wirksam, crash-fest.
            var append = await AppendeAsync(cmdEnvelope, expectedVersion, persistentEvents, KommandoAusgang.Gewirkt,
                Fristen(persistentEvents, expectedVersion, cmdEnvelope.UserId));
            if (append != AppendErgebnis.Geschrieben)
            {
                // Mehrdeutiger Commit (G-3): die Events stehen im Log, der Actor hat sie beim Rehydrieren schon gefaltet. Die Pushes an
                //   Clients gehen trotzdem hinaus (verlierbar); geweckt hat der Wecker nicht (sein Commit galt als gescheitert) — die Konsumenten
                //   holt der Takt. Quittiert wird der durable Ausgang.
                if (append == AppendErgebnis.SchonGewirkt && _verteilung != null)
                {
                    try { await PublishEventsAsync(persistentEvents, cmdEnvelope, expectedVersion); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "[Actor] Pushes nach mehrdeutigem Commit nicht veröffentlicht"); }
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
                // ★ Audit-Fix #5 (Symmetrie Live == Rehydration): ein IRahmenEvent-Persistent-Event wird
                //   NICHT angewandt (nur die Version mitgezählt) — exakt wie die Rehydration (AggregateRehydrator).
                foreach (var evt in persistentEvents)
                {
                    if (evt is not IRahmenEvent) _handler.ApplyEvent(evt);
                    _state.Version++;
                }
                _verarbeiteteCommandIds.Add(cmdEnvelope.CommandId);


                // 7b. ★ Snapshot bei Schwellwert (best-effort, out-of-band): verkürzt künftige Rehydration.
                TrySnapshotOnThreshold(context);

                // 8. Events an die Verteiler (Pushes an die Clients, die sie hören)
                if (_verteilung != null)
                {
                    await PublishEventsAsync(persistentEvents, cmdEnvelope, expectedVersion);
                }

                _logger?.LogDebug("[Actor] ✔ Processed. New version: {Version}", _state.Version);

                // 9. WICHTIG: Erfolg antworten mit NewVersion!
                Antworte(context, new CommandResult
                {
                    Success = true,
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
                try { await InitializeAsync(); }
                catch (Exception rehydrateEx)
                {
                    // Rehydration schlug fehl → _initialized bleibt false → die nächste Nachricht initialisiert sauber neu.
                    _logger?.LogError(rehydrateEx,
                        "[Actor] Re-Rehydration nach Post-Append-Fehler fehlgeschlagen — die nächste Nachricht initialisiert neu.");
                }

                Antworte(context, new CommandResult
                {
                    Success = true,
                    NewVersion = _state?.Version ?? (expectedVersion + persistentEvents.Count)
                });
            }
        }
        catch (Exception ex) when (ex is not AppendGescheitertException)
        {
            // Catch-Block ist jetzt nur noch für technische Fehler
            // (DB-Timeout, Null-Referenz, etc. — nicht für Domänen-Ablehnungen)
            _logger?.LogError(ex, "[Actor] Technical error: {Message}", ex.Message);

            MeldeCommandFailed(cmdEnvelope, ex.Message);
            
            Antworte(context, new CommandResult 
            { 
                Success = false, 
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Ein Noop oder eine Ablehnung: nur die Quittung des Commands (Inbox-Tabelle), ohne Event — die Version des Streams bleibt. Gültig nur, wenn
    /// der Stream noch auf <paramref name="expectedVersion"/> steht (Kopf-Prüfung in derselben Transaktion): eine zweite Aktivierung auf einem
    /// veralteten Stand hätte sonst dauerhaft abgelehnt oder nichts getan. Basis ist die aufgelöste aktuelle Version (Single-Writer).
    /// </summary>
    private async Task<AppendErgebnis> QuittiereOhneEventsAsync(CommandEnvelope cmdEnvelope, int expectedVersion, KommandoAusgang ausgang)
    {
        var ergebnis = await AppendeAsync(cmdEnvelope, expectedVersion, Array.Empty<IEvent>(), ausgang);
        if (ergebnis != AppendErgebnis.Geschrieben) return ergebnis;   // schon im frisch rehydrierten Stand

        (ausgang == KommandoAusgang.Abgelehnt ? _abgelehnteCommandIds : _verarbeiteteCommandIds).Add(cmdEnvelope.CommandId);
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

    // ─── Snapshot (docs/04-konsum-und-prozess-maschine.md §4.8): im Turn einfrieren, neben der Mailbox schreiben ───

    /// <summary>Wie lange das Schreiben des Snapshots beim Stoppen höchstens dauern darf.</summary>
    private static readonly TimeSpan SnapshotBeimStoppen = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Schreibt einen Snapshot, wenn seit dem letzten mindestens <c>_snapshotThreshold</c> Events kamen: neben der Mailbox
    /// (<see cref="RahmenActor.Neben"/>), der Command-Turn wartet nicht darauf. Best-effort, außerhalb der Command-Transaktion
    /// (Invariante 6); ein Fehler ist folgenlos, die nächste Rehydration heilt.
    /// </summary>
    private void TrySnapshotOnThreshold(IContext context)
    {
        if (_snapshots is null || _snapshotThreshold <= 0 || _state is null) return;
        if (_state.Version - _lastSnapshotVersion < _snapshotThreshold) return;
        var snapshot = Einfrieren();
        Neben(context, ct => _snapshots.SaveAsync(snapshot, ct), t =>
        {
            if (t.Exception is { } ex) SnapshotGescheitert(ex, snapshot.Version);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Schreibt einen Snapshot bei Deaktivierung, wenn seit dem letzten neue Events kamen — abgewartet, mit Frist
    /// (<see cref="SnapshotBeimStoppen"/>): nach dem Stopp kehrte keine Arbeit neben der Mailbox mehr zurück. Best-effort.
    /// </summary>
    private async Task SnapshotBeimStoppenAsync()
    {
        if (_snapshots is null || _state is null) return;
        if (_state.Version <= _lastSnapshotVersion) return;
        var snapshot = Einfrieren();
        try
        {
            using var frist = new CancellationTokenSource(SnapshotBeimStoppen);
            await _snapshots.SaveAsync(snapshot, frist.Token);
        }
        catch (Exception ex) { SnapshotGescheitert(ex, snapshot.Version); }
    }

    /// <summary>
    /// Friert den State (Deep-Copy) IM TURN ein — der spätere Write kann das weiter mutierende <c>_state</c> nicht mehr korrumpieren
    /// (Single-Writer-Turn, Capture-Race-Probe).
    /// </summary>
    private Snapshot<TState> Einfrieren()
    {
        _lastSnapshotVersion = _state!.Version;
        return new Snapshot<TState>
        {
            Id = _id,
            Version = _state.Version,
            SchemaVersion = _snapshotSchemaVersion ?? "",
            State = Clone(_state),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SnapshotGescheitert(Exception ex, int version) =>
        _logger?.LogWarning(ex, "Snapshot für {Aggregate}/{Id} v{Version} fehlgeschlagen — folgenlos, die nächste Rehydration heilt.",
            typeof(TState).Name, _id, version);

    /// <summary>Deep-Copy des States per JSON-Round-Trip (Bibliotheks-Serializer, kein Eigen-Reflection).</summary>
    private static TState Clone(TState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        return System.Text.Json.JsonSerializer.Deserialize<TState>(json)!;
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
            // Der Actor-State steht hier bereits auf dem Kopf NACH dem Commit — genau die ExpectedVersion, die der nächste
            // Client-Command tragen muss (auch wenn der Client das letzte Event des Commits nicht hört).
            streamHeadVersion: _state.Version,
            spur: Spur.Aktuell);

        // ★ Perf: Publish ist reine Benachrichtigung — der Command ist bereits im Log committet (Schritt 5). Zustellung ist best-effort
        //   (Invariante 2); die durablen Konsumenten heilen über Log-Read + Takt. Darum NICHT im Single-Writer-Turn auf die Verteiler warten.
        //   Dieselbe Meldung wie jeder andere Schreiber (Quelle, festgehaltene Ausgabe einer Funktion): der Envelope je Event an die Clients;
        //   die Konsumenten weckt der Wecker am Commit.
        Infrastructure.Verteilen.Commitmeldung.Melde(_verteilung!, envelopes);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Meldet ein Ablehnungs-Event gezielt an die Sitzung des Aufrufers (verlierbar, Invariante 6; der Turn wartet nicht).
    /// Verwendet OriginSessionId für die direkte Zustellung. Das Ablehnungs-Event ist das typisierte Domänen-Event (z.B. BestandNichtAusreichend).
    /// </summary>
    private void MeldeAblehnung(CommandEnvelope cmdEnvelope, IEvent rejection)
    {
        // An die Funktionen, die den Hinweis hören (Hinweis-Netz, Plan §2.9): Kennung aus der Command-Id (A7), Herkunft der eigene Stream.
        if (_hinweisNetz is { } netz && netz.Hoert(rejection.GetType()))
        {
            var version = _state?.Version ?? 0;
            netz.Verteile(new Infrastructure.Funktionen.HinweisZustellen(Infrastructure.Funktionen.FunktionsIds.HinweisAusCommand(cmdEnvelope.CommandId),
                rejection, _id, version, DateTimeOffset.UtcNow, cmdEnvelope.CorrelationId, ImAuftrag.IstAkteur(cmdEnvelope.UserId) ? cmdEnvelope.UserId : null,
                Spur.Ursache(_id, version)));
        }

        if (_verteilung == null || string.IsNullOrEmpty(cmdEnvelope.OriginSessionId))
        {
            _logger?.LogDebug("[Actor] Cannot publish rejection: keine Verteilung oder keine Sitzung");
            return;
        }

        _verteilung.Melde(new EventEnvelope
        {
            AggregateId = _id,
            AggregateVersion = _state?.Version ?? 0,
            AggregateType = typeof(TState).Name,
            CorrelationId = cmdEnvelope.CorrelationId,
            CausationId = cmdEnvelope.CommandId.ToString(),
            UserId = cmdEnvelope.UserId,
            Payload = rejection,
            ZielSitzung = cmdEnvelope.OriginSessionId
        });
        _logger?.LogDebug("[Actor] Rejection {Rejection} an '{Session}' gemeldet", rejection.GetType().Name, cmdEnvelope.OriginSessionId);
    }

    /// <summary>
    /// Meldet ein CommandFailed Event für technische Fehler (verlierbar, gezielt an die Sitzung der OriginSessionId).
    /// Nur für technische Fehler — Domänen-Ablehnungen nutzen <see cref="MeldeAblehnung"/>.
    /// </summary>
    private void MeldeCommandFailed(CommandEnvelope cmdEnvelope, string reason)
    {
        if (_verteilung == null || string.IsNullOrEmpty(cmdEnvelope.OriginSessionId))
        {
            _logger?.LogDebug("[Actor] Cannot publish CommandFailed: keine Verteilung oder keine Sitzung");
            return;
        }

        _verteilung.Melde(new EventEnvelope
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
            ZielSitzung = cmdEnvelope.OriginSessionId
        });
        _logger?.LogDebug("[Actor] CommandFailed an '{Session}' gemeldet", cmdEnvelope.OriginSessionId);
    }
}