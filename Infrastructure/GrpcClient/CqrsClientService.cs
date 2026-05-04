using Abstractions;
using Domain.Projections;
using Grpc.Core;
using Infrastructure.Mapping;
using Infrastructure.Serialization;
using Proto;
using Proto.Cluster;

namespace Infrastructure.GrpcClient;

/// <summary>
/// gRPC Service fÃ¼r bidirektionale Client-Kommunikation.
/// 
/// DESIGN:
/// - Read-Loop direkt im Service (nutzt Kestrel's Thread-Pool)
/// - Nur EIN Actor pro Verbindung: EventProxyActor (existiert nur fÃ¼r PID)
/// - SubscriptionTracker als normales Objekt (kein Actor)
/// - Cleanup im finally-Block (deterministisch, kein Actor-Messaging)
/// 
/// Lifecycle einer Verbindung:
/// 1. Client ruft Connect() auf
/// 2. Service spawnt EventProxyActor (fÃ¼r PID)
/// 3. Service erstellt SubscriptionTracker
/// 4. Read-Loop verarbeitet ClientMessages
/// 5. finally-Block: Subscriptions beenden, Actor stoppen
/// </summary>
public class CqrsClientServiceImpl : ProtoRepo.CqrsClientService.CqrsClientServiceBase
{
    private readonly ActorSystem _actorSystem;
    private readonly ProtoMessageMapper _mapper;
    private readonly IAggregateDispatcher _dispatcher;
    private readonly CapabilitiesHandler _capabilitiesHandler;
    private readonly ProjectionQueryService _queryService;
    
    private static int _sessionCounter = 0;

