// REPO-PFAD: Infrastructure/GrpcClient/CqrsClientService.cs  (MODIFIZIERT)
using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Akteure;
using Grpc.Core;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.Mapping;
using Infrastructure.Projections;   // WakeAck
using Infrastructure.PubSub;
using Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

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
/// 4. Service registriert in QueryHandlerRegistry
/// 5. Read-Loop verarbeitet ClientMessages
/// 6. finally-Block: Subscriptions beenden, Registrierungen entfernen, Actor stoppen
/// </summary>
public class CqrsClientServiceImpl : Cqrs.Leitung.CqrsClientService.CqrsClientServiceBase
{
    private readonly ActorSystem _actorSystem;
    private readonly ProtoMessageMapper _mapper;
    private readonly IAggregateDispatcher _dispatcher;
    private readonly CapabilitiesHandler _capabilitiesHandler;
    private readonly IQueryDienst _queryService;
    private readonly QueryHandlerRegistry _queryHandlerRegistry;
    private readonly BrokerPublisher _publisher;
    private readonly AkteurTor? _akteurTor;
    private readonly IAusgabeAblage? _ausgaben;
    private readonly Konsum _konsum;
    private readonly ILogger _logger;

    /// <summary>
    /// Timeout für Query-Forwarding an Clients.
    /// Konfigurierbar, aber mit sensiblem Default für ML-Inference.
    /// </summary>
    private static readonly TimeSpan QueryForwardTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Pending Query-Forwards über alle Verbindungen hinweg.
    /// Key: CorrelationId, Value: TaskCompletionSource für die Antwort.
    /// 
    /// Cross-Connection: HandleQueryAsync (Session A) registriert TCS,
    /// HandleQueryAnswer (Session B des Handler-Clients) vervollständigt ihn.
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Cqrs.Leitung.QueryResponseFromClient>>
        _pendingQueryForwards = new();

    
    private static int _sessionCounter = 0;

