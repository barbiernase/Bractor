// REPO-PFAD: Infrastructure/GrpcClient/CqrsClientService.cs  (MODIFIZIERT)
using System.Collections.Concurrent;
using Abstractions;
using Domain.Projections;
using Grpc.Core;
using Infrastructure.Extensions;
using Infrastructure.Mapping;
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
        ILogger<CqrsClientServiceImpl>? logger = null)
    {
        _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _triggerHandlerRegistry = triggerHandlerRegistry ?? throw new ArgumentNullException(nameof(triggerHandlerRegistry));
        _queryHandlerRegistry = queryHandlerRegistry ?? throw new ArgumentNullException(nameof(queryHandlerRegistry));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
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

        PID? proxyPid = null;

        try
        {
            // 1. EventProxyActor spawnen (für PID)
            var proxyProps = Props.FromProducer(() =>
                new EventProxyActor(responseStream, _mapper, sessionId, _logger));
            proxyPid = _actorSystem.Root.Spawn(proxyProps);

            _logger.LogDebug("{Session} EventProxy spawned: {Pid}", sessionId, proxyPid);

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
                        proxyPid,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Session} error", sessionId);
        }
        finally
        {
            // 4. Cleanup: Registrierungen entfernen, pending Forwards abbrechen, Actor stoppen
            // SubscriptionTracker.DisposeAsync() wird automatisch aufgerufen (await using)

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
        string sessionId,
        CancellationToken ct)
    {
        try
        {
            switch (message.MessageCase)
            {
                case ProtoRepo.ClientMessage.MessageOneofCase.Command:
                    await HandleCommandAsync(message.Command, responseStream, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Subscribe:
                    await HandleSubscribeAsync(message.Subscribe, responseStream, subscriptionTracker, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Unsubscribe:
                    await HandleUnsubscribeAsync(message.Unsubscribe, responseStream, subscriptionTracker, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Capabilities:
                    await HandleCapabilitiesAsync(message.Capabilities, responseStream, subscriptionTracker, proxyPid, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Query:
                    await HandleQueryAsync(message.Query, responseStream, proxyPid, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Trigger:
                    await HandleTriggerAsync(message.Trigger, responseStream, proxyPid, sessionId, ct);
                    break;

                // ═════════════════════════════════════════
                // NEU: First-Citizen Messages
                // ═════════════════════════════════════════

                case ProtoRepo.ClientMessage.MessageOneofCase.TransientEvent:
                    await HandleTransientEventAsync(message.TransientEvent, responseStream, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.QueryAnswer:
                    HandleQueryAnswer(message.QueryAnswer, sessionId);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.TriggerResult:
                    HandleTriggerResult(message.TriggerResult, sessionId);
                    break;

                default:
                    _logger.LogWarning("{Session} unknown message type: {MessageCase}", sessionId, message.MessageCase);
                    break;
            }
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
        string sessionId,
        CancellationToken ct)
    {
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

            // 7. Response senden
            var response = _capabilitiesHandler.BuildResponse(result);
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
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Command", sessionId);

        try
        {
            var envelope = _mapper.MapToDomain(request.Envelope);
            envelope = envelope with { OriginSessionId = sessionId };

            // Routing über Typen (Invariante 3): der AggregateType ist serverseitig autoritativ aus
            // dem Command-Typ ableitbar (generierte CommandToAggregate-Map). Clients, die ihn nicht
            // setzen — z. B. der Python-Worker, der reaktive Melde*-Commands emittiert — würden sonst
            // eine ClusterIdentity mit leerem Kind erzeugen; die ist nicht routbar → Dispatch läuft in
            // den Timeout (DLQ), das Command wirkt nie. Fehlt der Type, hier deterministisch auflösen.
            if (string.IsNullOrWhiteSpace(envelope.AggregateType))
                envelope = envelope with { AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(envelope.Payload) };

            // Emittiert-Modus über die gRPC-Grenze (§4.2): ein externer Reaktions-Treiber — der
            // Python-Worker reagiert auf TrainingAngefordert und emittiert Melde*-Commands — ist
            // semantisch eine Reaktion, kein Client mit behaupteter Version. OCC würde ihn am
            // co-committeten KommandoVerarbeitet-Marker scheitern lassen (Stream steht auf v2, das
            // Event war v1). Der Wire kennt nur expected_version; negativ = Sentinel für Emittiert
            // (keine Version, Empfänger-Inbox dedupliziert) — dieselbe Semantik wie der interne
            // CommandEmitter. Positive Versionen bleiben strikt Client (OCC), z. B. die Blazor-GUI.
            if (request.Envelope.ExpectedVersion < 0)
                envelope = envelope with { Modus = new CommandModus.Emittiert() };

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
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Trigger", sessionId);

        try
        {
            var trigger = _mapper.MapToDomain(request.Payload);
            var triggerTypeName = trigger.GetType().Name;

            _logger.LogDebug("{Session} Trigger type: {Trigger}", sessionId, triggerTypeName);

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
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Query", sessionId);

        try
        {
            var query = _mapper.MapToDomain(request.Payload);
            var queryTypeName = query.GetType().Name;

            _logger.LogDebug("{Session} Query type: {Query}", sessionId, queryTypeName);

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

            // Fallback: ProjectionQueryService (bestehend)
            var response = await _queryService.ExecuteAsync(query);
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
        string sessionId,
        CancellationToken ct)
    {
        _logger.LogDebug("{Session} ← Subscribe: {EventType}", sessionId, request.EventType);

        try
        {
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
