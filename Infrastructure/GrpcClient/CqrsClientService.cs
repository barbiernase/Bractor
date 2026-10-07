// REPO-PFAD: Infrastructure/GrpcClient/CqrsClientService.cs  (MODIFIZIERT)
using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Akteure;
using Domain.Projections;
using Grpc.Core;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.Mapping;
using Infrastructure.Projections;   // WakeAck
using Infrastructure.Pipeline;
using Infrastructure.PubSub;
using Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto;
using Proto.Cluster;

namespace Infrastructure.GrpcClient;

/// <summary>
/// gRPC Service für bidirektionale Client-Kommunikation.
/// 
/// DESIGN:
/// - Read-Loop direkt im Service (nutzt Kestrel's Thread-Pool)
/// - Nur EIN Actor pro Verbindung: EventProxyActor (existiert nur für PID)
/// - SubscriptionTracker als normales Objekt (kein Actor)
/// - Cleanup im finally-Block (deterministisch, kein Actor-Messaging)
/// 
/// Lifecycle einer Verbindung:
/// 1. Client ruft Connect() auf
/// 2. Service spawnt EventProxyActor (für PID)
/// 3. Service erstellt SubscriptionTracker
/// 4. Service registriert in TriggerHandlerRegistry / QueryHandlerRegistry  (NEU)
/// 5. Read-Loop verarbeitet ClientMessages
/// 6. finally-Block: Subscriptions beenden, Registrierungen entfernen, Actor stoppen
/// </summary>
public class CqrsClientServiceImpl : ProtoRepo.CqrsClientService.CqrsClientServiceBase
{
    private readonly ActorSystem _actorSystem;
    private readonly ProtoMessageMapper _mapper;
    private readonly IAggregateDispatcher _dispatcher;
    private readonly CapabilitiesHandler _capabilitiesHandler;
    private readonly ProjectionQueryService _queryService;
    private readonly TriggerHandlerRegistry _triggerHandlerRegistry;
    private readonly QueryHandlerRegistry _queryHandlerRegistry;
    private readonly BrokerPublisher _publisher;
    private readonly AkteurTor? _akteurTor;
    private readonly FunktionsAusfuehrer? _funktionsAusfuehrer;
    private readonly ILogger _logger;

    /// <summary>
    /// Timeout für Query-Forwarding an Clients.
    /// Konfigurierbar, aber mit sensiblem Default für ML-Inference.
    /// </summary>
    private static readonly TimeSpan QueryForwardTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TriggerForwardTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Pending Query-Forwards über alle Verbindungen hinweg.
    /// Key: CorrelationId, Value: TaskCompletionSource für die Antwort.
    /// 
    /// Cross-Connection: HandleQueryAsync (Session A) registriert TCS,
    /// HandleQueryAnswer (Session B des Handler-Clients) vervollständigt ihn.
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoRepo.QueryResponseFromClient>>
        _pendingQueryForwards = new();

    /// <summary>
    /// Pending Trigger-Forwards über alle Verbindungen hinweg.
    /// Selbes Pattern wie Query-Forwards.
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoRepo.TriggerResult>>
        _pendingTriggerForwards = new();
    
    private static int _sessionCounter = 0;

    /// <summary>
    /// Intervall, in dem eine offene Session ihre PubSub-Subscriptions ERNEUT an die Shards sendet
    /// (Selbst-Heilung gegen Shard-Rebalance — das Poll-Äquivalent für den Client-Targeted-Pfad).
    /// In Anlehnung an das Projektions-Poll-Intervall (30 s), etwas kürzer für schnellere Erholung.
    /// </summary>
    private static readonly TimeSpan SubscriptionReassertInterval = TimeSpan.FromSeconds(20);

    public CqrsClientServiceImpl(
        ActorSystem actorSystem,
        ProtoMessageMapper mapper,
        IAggregateDispatcher dispatcher,
        ProjectionQueryService queryService,
        TriggerHandlerRegistry triggerHandlerRegistry,
        QueryHandlerRegistry queryHandlerRegistry,
        BrokerPublisher publisher,
        AkteurTor? akteurTor = null,
        ILogger<CqrsClientServiceImpl>? logger = null,
        FunktionsAusfuehrer? funktionsAusfuehrer = null)
    {
        _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _triggerHandlerRegistry = triggerHandlerRegistry ?? throw new ArgumentNullException(nameof(triggerHandlerRegistry));
        _queryHandlerRegistry = queryHandlerRegistry ?? throw new ArgumentNullException(nameof(queryHandlerRegistry));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _akteurTor = akteurTor; // null = Akteure nicht konfiguriert → Pfad offen wie bisher (opt-in)
        _funktionsAusfuehrer = funktionsAusfuehrer; // null = keine Prozess-/Funktions-Maschinerie → Anbieten wird abgewiesen
        _logger = logger ?? NullLogger<CqrsClientServiceImpl>.Instance;
        _capabilitiesHandler = new CapabilitiesHandler();
    }