    /// <summary>Das Hinweis-Netz (Plan §2.9): ein Hinweis, den ein Client sendet (ISendet), geht auch an die Funktionen, die ihn hören.</summary>
    private readonly HinweisNetz? _hinweisNetz;

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
        IQueryDienst queryService,
        QueryHandlerRegistry queryHandlerRegistry,
        BrokerPublisher publisher,
        AkteurTor? akteurTor = null,
        ILogger<CqrsClientServiceImpl>? logger = null,
        IAusgabeAblage? ausgaben = null,
        Konsum? konsum = null,
        HinweisNetz? hinweisNetz = null)
    {
        _hinweisNetz = hinweisNetz;   // null = keine Funktion hört Hinweise
        _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _queryHandlerRegistry = queryHandlerRegistry ?? throw new ArgumentNullException(nameof(queryHandlerRegistry));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _akteurTor = akteurTor; // null = Akteure nicht konfiguriert → Pfad offen wie bisher (opt-in)
        _ausgaben = ausgaben;   // null = keine Funktion draußen in dieser Anwendung → Anbieten wird abgewiesen
        _konsum = konsum ?? Konsum.Standard;
        _logger = logger ?? NullLogger<CqrsClientServiceImpl>.Instance;
        _capabilitiesHandler = new CapabilitiesHandler();
    }

    public override async Task Connect(
        IAsyncStreamReader<Cqrs.Leitung.ClientMessage> requestStream,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
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
        BausteinSitzung? anbieter = null;
        // Wer diese Session ist: vom Tor (Token), ggf. erst am Handshake vom Client-Vertrag festgelegt (Akteur-Konzept §5.2).
        var sitzung = new AkteurSitzung(akteur);

        try
        {
            // 1. EventProxyActor spawnen (für PID)
            var proxyProps = Props.FromProducer(() =>
                new EventProxyActor(responseStream, _mapper, sessionId, _logger));
            proxyPid = _actorSystem.Root.Spawn(proxyProps);

            _logger.LogDebug("{Session} EventProxy spawned: {Pid}", sessionId, proxyPid);

            // 1b. Funktionen draußen (ITraegt, Laufort Extern): Hol-Schleifen starten erst, wenn der Client anbietet.
            if (_ausgaben != null)
                anbieter = BaueAnbieterSitzung(sessionId, proxyPid, _ausgaben);

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

            // Funktionen draußen: Hol-Schleifen beenden; Laufendes nicht erledigt melden — die Leases laufen ab, der Vermittler gibt die
            // Eingänge neu aus (anderer Träger, oder dieser nach Reconnect), und der Funktions-Actor sendet nur, was festgehalten ist.
            if (anbieter != null)
            {
                try { await anbieter.DisposeAsync(); }
                catch (Exception ex) { _logger.LogDebug(ex, "{Session} Funktions-Anbieter: Ende mit Fehler", sessionId); }
            }

            if (proxyPid != null)
            {
                // NEU: Registrierungen entfernen
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
        Cqrs.Leitung.ClientMessage message,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        PID proxyPid,
        AkteurSitzung sitzung,
        BausteinSitzung? anbieter,
        string sessionId,
        CancellationToken ct)
    {
        var akteur = sitzung.Akteur;
        try
        {
            switch (message.MessageCase)
            {
                case Cqrs.Leitung.ClientMessage.MessageOneofCase.Command:
                    await HandleCommandAsync(message.Command, responseStream, sitzung, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.Subscribe:
                    await HandleSubscribeAsync(message.Subscribe, responseStream, subscriptionTracker, akteur, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.Unsubscribe:
                    await HandleUnsubscribeAsync(message.Unsubscribe, responseStream, subscriptionTracker, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.Capabilities:
                    await HandleCapabilitiesAsync(message.Capabilities, responseStream, subscriptionTracker, proxyPid, sitzung, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.Query:
                    await HandleQueryAsync(message.Query, responseStream, proxyPid, akteur, sessionId, ct);
                    break;

                    break;

                // ═════════════════════════════════════════
                // NEU: First-Citizen Messages
                // ═════════════════════════════════════════

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.TransientEvent:
                    await HandleTransientEventAsync(message.TransientEvent, responseStream, akteur, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.QueryAnswer:
                    HandleQueryAnswer(message.QueryAnswer, sessionId);
                    break;

                    break;

                // ═════════════════════════════════════════
                // Funktionen draußen (ITraegt, Laufort Extern)
                // ═════════════════════════════════════════

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.FunktionenAnbieten:
                    await HandleFunktionenAnbietenAsync(message.FunktionenAnbieten, responseStream, anbieter, sitzung, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.ArbeitsErgebnis:
                    await HandleArbeitsErgebnisAsync(message.ArbeitsErgebnis, responseStream, anbieter, sessionId, ct);
                    break;

                case Cqrs.Leitung.ClientMessage.MessageOneofCase.ArbeitLebt:
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
        Cqrs.Leitung.CapabilitiesRequest request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        PID proxyPid,
        AkteurSitzung sitzung,
        string sessionId,
        CancellationToken ct)
    {
        // 0. Client-Vertrag (docs/konzept-akteure.md §5.2): „ich bin Client X" — geprüft gegen die generierte Tabelle (wirksam ist Vertrag ∩
        //    Token; abweichender Hash = Warnung bzw. im strengen Modus Ablehnung). Ohne Tor sagt der Vertrag, wer da ist.
        var vp = ClientVertragsPruefung.Pruefe(request.Vertrag, request.VertragHash, sitzung.Akteur, Anwendung.Akteure.Rechte,
            _akteurTor?.VertragStreng ?? false, Anwendung.Akteure.ClientVertraege);
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
            // Die Schnittmenge Vertrag ∩ Token IST die Befugnis der Session (docs/konzept-akteure.md §4.3).
            sitzung.Akteur = vertrag;
            _logger.LogInformation("{Session} Client {Vertrag} ({Typ}) angenommen — verkörpert {Akteure}", sessionId, vertrag.Name, vertrag.VertragTyp?.Name,
                string.Join(", ", vertrag.Teile.Select(t => t.Name)));
        }
        var akteur = sitzung.Akteur;

        var messageSource = request.MessageTypes.Any()
            ? $"message_types: [{string.Join(", ", request.MessageTypes)}]"
            : $"event_types: [{string.Join(", ", request.EventTypes)}]";
        _logger.LogDebug("{Session} ← Capabilities: {Source}", sessionId, messageSource);

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
            Func<Type, bool> internBedient = t => Anwendung.QueryTypen.Contains(t);
            Func<string, bool> schonVergeben = n => _queryHandlerRegistry.GetHandler(n) is { } q && !q.Equals(proxyPid);
            if (sitzung.Vertrag is { } v)
            {
                var abweichung = ClientVertragsPruefung.Wende(result, v, loese, internBedient, schonVergeben);
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

            // 4. Query-Handler registrieren
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
            var serverMessage = new Cqrs.Leitung.ServerMessage
            {
                CapabilitiesResponse = response
            };

            await responseStream.WriteAsync(serverMessage, ct);

            _logger.LogInformation(
                "{Session} → CapabilitiesResponse: {Commands} commands, {Events} events, {Queries} queries, handling {HandlingQueries} queries",
                sessionId, result.AllowedCommands.Count, result.SubscribedEvents.Count, result.AllowedQueries.Count, result.HandlingQueries.Count);
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
        Cqrs.Leitung.CommandRequest request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
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
                        _publisher.Melde(verweigert);
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

            // Ohne Versionsprüfung (negativ = Sentinel, wie der interne CommandEmitter): ein Client, der den Stand des Ziels nicht kennt und keine
            //   OCC will (ein Worker, der von sich aus sendet), schickt den Command im Modus Emittiert — die Inbox des Ziels dedupliziert über
            //   die Command-Id. Positive Versionen bleiben strikt Client (OCC), z. B. die Blazor-GUI.
            if (request.Envelope.ExpectedVersion < 0)
                envelope = envelope with { Modus = new CommandModus.Emittiert() };

            // Spur: von außen schreibt der Akteur (Wirker akteur:<A>); ohne Akteur bleibt der Wirker leer.
            if (ImAuftrag.IstAkteur(envelope.UserId))
                envelope = envelope with { Wirker = WirkerId.FuerAkteur(envelope.UserId) };

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
    // QUERY HANDLING — mit Client-Handler-Registry (NEU)
    // =========================================================================

    private async Task HandleQueryAsync(
        Cqrs.Leitung.QueryRequest request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        PID proxyPid,
        AkteurRechte? akteur,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Query", sessionId);

        try
        {
            var query = _mapper.QueryAus(request.Payload);
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
                var tcs = new TaskCompletionSource<Cqrs.Leitung.QueryResponseFromClient>(
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
                    var serverMessage = new Cqrs.Leitung.ServerMessage
                    {
                        QueryResponse = new Cqrs.Leitung.QueryResponse
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
            var serverMsg = new Cqrs.Leitung.ServerMessage { QueryResponse = responseDto };
            
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
        Cqrs.Leitung.TransientEventRequest request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
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

            _publisher.Melde(envelope);
            // An die Funktionen, die den Hinweis hören (Hinweis-Netz): die Kennung vergibt der Client (CausationId, sonst gewürfelt — Clients
            //   würfeln, R13), die Adresse folgt aus dem Stream, den er nennt (A6).
            _hinweisNetz?.Verteile(new HinweisZustellen(Guid.TryParse(envelope.CausationId, out var kennung) ? kennung : Guid.NewGuid(), envelope.Payload,
                envelope.AggregateId, envelope.AggregateVersion, DateTimeOffset.UtcNow, envelope.CorrelationId,
                ImAuftrag.IstAkteur(envelope.UserId) ? envelope.UserId : null));

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
        Cqrs.Leitung.QueryResponseFromClient answer,
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
    // SUBSCRIBE HANDLING (unverändert)
    // =========================================================================

    private async Task HandleSubscribeAsync(
        Cqrs.Leitung.SubscribeRequest request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
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

            var confirmed = new Cqrs.Leitung.ServerMessage
            {
                SubscriptionConfirmed = new Cqrs.Leitung.SubscriptionConfirmed
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
        Cqrs.Leitung.UnsubscribeRequest request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Unsubscribe: {EventType}", sessionId, request.EventType);

        try
        {
            await subscriptionTracker.UnsubscribeAsync(request.EventType, ct);

            var confirmed = new Cqrs.Leitung.ServerMessage
            {
                UnsubscriptionConfirmed = new Cqrs.Leitung.UnsubscriptionConfirmed
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

    // =========================================================================
    // FUNKTIONEN DRAUSSEN (Plan Pub/Sub-Neustrukturierung §2.2, §2.7: ITraegt, Laufort Extern)
    // =========================================================================

    /// <summary>
    /// Die Anbieter-Sitzung dieser Verbindung, live verdrahtet: Hol-/Melde-Wege zum Vermittler im Cluster, Weiterreichen über den EventProxyActor
    /// (EIN Schreiber am Stream), Festhalten der Antwort unter „Funktion, Eingang“ und die Meldung an den Funktions-Actor der Partition, der sendet.
    /// </summary>
    private BausteinSitzung BaueAnbieterSitzung(string sessionId, PID proxyPid, IAusgabeAblage ausgaben)
    {
        var bote = new Bote(_actorSystem.Cluster(), _logger);
        var draussen = Anwendung.Funktionen.Bausteine.Where(b => b.Draussen).ToList();
        return new BausteinSitzung(
            $"{_actorSystem.Address}/{sessionId}",   // Arbeiter-Name am Vermittler: eindeutig über Knoten hinweg
            name => draussen.FirstOrDefault(b => b.Name == name),
            (b, anfrage, ct) => bote.FrageAsync<EingaengeZugeteilt>(FunktionsVermittlerActor.Identitaet(b.Typ), anfrage,
                TimeSpan.FromMilliseconds(Math.Max(0, anfrage.WarteMs)) + TimeSpan.FromSeconds(5), ct),
            (b, nachricht) => { bote.Melde(FunktionsVermittlerActor.Identitaet(b.Typ), nachricht); return Task.CompletedTask; },
            (e, b, ab) =>
            {
                _actorSystem.Root.Send(proxyPid, new ServerNachrichtMsg(new Cqrs.Leitung.ServerMessage
                {
                    ArbeitsAuftrag = new Cqrs.Leitung.ArbeitsAuftrag
                    {
                        Vorgang = e.Vorgang.ToString(),
                        Korrelation = e.KorrelationsText ?? "",
                        Funktion = b.Name,
                        Eingang = _mapper.MapToDto(e.Eingang),
                        Akteur = e.Akteur ?? "",
                        Stream = e.Stream.ToString(),
                        Version = e.Version,
                        ZeitUtc = e.Zeit.ToUnixTimeMilliseconds(),
                        Ab = ab,
                        Hinweis = e.IstHinweis,
                    }
                }));
                return Task.CompletedTask;
            },
            ausgaben,
            (b, e, fehler, teil, ausgaben) =>
            {
                bote.Melde(FunktionsActor.IdentitaetFuer(b, e.Stream, _konsum), EingangBeantwortet.Aus(e.Vorgang, fehler, teil, ausgaben));
                return Task.CompletedTask;
            },
            _logger);
    }

    private async Task HandleFunktionenAnbietenAsync(
        Cqrs.Leitung.FunktionenAnbieten request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        BausteinSitzung? anbieter,
        AkteurSitzung sitzung,
        string sessionId,
        CancellationToken ct)
    {
        if (anbieter == null)
        {
            await SendErrorAsync(responseStream, "FUNKTIONEN_NICHT_VERFUEGBAR",
                "Dieser Host hat keine Funktion, die draußen rechnet", "", ct);
            return;
        }
        // Akteur-Tor: mit angemeldetem Akteur nur, was er rechnen darf (IDarf<F>, bei einem Client ∩ ITraegt); ohne Tor/Akteur offen.
        var akteur = sitzung.Akteur;
        var unbekannt = anbieter.Biete(request.Angebote.Select(a => (a.Funktion, a.Slots)),
            akteur is null ? null : akteur.DarfRechnen, out var nichtBefugt);
        if (nichtBefugt.Count > 0)
            await SendErrorAsync(responseStream, "FUNKTION_NICHT_BEFUGT",
                $"{akteur!.Name} darf nicht rechnen: {string.Join(", ", nichtBefugt)} (IDarf<…> am Akteur bzw. ITraegt<…> im Client-Vertrag fehlt)", "", ct);
        if (unbekannt.Count > 0)
        {
            _logger.LogWarning("{Session} bietet unbekannte Funktionen an: [{Unbekannt}]", sessionId, string.Join(", ", unbekannt));
            await SendErrorAsync(responseStream, "FUNKTION_UNBEKANNT",
                $"Unbekannte Funktion(en) (keine Funktion draußen dieses Namens): {string.Join(", ", unbekannt)}", "", ct);
        }
    }

    private async Task HandleArbeitsErgebnisAsync(
        Cqrs.Leitung.ArbeitsErgebnis request,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        BausteinSitzung? anbieter,
        string sessionId,
        CancellationToken ct)
    {
        if (anbieter == null || !Guid.TryParse(request.Vorgang, out var vorgang))
        {
            await SendErrorAsync(responseStream, "ARBEITS_ERGEBNIS_UNGUELTIG",
                $"Antwort ohne gültigen Eingang oder ohne Funktion draußen: '{request.Vorgang}'", request.Vorgang, ct);
            return;
        }

        var fehler = string.IsNullOrEmpty(request.Fehler) ? null : request.Fehler;
        List<IAusgabe>? ausgaben = null;
        if (fehler == null)
        {
            try { ausgaben = request.Ausgaben.Select(_mapper.Ausgabe).ToList(); }
            catch (Exception ex) { fehler = $"Antwort nicht lesbar: {ex.Message}"; }
        }

        var ausgang = await anbieter.AntwortAsync(vorgang, ausgaben, fehler, ct, request.Ab, request.Fertig);
        _logger.LogDebug("{Session} Antwort {Vorgang} (ab {Ab}, fertig {Fertig}): {Ausgang}", sessionId, vorgang, request.Ab, request.Fertig, ausgang);
        if (ausgang == ErgebnisAusgang.Unbekannt)
            await SendErrorAsync(responseStream, "ARBEIT_UNBEKANNT",
                $"Eingang {vorgang} läuft in dieser Sitzung nicht (Lease abgelaufen oder schon erledigt)", request.Vorgang, ct);
    }

    private static async Task SendErrorAsync(
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        string code,
        string message,
        string correlationId,
        CancellationToken ct)
    {
        try
        {
            var error = new Cqrs.Leitung.ServerMessage
            {
                Error = new Cqrs.Leitung.ErrorResponse
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
