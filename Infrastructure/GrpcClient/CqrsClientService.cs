// REPO-PFAD: Infrastructure/GrpcClient/CqrsClientService.cs  (MODIFIZIERT)
using System.Collections.Concurrent;
using Abstractions;
using Domain.Projections;
using Grpc.Core;
using Infrastructure.Mapping;
using Infrastructure.Pipeline;
using Infrastructure.PubSub;
using Infrastructure.Serialization;
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

    public CqrsClientServiceImpl(
        ActorSystem actorSystem,
        ProtoMessageMapper mapper,
        IAggregateDispatcher dispatcher,
        ProjectionQueryService queryService,
        TriggerHandlerRegistry triggerHandlerRegistry,
        QueryHandlerRegistry queryHandlerRegistry,
        BrokerPublisher publisher)
    {
        _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _triggerHandlerRegistry = triggerHandlerRegistry ?? throw new ArgumentNullException(nameof(triggerHandlerRegistry));
        _queryHandlerRegistry = queryHandlerRegistry ?? throw new ArgumentNullException(nameof(queryHandlerRegistry));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _capabilitiesHandler = new CapabilitiesHandler();
    }

    public override async Task Connect(
        IAsyncStreamReader<ProtoRepo.ClientMessage> requestStream,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        ServerCallContext context)
    {
        var sessionId = $"session-{Interlocked.Increment(ref _sessionCounter):D4}";
        var ct = context.CancellationToken;
        
        Console.WriteLine($"[CqrsService] New connection: {sessionId}");
        Console.WriteLine($"[CqrsService]   Peer: {context.Peer}");

        PID? proxyPid = null;

        try
        {
            // 1. EventProxyActor spawnen (für PID)
            var proxyProps = Props.FromProducer(() => 
                new EventProxyActor(responseStream, _mapper, sessionId));
            proxyPid = _actorSystem.Root.Spawn(proxyProps);
            
            Console.WriteLine($"[CqrsService] EventProxy spawned: {proxyPid}");

            // 2. SubscriptionTracker erstellen (await using = automatisches Cleanup)
            await using var subscriptionTracker = new SubscriptionTracker(
                _actorSystem.Cluster(),
                proxyPid,
                sessionId);

            // 3. Read-Loop
            Console.WriteLine($"[CqrsService] Entering read loop...");
            
            while (await requestStream.MoveNext(ct))
            {
                var clientMessage = requestStream.Current;
                await ProcessMessageAsync(
                    clientMessage, responseStream, subscriptionTracker,
                    proxyPid,
                    sessionId, ct);
            }
            
            Console.WriteLine($"[CqrsService] Client closed stream normally");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[CqrsService] {sessionId} cancelled");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine($"[CqrsService] {sessionId} client disconnected");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} error: {ex.Message}");
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
                    Console.WriteLine($"[CqrsService] EventProxy stopped");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CqrsService] Error stopping proxy: {ex.Message}");
                }
            }
            
            Console.WriteLine($"[CqrsService] {sessionId} disconnected");
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
                    Console.WriteLine($"[CqrsService] {sessionId} unknown message type: {message.MessageCase}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} error processing message: {ex.Message}");
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
        Console.WriteLine($"[CqrsService] {sessionId} ← Capabilities: {messageSource}");

        if (request.HandleTriggers.Any())
            Console.WriteLine($"[CqrsService] {sessionId}   handle_triggers: [{string.Join(", ", request.HandleTriggers)}]");
        if (request.HandleQueries.Any())
            Console.WriteLine($"[CqrsService] {sessionId}   handle_queries: [{string.Join(", ", request.HandleQueries)}]");

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
                    Console.WriteLine($"[CqrsService] {sessionId} WARNING: Could not subscribe to {eventTypeName}");
                }
            }

            // 3. IMMER für CommandFailed subscriben (Targeted Delivery für diesen Client)
            var commandFailedSubscribed = await subscriptionTracker.SubscribeAsync("CommandFailed", ct);
            if (commandFailedSubscribed)
            {
                Console.WriteLine($"[CqrsService] {sessionId} Auto-subscribed to CommandFailed");
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
                Console.WriteLine($"[CqrsService] {sessionId} WARNING: Unknown types: [{string.Join(", ", result.UnknownTypes)}]");
            }

            // 7. Response senden
            var response = _capabilitiesHandler.BuildResponse(result);
            var serverMessage = new ProtoRepo.ServerMessage
            {
                CapabilitiesResponse = response
            };

            await responseStream.WriteAsync(serverMessage, ct);

            Console.WriteLine($"[CqrsService] {sessionId} → CapabilitiesResponse: " +
                $"{result.AllowedCommands.Count} commands, " +
                $"{result.SubscribedEvents.Count} events, " +
                $"{result.AllowedTriggers.Count} triggers, " +
                $"{result.AllowedQueries.Count} queries" +
                (result.HandlingTriggers.Count > 0 ? $", handling {result.HandlingTriggers.Count} triggers" : "") +
                (result.HandlingQueries.Count > 0 ? $", handling {result.HandlingQueries.Count} queries" : ""));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} capabilities failed: {ex.Message}");
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
        Console.WriteLine($"[CqrsService] {sessionId} ← Command");

        try
        {
            var envelope = _mapper.MapToDomain(request.Envelope);
            envelope = envelope with { OriginSessionId = sessionId };
        
            Console.WriteLine($"[CqrsService] {sessionId} Command: {envelope.Payload.GetType().Name}, CorrelationId: {envelope.CorrelationId}");

            _dispatcher.Dispatch(envelope);
        
            Console.WriteLine($"[CqrsService] {sessionId} Command dispatched (fire-and-forget)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} Command mapping failed: {ex.Message}");
        
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
        Console.WriteLine($"[CqrsService] {sessionId} ← Trigger");

        try
        {
            var trigger = _mapper.MapToDomain(request.Payload);
            var triggerTypeName = trigger.GetType().Name;
            
            Console.WriteLine($"[CqrsService] {sessionId} Trigger type: {triggerTypeName}");

            // NEU: Erst TriggerHandlerRegistry prüfen (Client-Handler)
            var handlerPid = _triggerHandlerRegistry.GetHandler(triggerTypeName);
            if (handlerPid != null)
            {
                Console.WriteLine($"[CqrsService] {sessionId} Forwarding trigger to client handler: {handlerPid}");
                
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
                    Console.WriteLine($"[CqrsService] {sessionId} Trigger forward timed out");
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
                Console.WriteLine($"[CqrsService] {sessionId} No handler for trigger: {triggerType.Name}");
                
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
            
            Console.WriteLine($"[CqrsService] {sessionId} → TriggerAck: {(ack?.Accepted ?? false ? "accepted" : "rejected")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} trigger failed: {ex.Message}");
            
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
        Console.WriteLine($"[CqrsService] {sessionId} ← Query");

        try
        {
            var query = _mapper.MapToDomain(request.Payload);
            var queryTypeName = query.GetType().Name;
            
            Console.WriteLine($"[CqrsService] {sessionId} Query type: {queryTypeName}");

            // NEU: Erst QueryHandlerRegistry prüfen (Client-Handler)
            var handlerPid = _queryHandlerRegistry.GetHandler(queryTypeName);
            if (handlerPid != null)
            {
                Console.WriteLine($"[CqrsService] {sessionId} Forwarding query to client handler: {handlerPid}");

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

                    Console.WriteLine($"[CqrsService] {sessionId} → QueryResponse (from client handler)");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    Console.WriteLine($"[CqrsService] {sessionId} Query forward timed out");
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
            
            Console.WriteLine($"[CqrsService] {sessionId} → QueryResponse: {response.Data.GetType().Name}");
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} query not supported: {ex.Message}");
            await SendErrorAsync(responseStream, "QUERY_NOT_SUPPORTED", ex.Message, request.CorrelationId, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} query failed: {ex.Message}");
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
        Console.WriteLine($"[CqrsService] {sessionId} ← TransientEvent");

        try
        {
            var envelope = _mapper.MapToDomain(request.Envelope);

            if (envelope.Payload is not ITransientEvent)
            {
                Console.WriteLine($"[CqrsService] {sessionId} REJECTED: {envelope.Payload.GetType().Name} is not ITransientEvent");
                await SendErrorAsync(responseStream, "INVALID_TRANSIENT_EVENT",
                    $"{envelope.Payload.GetType().Name} does not implement ITransientEvent",
                    "", ct);
                return;
            }

            await _publisher.PublishAsync(envelope, ct);

            Console.WriteLine($"[CqrsService] {sessionId} TransientEvent published: {envelope.Payload.GetType().Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} TransientEvent failed: {ex.Message}");
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
        Console.WriteLine($"[CqrsService] {sessionId} ← QueryAnswer, CorrelationId: {answer.CorrelationId}");

        if (_pendingQueryForwards.TryGetValue(answer.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(answer);
        }
        else
        {
            Console.WriteLine(
                $"[CqrsService] {sessionId} WARNING: QueryAnswer for unknown CorrelationId: {answer.CorrelationId}");
        }
    }

    // =========================================================================
    // TRIGGER RESULT FROM CLIENT (NEU)
    // =========================================================================

    private void HandleTriggerResult(
        ProtoRepo.TriggerResult result,
        string sessionId)
    {
        Console.WriteLine($"[CqrsService] {sessionId} ← TriggerResult, CorrelationId: {result.CorrelationId}");

        if (_pendingTriggerForwards.TryGetValue(result.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
        else
        {
            Console.WriteLine(
                $"[CqrsService] {sessionId} WARNING: TriggerResult for unknown CorrelationId: {result.CorrelationId}");
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
        Console.WriteLine($"[CqrsService] {sessionId} ← Subscribe: {request.EventType}");

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
            
            Console.WriteLine($"[CqrsService] {sessionId} → SubscriptionConfirmed: {request.EventType}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} subscribe failed: {ex.Message}");
            
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
        Console.WriteLine($"[CqrsService] {sessionId} ← Unsubscribe: {request.EventType}");

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
            
            Console.WriteLine($"[CqrsService] {sessionId} → UnsubscriptionConfirmed: {request.EventType}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} unsubscribe failed: {ex.Message}");
            
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