    public override async Task Connect(
        IAsyncStreamReader<ProtoRepo.ClientMessage> requestStream,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        ServerCallContext context)
    {
        var sessionId = $"session-{Interlocked.Increment(ref _sessionCounter):D4}";
        var ct = context.CancellationToken;
        
        _logger.LogInformation("New connection {Session} from {Peer}", sessionId, context.Peer);

        // Akteur (docs/konzept-akteure.md): wer sich anmeldet, ist EIN Akteur; seine Befugnisse gelten für die
        // ganze Session. Ohne konfiguriertes Tor bleibt akteur = null (alles erlaubt, wie bisher).
        AkteurRechte? akteur = null;
        if (_akteurTor != null)
        {
            akteur = _akteurTor.Erkenne(context.RequestHeaders.GetValue(AkteurOptionen.TokenHeader));
            if (akteur == null)
            {
                _logger.LogWarning("{Session} abgewiesen: kein gültiges Akteur-Token ({Peer})", sessionId, context.Peer);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Kein gültiges Akteur-Token"));
            }
            _logger.LogInformation("{Session} Akteur: {Akteur}", sessionId, akteur.Name);
        }

        PID? proxyPid = null;
        FunktionsAnbieterSitzung? anbieter = null;
        // Wer diese Session ist: vom Tor (Token), ggf. erst am Handshake vom Akteur-Vertrag festgelegt (Akteur-Konzept §5.2).
        var sitzung = new AkteurSitzung(akteur);

        try
        {
            // 1. EventProxyActor spawnen (für PID)
            var proxyProps = Props.FromProducer(() =>
                new EventProxyActor(responseStream, _mapper, sessionId, _logger));
            proxyPid = _actorSystem.Root.Spawn(proxyProps);

            _logger.LogDebug("{Session} EventProxy spawned: {Pid}", sessionId, proxyPid);

            // 1b. Funktions-Anbieter (Katalog-Funktionen extern, §14.5): Hol-Schleifen starten erst, wenn der Worker anbietet.
            if (_funktionsAusfuehrer != null)
                anbieter = BaueAnbieterSitzung(sessionId, proxyPid, _funktionsAusfuehrer);

            // 2. SubscriptionTracker erstellen (await using = automatisches Cleanup)
            await using var subscriptionTracker = new SubscriptionTracker(
                _actorSystem.Cluster(),
                proxyPid,
                sessionId,
                _logger);

            // 2b. Re-Assert-Loop: hält die Subscriptions dieser Session am Leben (Selbst-Heilung gegen
            //     Shard-Rebalance). Läuft parallel zum Read-Loop, gekoppelt an ct; wird VOR dem
            //     Tracker-Dispose (await using) gestoppt.
            using var reassertCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var reassertLoop = SubscriptionReassertLoopAsync(subscriptionTracker, sessionId, reassertCts.Token);

            // 3. Read-Loop
            _logger.LogDebug("{Session} entering read loop", sessionId);

            try
            {
                while (await requestStream.MoveNext(ct))
                {
                    var clientMessage = requestStream.Current;
                    await ProcessMessageAsync(
                        clientMessage, responseStream, subscriptionTracker,
                        proxyPid, sitzung, anbieter,
                        sessionId, ct);
                }

                _logger.LogInformation("{Session} client closed stream normally", sessionId);
            }
            finally
            {
                reassertCts.Cancel();
                try { await reassertLoop; } catch { /* Loop-Ende ist erwartbar */ }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{Session} cancelled", sessionId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("{Session} client disconnected", sessionId);
        }
        catch (RpcException ex)
        {
            // Bewusst beendet (z. B. Akteur-Vertrag abgelehnt): der Client bekommt den Status, nicht ein stilles Ende.
            _logger.LogWarning("{Session} beendet: {Status} {Detail}", sessionId, ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Session} error", sessionId);
        }
        finally
        {
            // 4. Cleanup: Registrierungen entfernen, pending Forwards abbrechen, Actor stoppen
            // SubscriptionTracker.DisposeAsync() wird automatisch aufgerufen (await using)

            // Funktions-Anbieter: Hol-Schleifen beenden; Laufendes nicht erledigt melden — die Leases laufen ab, der
            // Vermittler gibt die Aufträge neu aus (anderer Worker, oder dieser nach Reconnect).
            if (anbieter != null)
            {
                try { await anbieter.DisposeAsync(); }
                catch (Exception ex) { _logger.LogDebug(ex, "{Session} Funktions-Anbieter: Ende mit Fehler", sessionId); }
            }

            if (proxyPid != null)
            {
                // NEU: Registrierungen entfernen
                _triggerHandlerRegistry.UnregisterAll(proxyPid);
                _queryHandlerRegistry.UnregisterAll(proxyPid);

                try
                {
                    await _actorSystem.Root.StopAsync(proxyPid);
                    _logger.LogDebug("{Session} EventProxy stopped", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Session} error stopping proxy", sessionId);
                }
            }

            _logger.LogInformation("{Session} disconnected", sessionId);
        }
    }

    /// <summary>
    /// Erneuert periodisch die Subscriptions der Session an den Broker-Shards, bis die Verbindung endet
    /// (<paramref name="ct"/> gecancelt). Selbst-Heilung gegen Shard-Rebalance — das Poll-Äquivalent für
    /// den Client-Targeted-Pfad. Fehler sind folgenlos (der nächste Durchlauf heilt).
    /// </summary>
    private async Task SubscriptionReassertLoopAsync(SubscriptionTracker tracker, string sessionId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(SubscriptionReassertInterval, ct);
                await tracker.ReassertAllAsync(ct);
                _logger.LogTrace("{Session} Subscriptions erneut angemeldet (Re-Assert)", sessionId);
            }
        }
        catch (OperationCanceledException)
        {
            // Verbindung endet — normal.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Session} Re-Assert-Loop unerwartet beendet", sessionId);
        }
    }

    // =========================================================================
    // MESSAGE PROCESSING
    // =========================================================================

    private async Task ProcessMessageAsync(
        ProtoRepo.ClientMessage message,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        PID proxyPid,
        AkteurSitzung sitzung,
        FunktionsAnbieterSitzung? anbieter,
        string sessionId,
        CancellationToken ct)
    {
        var akteur = sitzung.Akteur;
        try
        {
            switch (message.MessageCase)
            {
                case ProtoRepo.ClientMessage.MessageOneofCase.Command:
                    await HandleCommandAsync(message.Command, responseStream, sitzung, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Subscribe:
                    await HandleSubscribeAsync(message.Subscribe, responseStream, subscriptionTracker, akteur, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Unsubscribe:
                    await HandleUnsubscribeAsync(message.Unsubscribe, responseStream, subscriptionTracker, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Capabilities:
                    await HandleCapabilitiesAsync(message.Capabilities, responseStream, subscriptionTracker, proxyPid, sitzung, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Query:
                    await HandleQueryAsync(message.Query, responseStream, proxyPid, akteur, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Trigger:
                    await HandleTriggerAsync(message.Trigger, responseStream, proxyPid, akteur, sessionId, ct);
                    break;

                // ═════════════════════════════════════════
                // NEU: First-Citizen Messages
                // ═════════════════════════════════════════

                case ProtoRepo.ClientMessage.MessageOneofCase.TransientEvent:
                    await HandleTransientEventAsync(message.TransientEvent, responseStream, akteur, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.QueryAnswer:
                    HandleQueryAnswer(message.QueryAnswer, sessionId);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.TriggerResult:
                    HandleTriggerResult(message.TriggerResult, sessionId);
                    break;

                // ═════════════════════════════════════════
                // Katalog-Funktionen extern (§14.5)
                // ═════════════════════════════════════════

                case ProtoRepo.ClientMessage.MessageOneofCase.FunktionenAnbieten:
                    await HandleFunktionenAnbietenAsync(message.FunktionenAnbieten, responseStream, anbieter, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.ArbeitsErgebnis:
                    await HandleArbeitsErgebnisAsync(message.ArbeitsErgebnis, responseStream, anbieter, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.ArbeitLebt:
                    if (anbieter != null && Guid.TryParse(message.ArbeitLebt.Vorgang, out var lebt))
                        await anbieter.LebtAsync(lebt);
                    break;

                default:
                    _logger.LogWarning("{Session} unknown message type: {MessageCase}", sessionId, message.MessageCase);
                    break;
            }
        }
        catch (RpcException)
        {
            throw;   // Session bewusst beenden (Status an den Client), siehe Connect
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Session} error processing message", sessionId);
            await SendErrorAsync(responseStream, "PROCESSING_ERROR", ex.Message, "", ct);
        }
    }

    // =========================================================================
    // CAPABILITIES HANDLING
    // =========================================================================

    private async Task HandleCapabilitiesAsync(
        ProtoRepo.CapabilitiesRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        PID proxyPid,
        AkteurSitzung sitzung,
        string sessionId,
        CancellationToken ct)
    {
        // 0. Akteur-Vertrag (docs/konzept-akteure.md §5.2): „ich bin Vertrag X" — geprüft gegen die generierte Tabelle (mit Tor:
        //    das Token muss ihn verkörpern; abweichender Hash = Warnung bzw. im strengen Modus Ablehnung). Ohne Tor sagt der Vertrag,
        //    wer da ist: die Session bekommt seine Befugnisse.
        var vp = AkteurVertragsPruefung.Pruefe(request.Vertrag, request.VertragHash, sitzung.Akteur, GeneratedAkteurRechte.Alle,
            _akteurTor?.VertragStreng ?? false, GeneratedClientVertraege.Alle);
        if (vp.Ablehnung is { } ablehnung)
        {
            _logger.LogWarning("{Session} Vertrag {Vertrag} abgelehnt: {Grund}", sessionId, request.Vertrag, ablehnung);
            await SendErrorAsync(responseStream, "VERTRAG_ABGELEHNT", ablehnung, "", ct);
            throw new RpcException(new Status(StatusCode.PermissionDenied, ablehnung));
        }
        if (vp.Warnung is { } warnung)
            _logger.LogWarning("{Session} Vertrag {Vertrag}: {Warnung}", sessionId, request.Vertrag, warnung);
        if (vp.Vertrag is { } vertrag)
        {
            sitzung.Vertrag = vertrag;
            // Client-Vertrag: die Schnittmenge Vertrag ∩ Token IST die Befugnis der Session (docs/konzept-akteure.md §4.3).
            if (vp.Client != null) sitzung.Akteur = vertrag;
            else sitzung.Akteur ??= vertrag;
            _logger.LogInformation("{Session} Vertrag {Vertrag} ({Typ}) angenommen{Akteure}", sessionId, vertrag.Name, vertrag.VertragTyp?.Name,
                vp.Client != null ? $" — verkörpert {string.Join(", ", vertrag.Teile.Select(t => t.Name))}" : "");
        }
        var akteur = sitzung.Akteur;

        var messageSource = request.MessageTypes.Any()
            ? $"message_types: [{string.Join(", ", request.MessageTypes)}]"
            : $"event_types: [{string.Join(", ", request.EventTypes)}]";
        _logger.LogDebug("{Session} ← Capabilities: {Source}", sessionId, messageSource);

        if (request.HandleTriggers.Any())
            _logger.LogDebug("{Session}   handle_triggers: [{Triggers}]", sessionId, string.Join(", ", request.HandleTriggers));
        if (request.HandleQueries.Any())
            _logger.LogDebug("{Session}   handle_queries: [{Queries}]", sessionId, string.Join(", ", request.HandleQueries));

        try
        {
            // 1. Capabilities ermitteln (universell)
            var result = _capabilitiesHandler.Handle(request, sessionId);

            // 1b. Akteur: Befugnis statt Selbstauskunft — erlaubte Mengen aus IDarf (∪ Vertrags-Ausgaben), Hören abgeleitet,
            //     Zuständigkeit nur für Lücken (das System bedient den Typ nicht selbst) und nur einmal. Mit Vertrag: abonniert wird
            //     genau, worauf er reagiert.
            Func<string, Type?> loese = n => MessageTypeMapping.Resolve(n).Type;
            Func<Type, bool> internBedient = t => ProjectionQueryService.SupportedQueryTypes.Contains(t)
                                                  || GeneratedPipelines.TriggerToPipelineId.ContainsKey(t);
            Func<string, bool> schonVergeben = n => _queryHandlerRegistry.GetHandler(n) is { } q && !q.Equals(proxyPid)
                                                    || _triggerHandlerRegistry.GetHandler(n) is { } t && !t.Equals(proxyPid);
            if (sitzung.Vertrag is { } v)
            {
                var abweichung = AkteurVertragsPruefung.Wende(result, v, loese, internBedient, schonVergeben);
                if (abweichung.Count > 0)
                    _logger.LogInformation("{Session} Vertrag {Vertrag} statt Selbstauskunft: [{Abweichung}]",
                        sessionId, v.Name, string.Join(", ", abweichung));
            }
            else if (akteur != null)
            {
                var verweigert = AkteurTor.Wende(result, akteur, loese, internBedient, schonVergeben);
                if (verweigert.Count > 0)
                    _logger.LogWarning("{Session} Akteur {Akteur} verweigert: [{Verweigert}]",
                        sessionId, akteur.Name, string.Join(", ", verweigert));
            }

            // 2. Für jeden gültigen Event-Typ subscriben
            foreach (var eventTypeName in result.SubscribedEvents)
            {
                var subscribeSuccess = await subscriptionTracker.SubscribeAsync(eventTypeName, ct);
                if (!subscribeSuccess)
                {
                    _logger.LogWarning("{Session} could not subscribe to {EventType}", sessionId, eventTypeName);
                }
            }

            // 3. IMMER für CommandFailed subscriben (Targeted Delivery für diesen Client)
            var commandFailedSubscribed = await subscriptionTracker.SubscribeAsync("CommandFailed", ct);
            if (commandFailedSubscribed)
            {
                _logger.LogDebug("{Session} auto-subscribed to CommandFailed", sessionId);
            }

            // 4. NEU: Trigger-Handler registrieren
            foreach (var triggerName in result.HandlingTriggers)
            {
                _triggerHandlerRegistry.Register(triggerName, proxyPid);
            }

            // 5. NEU: Query-Handler registrieren
            foreach (var queryName in result.HandlingQueries)
            {
                _queryHandlerRegistry.Register(queryName, proxyPid);
            }

            // 6. Unbekannte Typen loggen
            if (result.UnknownTypes.Any())
            {
                _logger.LogWarning("{Session} unknown types: [{Types}]", sessionId, string.Join(", ", result.UnknownTypes));
            }

            // 7. Response senden (mit dem angenommenen Vertrag + Server-Hash — der Client sieht so einen abweichenden Stand)
            var response = _capabilitiesHandler.BuildResponse(result);
            if (sitzung.Vertrag is { } angenommen)
            {
                response.Vertrag = angenommen.Name;
                response.VertragHash = angenommen.VertragHash;
            }
            var serverMessage = new ProtoRepo.ServerMessage
            {
                CapabilitiesResponse = response
            };

            await responseStream.WriteAsync(serverMessage, ct);

            _logger.LogInformation(
                "{Session} → CapabilitiesResponse: {Commands} commands, {Events} events, {Triggers} triggers, {Queries} queries, handling {HandlingTriggers} triggers / {HandlingQueries} queries",
                sessionId, result.AllowedCommands.Count, result.SubscribedEvents.Count,
                result.AllowedTriggers.Count, result.AllowedQueries.Count,
                result.HandlingTriggers.Count, result.HandlingQueries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Session} capabilities failed", sessionId);
            await SendErrorAsync(responseStream, "CAPABILITIES_FAILED", ex.Message, "", ct);
        }
    }

    // =========================================================================
    // COMMAND HANDLING
    // =========================================================================

    private async Task HandleCommandAsync(
        ProtoRepo.CommandRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        AkteurSitzung sitzung,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Command", sessionId);
        var akteur = sitzung.Akteur;

        try
        {
            var envelope = _mapper.MapToDomain(request.Envelope);
            envelope = envelope with { OriginSessionId = sessionId };
            // Tor aus (opt-in): niemand ist angemeldet — die Herkunft folgt dann dem Modell: darf genau EIN Akteur den Typ,
            //   kommt er von ihm (sonst bliebe die ganze Kette dahinter ohne Akteur). Mit Tor stempelt unten das Token.
            if (akteur == null && !ImAuftrag.IstAkteur(envelope.UserId)
                && Infrastructure.Akteure.AkteurHerkunft.EindeutigerHalter(envelope.Payload.GetType()) is { } halter)
                envelope = envelope with { UserId = halter };

            if (akteur != null)
            {
                // Wer hineingibt, steht im Envelope — vom Tor gestempelt, nicht vom Client behauptet.
                envelope = envelope with { UserId = akteur.AkteurFuer(envelope.Payload.GetType()) };
                if (!akteur.DarfHinein(envelope.Payload.GetType()))
                {
                    _logger.LogWarning("{Session} Akteur {Akteur} darf {Command} nicht",
                        sessionId, akteur.Name, envelope.Payload.GetType().Name);
                    if (string.IsNullOrWhiteSpace(envelope.AggregateType))
                        envelope = envelope with { AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(envelope.Payload) };
                    if (AkteurVerweigerung.Baue(envelope, akteur.Name) is { } verweigert)
                        await _publisher.PublishAsync(verweigert);
                    return;
                }
            }

            // Routing über Typen (Invariante 3): der AggregateType ist serverseitig autoritativ aus
            // dem Command-Typ ableitbar (generierte CommandToAggregate-Map). Clients, die ihn nicht
            // setzen — z. B. der Python-Worker, der reaktive Melde*-Commands emittiert — würden sonst
            // eine ClusterIdentity mit leerem Kind erzeugen; die ist nicht routbar → Dispatch läuft in
            // den Timeout (DLQ), das Command wirkt nie. Fehlt der Type, hier deterministisch auflösen.
            if (string.IsNullOrWhiteSpace(envelope.AggregateType))
                envelope = envelope with { AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(envelope.Payload) };

            // Emittiert-Modus über die gRPC-Grenze (§4.2): ein externer Zusage-Treiber — der
            // Python-Worker reagiert auf TrainingAngefordert und emittiert Melde*-Commands — ist
            // semantisch wie der Backend-Baustein Reaktion (emittierend), kein Client mit behaupteter Version. OCC würde ihn am
            // co-committeten KommandoVerarbeitet-Marker scheitern lassen (Stream steht auf v2, das
            // Event war v1). Der Wire kennt nur expected_version; negativ = Sentinel für Emittiert
            // (keine Version, Empfänger-Inbox dedupliziert) — dieselbe Semantik wie der interne
            // CommandEmitter. Positive Versionen bleiben strikt Client (OCC), z. B. die Blazor-GUI.
            if (request.Envelope.ExpectedVersion < 0)
                envelope = envelope with { Modus = new CommandModus.Emittiert() };

            // Zusage von außen mit Kausalität (Akteur-Konzept §5.3): der Client nennt das Event, auf das er antwortet. Mit Vertrag muss die
            // Antwort darin stehen (Auf(Event) → dieser Command); die CommandId wird deterministisch abgeleitet — doppelt
            // zugestellt ≠ doppelt wirksam (die Inbox des Ziels dedupliziert, Emittiert-Modus wie beim internen Emit).
            var c = request.Envelope;
            if (!string.IsNullOrEmpty(c.CausationStreamId) && Guid.TryParse(c.CausationStreamId, out var ursache))
            {
                var cmdTyp = envelope.Payload.GetType();
                if (sitzung.Vertrag is { } vertrag
                    && !(MessageTypeMapping.Resolve(c.CausationType).Type is { } ausloeser && vertrag.Zugesagt(ausloeser, cmdTyp)))
                {
                    _logger.LogWarning("{Session} {Command} ist laut Vertrag {Vertrag} keine Antwort auf {Ausloeser}",
                        sessionId, cmdTyp.Name, vertrag.Name, c.CausationType);
                    if (string.IsNullOrWhiteSpace(envelope.AggregateType))
                        envelope = envelope with { AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(envelope.Payload) };
                    if (AkteurVerweigerung.Baue(envelope, $"{vertrag.Name} (Vertrag: keine Antwort auf {c.CausationType})") is { } verweigert)
                        await _publisher.PublishAsync(verweigert);
                    return;
                }
                envelope = envelope with
                {
                    CommandId = AkteurVertragsPruefung.CommandId(envelope.CorrelationId, ursache, c.CausationVersion, c.CausationType,
                        c.CausationIndex, cmdTyp, envelope.AggregateId),
                    Modus = new CommandModus.Emittiert(),
                };
                // Im Namen des Akteurs, dessen getragener Vertrag diese Antwort vorsieht (ein Client kann mehrere Akteure tragen).
                if (sitzung.Vertrag is { } v2 && MessageTypeMapping.Resolve(c.CausationType).Type is { } ausl)
                    envelope = envelope with { UserId = v2.AkteurFuerZusage(ausl, cmdTyp) };
            }

            _logger.LogDebug("{Session} Command {Command} → {AggregateType} ({Modus}), CorrelationId {CorrelationId}",
                sessionId, envelope.Payload.GetType().Name, envelope.AggregateType,
                envelope.Modus.GetType().Name, envelope.CorrelationId);

            _dispatcher.Dispatch(envelope);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} command mapping failed", sessionId);
        
            await SendErrorAsync(
                responseStream,
                "COMMAND_MAPPING_FAILED",
                ex.Message,
                request.Envelope?.CorrelationId ?? "",
                ct);
        }
    }

    // =========================================================================
    // TRIGGER HANDLING — mit Client-Handler-Registry (NEU)
    // =========================================================================

    private async Task HandleTriggerAsync(
        ProtoRepo.TriggerRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        PID proxyPid,
        AkteurRechte? akteur,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Trigger", sessionId);

        try
        {
            var trigger = _mapper.MapToDomain(request.Payload);
            var triggerTypeName = trigger.GetType().Name;

            _logger.LogDebug("{Session} Trigger type: {Trigger}", sessionId, triggerTypeName);

            if (!AkteurTor.Darf(akteur, trigger.GetType()))
            {
                await SendTriggerAckAsync(responseStream, false, request.CorrelationId,
                    $"Akteur '{akteur!.Name}' darf {triggerTypeName} nicht", ct);
                return;
            }

            // NEU: Erst TriggerHandlerRegistry prüfen (Client-Handler)
            var handlerPid = _triggerHandlerRegistry.GetHandler(triggerTypeName);
            if (handlerPid != null)
            {
                _logger.LogDebug("{Session} forwarding trigger to client handler {Pid}", sessionId, handlerPid);
                
                var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<ProtoRepo.TriggerResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                if (!_pendingTriggerForwards.TryAdd(correlationId, tcs))
                {
                    await SendTriggerAckAsync(responseStream, false, request.CorrelationId,
                        "Duplicate correlation ID for trigger forward", ct);
                    return;
                }

                try
                {
                    // An Client-Proxy-Actor senden
                    _actorSystem.Root.Send(handlerPid, new TriggerForwardMsg(trigger, correlationId));

                    // Auf Antwort warten (mit Timeout)
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TriggerForwardTimeout);
                    using var registration = timeoutCts.Token.Register(
                        () => tcs.TrySetCanceled(timeoutCts.Token));

                    var result = await tcs.Task;

                    await SendTriggerAckAsync(responseStream,
                        result.Accepted, request.CorrelationId,
                        result.Accepted ? null : result.ErrorMessage, ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("{Session} trigger forward timed out", sessionId);
                    await SendTriggerAckAsync(responseStream, false, request.CorrelationId,
                        "Client handler timeout", ct);
                }
                finally
                {
                    _pendingTriggerForwards.TryRemove(correlationId, out _);
                }
                return;
            }

            // Fallback: Pipeline-Routing (bestehend)
            var triggerType = trigger.GetType();
            if (!GeneratedPipelines.TriggerToPipelineId.TryGetValue(triggerType, out var pipelineId))
            {
                _logger.LogWarning("{Session} no handler for trigger {Trigger}", sessionId, triggerType.Name);
                
                await SendTriggerAckAsync(responseStream, false,
                    request.CorrelationId,
                    $"No handler for {triggerType.Name}", ct);
                return;
            }

            var identity = ClusterIdentity.Create(pipelineId, $"Pipeline-{pipelineId}");
            var ack = await _actorSystem.Cluster().RequestAsync<PipelineAck>(
                identity, trigger, ct);

            await SendTriggerAckAsync(responseStream,
                ack?.Accepted ?? false,
                request.CorrelationId,
                ack?.Accepted == false ? "Pipeline rejected" : null, ct);
            
            _logger.LogDebug("{Session} → TriggerAck: {Result}", sessionId, (ack?.Accepted ?? false) ? "accepted" : "rejected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} trigger failed", sessionId);
            
            await SendTriggerAckAsync(responseStream, false,
                request.CorrelationId, ex.Message, ct);
        }
    }

    // =========================================================================
    // QUERY HANDLING — mit Client-Handler-Registry (NEU)
    // =========================================================================

    private async Task HandleQueryAsync(
        ProtoRepo.QueryRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        PID proxyPid,
        AkteurRechte? akteur,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Query", sessionId);

        try
        {
            var query = _mapper.MapToDomain(request.Payload);
            var queryTypeName = query.GetType().Name;

            _logger.LogDebug("{Session} Query type: {Query}", sessionId, queryTypeName);

            if (!AkteurTor.Darf(akteur, query.GetType()))
            {
                await SendErrorAsync(responseStream, "AKTEUR_DARF_NICHT",
                    $"Akteur '{akteur!.Name}' darf {queryTypeName} nicht", request.CorrelationId, ct);
                return;
            }

            // NEU: Erst QueryHandlerRegistry prüfen (Client-Handler)
            var handlerPid = _queryHandlerRegistry.GetHandler(queryTypeName);
            if (handlerPid != null)
            {
                _logger.LogDebug("{Session} forwarding query to client handler {Pid}", sessionId, handlerPid);

                var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<ProtoRepo.QueryResponseFromClient>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                if (!_pendingQueryForwards.TryAdd(correlationId, tcs))
                {
                    await SendErrorAsync(responseStream, "QUERY_DUPLICATE_CORRELATION",
                        "Duplicate correlation ID for query forward", request.CorrelationId, ct);
                    return;
                }

                try
                {
                    // An Client-Proxy-Actor senden
                    _actorSystem.Root.Send(handlerPid, new QueryForwardMsg(query, correlationId));

                    // Auf Antwort warten (mit Timeout)
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(QueryForwardTimeout);
                    using var registration = timeoutCts.Token.Register(
                        () => tcs.TrySetCanceled(timeoutCts.Token));

                    var clientResponse = await tcs.Task;

                    // Fehler vom Client?
                    if (!string.IsNullOrEmpty(clientResponse.ErrorCode))
                    {
                        await SendErrorAsync(responseStream,
                            clientResponse.ErrorCode,
                            clientResponse.ErrorMessage,
                            request.CorrelationId, ct);
                        return;
                    }

                    // Antwort an den anfragenden Client weiterleiten
                    var serverMessage = new ProtoRepo.ServerMessage
                    {
                        QueryResponse = new ProtoRepo.QueryResponse
                        {
                            CorrelationId = request.CorrelationId,
                            Payload = clientResponse.Payload
                        }
                    };
                    await responseStream.WriteAsync(serverMessage, ct);

                    _logger.LogDebug("{Session} → QueryResponse (from client handler)", sessionId);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("{Session} query forward timed out", sessionId);
                    await SendErrorAsync(responseStream, "QUERY_FORWARD_TIMEOUT",
                        "Client handler did not respond in time", request.CorrelationId, ct);
                }
                finally
                {
                    _pendingQueryForwards.TryRemove(correlationId, out _);
                }
                return;
            }

            // Fallback: ProjectionQueryService (bestehend). Die vom Client mitgeschickten
            // "zuletzt geschrieben"-IDs werden als zusätzliche Deps getrackt (Read-Your-Writes).
            var response = await _queryService.ExecuteAsync(query, request.ExpectedFreshIds);
            var responseDto = _mapper.ToQueryResponse(response, request.CorrelationId);
            var serverMsg = new ProtoRepo.ServerMessage { QueryResponse = responseDto };
            
            await responseStream.WriteAsync(serverMsg, ct);
            
            _logger.LogDebug("{Session} → QueryResponse: {Response}", sessionId, response.Data.GetType().Name);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning("{Session} query not supported: {Message}", sessionId, ex.Message);
            await SendErrorAsync(responseStream, "QUERY_NOT_SUPPORTED", ex.Message, request.CorrelationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} query failed", sessionId);
            await SendErrorAsync(responseStream, "QUERY_FAILED", ex.Message, request.CorrelationId, ct);
        }
    }

    // =========================================================================
    // TRANSIENT EVENT HANDLING (NEU)
    // =========================================================================

    private async Task HandleTransientEventAsync(
        ProtoRepo.TransientEventRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        AkteurRechte? akteur,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← TransientEvent", sessionId);

        try
        {
            var envelope = _mapper.MapToDomain(request.Envelope);

            if (envelope.Payload is not ITransientEvent)
            {
                _logger.LogWarning("{Session} rejected: {Payload} is not ITransientEvent", sessionId, envelope.Payload.GetType().Name);
                await SendErrorAsync(responseStream, "INVALID_TRANSIENT_EVENT",
                    $"{envelope.Payload.GetType().Name} does not implement ITransientEvent",
                    "", ct);
                return;
            }

            if (!AkteurTor.Darf(akteur, envelope.Payload.GetType()))
            {
                await SendErrorAsync(responseStream, "AKTEUR_DARF_NICHT",
                    $"Akteur '{akteur!.Name}' darf {envelope.Payload.GetType().Name} nicht", "", ct);
                return;
            }
            if (akteur != null)
                envelope = envelope with { UserId = akteur.AkteurFuer(envelope.Payload.GetType()) };
            else if (!ImAuftrag.IstAkteur(envelope.UserId) && Infrastructure.Akteure.AkteurHerkunft.EindeutigerHalter(envelope.Payload.GetType()) is { } halter)
                envelope = envelope with { UserId = halter };   // Tor aus: Herkunft laut Modell (der eine Akteur mit IDarf)

            await _publisher.PublishAsync(envelope, ct);

            _logger.LogDebug("{Session} TransientEvent published: {Payload}", sessionId, envelope.Payload.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} TransientEvent failed", sessionId);
            await SendErrorAsync(responseStream, "TRANSIENT_EVENT_FAILED", ex.Message, "", ct);
        }
    }

    // =========================================================================
    // QUERY ANSWER FROM CLIENT (NEU)
    // =========================================================================

    private void HandleQueryAnswer(
        ProtoRepo.QueryResponseFromClient answer,
        string sessionId)
    {
        _logger.LogDebug("{Session} ← QueryAnswer, CorrelationId {CorrelationId}", sessionId, answer.CorrelationId);

        if (_pendingQueryForwards.TryGetValue(answer.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(answer);
        }
        else
        {
            _logger.LogWarning("{Session} QueryAnswer for unknown CorrelationId {CorrelationId}", sessionId, answer.CorrelationId);
        }
    }

    // =========================================================================
    // TRIGGER RESULT FROM CLIENT (NEU)
    // =========================================================================

    private void HandleTriggerResult(
        ProtoRepo.TriggerResult result,
        string sessionId)
    {
        _logger.LogDebug("{Session} ← TriggerResult, CorrelationId {CorrelationId}", sessionId, result.CorrelationId);

        if (_pendingTriggerForwards.TryGetValue(result.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
        else
        {
            _logger.LogWarning("{Session} TriggerResult for unknown CorrelationId {CorrelationId}", sessionId, result.CorrelationId);
        }
    }

    // =========================================================================
    // SUBSCRIBE HANDLING (unverändert)
    // =========================================================================

    private async Task HandleSubscribeAsync(
        ProtoRepo.SubscribeRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        AkteurRechte? akteur,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Subscribe: {EventType}", sessionId, request.EventType);

        try
        {
            if (akteur != null && MessageTypeMapping.Resolve(request.EventType).Type is { } evt && !akteur.DarfHoeren(evt))
            {
                await SendErrorAsync(responseStream, "AKTEUR_DARF_NICHT",
                    $"Akteur '{akteur.Name}' darf {request.EventType} nicht hören", "", ct);
                return;
            }

            var success = await subscriptionTracker.SubscribeAsync(request.EventType, ct);

            if (!success)
            {
                await SendErrorAsync(
                    responseStream,
                    "SUBSCRIBE_FAILED",
                    $"Unknown event type: {request.EventType}",
                    "",
                    ct);
                return;
            }

            var confirmed = new ProtoRepo.ServerMessage
            {
                SubscriptionConfirmed = new ProtoRepo.SubscriptionConfirmed
                {
                    EventType = request.EventType,
                    AggregateId = request.AggregateId
                }
            };
            
            await responseStream.WriteAsync(confirmed, ct);
            
            _logger.LogDebug("{Session} → SubscriptionConfirmed: {EventType}", sessionId, request.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} subscribe failed", sessionId);
            
            await SendErrorAsync(
                responseStream,
                "SUBSCRIBE_FAILED",
                ex.Message,
                "",
                ct);
        }
    }

    // =========================================================================
    // UNSUBSCRIBE HANDLING (unverändert)
    // =========================================================================

    private async Task HandleUnsubscribeAsync(
        ProtoRepo.UnsubscribeRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Unsubscribe: {EventType}", sessionId, request.EventType);

        try
        {
            await subscriptionTracker.UnsubscribeAsync(request.EventType, ct);

            var confirmed = new ProtoRepo.ServerMessage
            {
                UnsubscriptionConfirmed = new ProtoRepo.UnsubscriptionConfirmed
                {
                    EventType = request.EventType,
                    AggregateId = request.AggregateId
                }
            };
            
            await responseStream.WriteAsync(confirmed, ct);
            
            _logger.LogDebug("{Session} → UnsubscriptionConfirmed: {EventType}", sessionId, request.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} unsubscribe failed", sessionId);
            
            await SendErrorAsync(
                responseStream,
                "UNSUBSCRIBE_FAILED",
                ex.Message,
                "",
                ct);
        }
    }

    // =========================================================================
    // HELPERS (unverändert)
    // =========================================================================

    private static async Task SendTriggerAckAsync(
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        bool accepted,
        string correlationId,
        string? errorMessage,
        CancellationToken ct)
    {
        try
        {
            var ack = new ProtoRepo.ServerMessage
            {
                TriggerAck = new ProtoRepo.TriggerAck
                {
                    Accepted = accepted,
                    CorrelationId = correlationId ?? "",
                    ErrorMessage = errorMessage ?? ""
                }
            };
            
            await responseStream.WriteAsync(ack, ct);
        }
        catch
        {
            // Stream möglicherweise bereits geschlossen
        }
    }

    // =========================================================================
    // KATALOG-FUNKTIONEN EXTERN (docs/konzept-editor-pipelines.md §14.5)
    // =========================================================================

    /// <summary>
    /// Die Anbieter-Sitzung dieser Verbindung, live verdrahtet: Hol-/Melde-Wege zum Vermittler im Cluster, Weiterreichen über
    /// den EventProxyActor (EIN Schreiber am Stream), Schreiben über den Ausführer (genau ein Ergebnis + Weckung).
    /// </summary>
    private FunktionsAnbieterSitzung BaueAnbieterSitzung(string sessionId, PID proxyPid, FunktionsAusfuehrer ausfuehrer)
    {
        var cluster = _actorSystem.Cluster();
        return new FunktionsAnbieterSitzung(
            $"{_actorSystem.Address}/{sessionId}",   // Arbeiter-Name am Vermittler: eindeutig über Knoten hinweg
            name => FunktionsAnbieterSitzung.LoeseImKatalog(name, GeneratedFunktionen.Ergebnisse.Keys),
            f => GeneratedFunktionen.Ergebnisse.TryGetValue(f, out var e) ? e : Array.Empty<Type>(),
            (f, anfrage, ct) => cluster.RequestAsync<ArbeitZugeteilt>(FunktionsVermittlerActor.Identitaet(f), anfrage, ct),
            async (f, nachricht) =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await cluster.RequestAsync<WakeAck>(FunktionsVermittlerActor.Identitaet(f), nachricht, cts.Token);
            },
            (a, f) =>
            {
                _actorSystem.Root.Send(proxyPid, new ServerNachrichtMsg(new ProtoRepo.ServerMessage
                {
                    ArbeitsAuftrag = new ProtoRepo.ArbeitsAuftrag
                    {
                        Vorgang = a.Vorgang.ToString(),
                        Korrelation = a.Korrelation.ToString(),
                        Funktion = f.Name,
                        Auftrag = _mapper.MapToDto(a.Auftrag),
                        Akteur = a.Akteur ?? ""
                    }
                }));
                return Task.CompletedTask;
            },
            ausfuehrer.SchreibeErgebnisAsync,
            _logger);
    }

    private async Task HandleFunktionenAnbietenAsync(
        ProtoRepo.FunktionenAnbieten request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        FunktionsAnbieterSitzung? anbieter,
        string sessionId,
        CancellationToken ct)
    {
        if (anbieter == null)
        {
            await SendErrorAsync(responseStream, "FUNKTIONEN_NICHT_VERFUEGBAR",
                "Dieser Host führt keine Katalog-Funktionen aus (kein Funktions-Ausführer konfiguriert)", "", ct);
            return;
        }
        var unbekannt = anbieter.Biete(request.Angebote.Select(a => (a.Funktion, a.Slots)));
        if (unbekannt.Count > 0)
        {
            _logger.LogWarning("{Session} bietet unbekannte Funktionen an: [{Unbekannt}]", sessionId, string.Join(", ", unbekannt));
            await SendErrorAsync(responseStream, "FUNKTION_UNBEKANNT",
                $"Unbekannte Funktion(en): {string.Join(", ", unbekannt)}", "", ct);
        }
    }

    private async Task HandleArbeitsErgebnisAsync(
        ProtoRepo.ArbeitsErgebnis request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        FunktionsAnbieterSitzung? anbieter,
        string sessionId,
        CancellationToken ct)
    {
        if (anbieter == null || !Guid.TryParse(request.Vorgang, out var vorgang))
        {
            await SendErrorAsync(responseStream, "ARBEITS_ERGEBNIS_UNGUELTIG",
                $"Ergebnis ohne gültigen Vorgang oder ohne Funktions-Ausführer: '{request.Vorgang}'", request.Vorgang, ct);
            return;
        }

        var fehler = string.IsNullOrEmpty(request.Fehler) ? null : request.Fehler;
        IEvent? ergebnis = null;
        if (fehler == null && request.Ergebnis != null)
        {
            try { ergebnis = _mapper.MapErgebnis(request.Ergebnis); }
            catch (Exception ex) { fehler = $"Ergebnis nicht lesbar: {ex.Message}"; }
        }

        var ausgang = await anbieter.ErgebnisAsync(vorgang, ergebnis, fehler, ct);
        _logger.LogDebug("{Session} Ergebnis {Vorgang}: {Ausgang}", sessionId, vorgang, ausgang);
        if (ausgang == ErgebnisAusgang.Unbekannt)
            await SendErrorAsync(responseStream, "ARBEIT_UNBEKANNT",
                $"Vorgang {vorgang} läuft in dieser Sitzung nicht (Lease abgelaufen oder schon erledigt)", request.Vorgang, ct);
    }

    private static async Task SendErrorAsync(
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        string code,
        string message,
        string correlationId,
        CancellationToken ct)
    {
        try
        {
            var error = new ProtoRepo.ServerMessage
            {
                Error = new ProtoRepo.ErrorResponse
                {
                    Code = code,
                    Message = message,
                    CorrelationId = correlationId
                }
            };
            
            await responseStream.WriteAsync(error, ct);
        }
        catch
        {
            // Stream möglicherweise bereits geschlossen
        }
    }
}