    public CqrsClientServiceImpl(
        ActorSystem actorSystem,
        ProtoMessageMapper mapper,
        IAggregateDispatcher dispatcher,
        ProjectionQueryService queryService)
    {
        _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
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
            // 1. EventProxyActor spawnen (fÃ¼r PID)
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
                await ProcessMessageAsync(clientMessage, responseStream, subscriptionTracker, sessionId, ct);
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
            // 4. Cleanup: Actor stoppen
            // SubscriptionTracker.DisposeAsync() wird automatisch aufgerufen (await using)
            if (proxyPid != null)
            {
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
                    await HandleCapabilitiesAsync(message.Capabilities, responseStream, subscriptionTracker, sessionId, ct);
                    break;

                case ProtoRepo.ClientMessage.MessageOneofCase.Query:
                    await HandleQueryAsync(message.Query, responseStream, sessionId, ct);
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
    string sessionId,
    CancellationToken ct)
{
    Console.WriteLine($"[CqrsService] {sessionId} â† Capabilities: [{string.Join(", ", request.EventTypes)}]");

    try
    {
        // 1. Capabilities ermitteln
        var result = _capabilitiesHandler.Handle(request, sessionId);

        // 2. FÃ¼r jeden gÃ¼ltigen Event-Typ subscriben
        foreach (var eventTypeName in result.SubscribedEvents)
        {
            var subscribeSuccess = await subscriptionTracker.SubscribeAsync(eventTypeName, ct);
            if (!subscribeSuccess)
            {
                Console.WriteLine($"[CqrsService] {sessionId} WARNING: Could not subscribe to {eventTypeName}");
            }
        }

        // 3. NEU: IMMER fÃ¼r CommandFailed subscriben (Targeted Delivery fÃ¼r diesen Client)
        var commandFailedSubscribed = await subscriptionTracker.SubscribeAsync("CommandFailed", ct);
        if (commandFailedSubscribed)
        {
            Console.WriteLine($"[CqrsService] {sessionId} Auto-subscribed to CommandFailed");
        }

        // 4. Unbekannte Event-Typen loggen
        if (result.UnknownEventTypes.Any())
        {
            Console.WriteLine($"[CqrsService] {sessionId} WARNING: Unknown event types: [{string.Join(", ", result.UnknownEventTypes)}]");
        }

        // 5. Response senden
        var response = _capabilitiesHandler.BuildResponse(result);
        var serverMessage = new ProtoRepo.ServerMessage
        {
            CapabilitiesResponse = response
        };

        await responseStream.WriteAsync(serverMessage, ct);

        Console.WriteLine($"[CqrsService] {sessionId} â†’ CapabilitiesResponse: {result.AllowedCommands.Count} commands, {result.SubscribedEvents.Count} events, {result.SupportedQueries.Count} queries");
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
        Console.WriteLine($"[CqrsService] {sessionId} â† Command");

        try
        {
            // 1. DTO â†’ Domain mappen
            var envelope = _mapper.MapToDomain(request.Envelope);
        
            // 2. NEU: OriginSessionId fÃ¼r Targeted Delivery setzen
            envelope = envelope with { OriginSessionId = sessionId };
        
            Console.WriteLine($"[CqrsService] {sessionId} Command: {envelope.Payload.GetType().Name}, CorrelationId: {envelope.CorrelationId}");

            // 3. Fire-and-Forget dispatch - KEIN Warten auf Result!
            _dispatcher.Dispatch(envelope);
        
            Console.WriteLine($"[CqrsService] {sessionId} Command dispatched (fire-and-forget)");
        
            // KEIN CommandAccepted mehr!
            // KEIN ErrorResponse bei Command-Fehlern!
            // Alles kommt als Event Ã¼ber PubSub:
            // - Erfolg: Domain-Events (Broadcast)
            // - Fehler: CommandFailed Event (Targeted an diesen Client)
        }
        catch (Exception ex)
        {
            // Nur bei Mapping-Fehlern (Command kam gar nicht beim Actor an)
            Console.WriteLine($"[CqrsService] {sessionId} Command mapping failed: {ex.Message}");
        
            // Bei Mapping-Fehlern kann der Actor kein CommandFailed publishen,
            // also hier eine ErrorResponse senden
            await SendErrorAsync(
                responseStream,
                "COMMAND_MAPPING_FAILED",
                ex.Message,
                request.Envelope?.CorrelationId ?? "",
                ct);
        }
    }
    // =========================================================================
    // QUERY HANDLING
    // =========================================================================

    private async Task HandleQueryAsync(
        ProtoRepo.QueryRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        string sessionId,
        CancellationToken ct)
    {
        Console.WriteLine($"[CqrsService] {sessionId} â† Query");

        try
        {
            // 1. DTO â†’ Domain mappen
            var query = _mapper.MapToDomain(request.Payload);
            
            Console.WriteLine($"[CqrsService] {sessionId} Query type: {query.GetType().Name}");

            // 2. Query ausfÃ¼hren
            var response = await _queryService.ExecuteAsync(query);

            // 3. Domain → DTO mappen (★ Phase 4: Deps werden mittransportiert)
            var responseDto = _mapper.ToQueryResponse(response, request.CorrelationId);

            // 4. Response senden
            var serverMessage = new ProtoRepo.ServerMessage
            {
                QueryResponse = responseDto
            };
            
            await responseStream.WriteAsync(serverMessage, ct);
            
            Console.WriteLine($"[CqrsService] {sessionId} â†’ QueryResponse: {response.Data.GetType().Name}");
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} query not supported: {ex.Message}");
            
            await SendErrorAsync(
                responseStream,
                "QUERY_NOT_SUPPORTED",
                ex.Message,
                request.CorrelationId,
                ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CqrsService] {sessionId} query failed: {ex.Message}");
            
            await SendErrorAsync(
                responseStream,
                "QUERY_FAILED",
                ex.Message,
                request.CorrelationId,
                ct);
        }
    }

    // =========================================================================
    // SUBSCRIBE HANDLING
    // =========================================================================

    private async Task HandleSubscribeAsync(
        ProtoRepo.SubscribeRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        string sessionId,
        CancellationToken ct)
    {
        Console.WriteLine($"[CqrsService] {sessionId} â† Subscribe: {request.EventType}");

        try
        {
            // 1. Subscribe via Tracker
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

            // 2. SubscriptionConfirmed senden
            var confirmed = new ProtoRepo.ServerMessage
            {
                SubscriptionConfirmed = new ProtoRepo.SubscriptionConfirmed
                {
                    EventType = request.EventType,
                    AggregateId = request.AggregateId // Wird im MVP ignoriert (Client filtert)
                }
            };
            
            await responseStream.WriteAsync(confirmed, ct);
            
            Console.WriteLine($"[CqrsService] {sessionId} â†’ SubscriptionConfirmed: {request.EventType}");
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
    // UNSUBSCRIBE HANDLING
    // =========================================================================

    private async Task HandleUnsubscribeAsync(
        ProtoRepo.UnsubscribeRequest request,
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        SubscriptionTracker subscriptionTracker,
        string sessionId,
        CancellationToken ct)
    {
        Console.WriteLine($"[CqrsService] {sessionId} â† Unsubscribe: {request.EventType}");

        try
        {
            // 1. Unsubscribe via Tracker
            await subscriptionTracker.UnsubscribeAsync(request.EventType, ct);

            // 2. UnsubscriptionConfirmed senden (auch wenn nicht subscribed war)
            var confirmed = new ProtoRepo.ServerMessage
            {
                UnsubscriptionConfirmed = new ProtoRepo.UnsubscriptionConfirmed
                {
                    EventType = request.EventType,
                    AggregateId = request.AggregateId
                }
            };
            
            await responseStream.WriteAsync(confirmed, ct);
            
            Console.WriteLine($"[CqrsService] {sessionId} â†’ UnsubscriptionConfirmed: {request.EventType}");
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
    // HELPERS
    // =========================================================================

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
            // Stream mÃ¶glicherweise bereits geschlossen - ignorieren
        }
    }
}