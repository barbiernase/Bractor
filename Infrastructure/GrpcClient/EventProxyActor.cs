// REPO-PFAD: Infrastructure/GrpcClient/EventProxyActor.cs  (MODIFIZIERT)
using Abstractions;
using Grpc.Core;
using Infrastructure.Serialization;
using Proto;

namespace Infrastructure.GrpcClient;

// ═══════════════════════════════════════════════════
// INTERNE MESSAGE-TYPEN für Actor-Kommunikation
// ═══════════════════════════════════════════════════

/// <summary>
/// Server → ClientProxy: Trigger an den Client weiterleiten.
/// CqrsClientServiceImpl sendet diesen Typ an den Actor.
/// </summary>
internal record TriggerForwardMsg(IPipelineTrigger Trigger, string CorrelationId);

/// <summary>
/// Server → ClientProxy: Query an den Client weiterleiten.
/// CqrsClientServiceImpl sendet diesen Typ und wartet auf QueryAnswerMsg.
/// </summary>
internal record QueryForwardMsg(IQuery Query, string CorrelationId);

/// <summary>
/// Minimaler Actor pro gRPC-Verbindung.
/// 
/// EXISTIERT NUR FÜR DIE PID!
/// 
/// Der BrokerShard braucht eine PID um Events zu senden.
/// Dieser Actor ist der "Briefkasten" der Messages empfängt
/// und in den gRPC Stream schreibt.
/// 
/// Verantwortlichkeiten:
/// - EventEnvelope vom PubSub empfangen → ServerMessage.Event
/// - TriggerForwardMsg empfangen       → ServerMessage.TriggerForward  (NEU)
/// - QueryForwardMsg empfangen          → ServerMessage.QueryForward   (NEU)
/// - In gRPC Stream schreiben
/// 
/// NICHT-Verantwortlichkeiten:
/// - Kein Subscription-Management (macht SubscriptionTracker)
/// - Kein Filtering (macht Client)
/// - Keine Response-Korrelation (macht CqrsClientServiceImpl)
/// - Kein State außer Stream-Referenz
/// </summary>
public class EventProxyActor : IActor
{
    private readonly IServerStreamWriter<ProtoRepo.ServerMessage> _responseStream;
    private readonly ProtoMessageMapper _mapper;
    private readonly string _sessionId;

    public EventProxyActor(
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        ProtoMessageMapper mapper,
        string sessionId)
    {
        _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _sessionId = sessionId;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                Console.WriteLine($"[EventProxy-{_sessionId}] Started");
                break;

            case EventEnvelope envelope:
                await HandleEventEnvelopeAsync(envelope);
                break;

            case TriggerForwardMsg triggerFwd:
                await HandleTriggerForwardAsync(triggerFwd);
                break;

            case QueryForwardMsg queryFwd:
                await HandleQueryForwardAsync(queryFwd);
                break;

            case Stopping:
                Console.WriteLine($"[EventProxy-{_sessionId}] Stopping");
                break;

            case Stopped:
                Console.WriteLine($"[EventProxy-{_sessionId}] Stopped");
                break;
        }
    }

    // ═══════════════════════════════════════════════════
    // EVENT → STREAM (bestehend)
    // ═══════════════════════════════════════════════════

    private async Task HandleEventEnvelopeAsync(EventEnvelope envelope)
    {
        try
        {
            var eventDto = _mapper.MapToDto(envelope);
            
            var serverMessage = new ProtoRepo.ServerMessage
            {
                Event = new ProtoRepo.EventNotification
                {
                    Envelope = eventDto
                }
            };
            
            await _responseStream.WriteAsync(serverMessage);
            
            Console.WriteLine($"[EventProxy-{_sessionId}] Sent {envelope.Payload.GetType().Name}");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine($"[EventProxy-{_sessionId}] Stream cancelled (client disconnected)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventProxy-{_sessionId}] Error writing to stream: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════
    // TRIGGER FORWARD → STREAM (NEU)
    // ═══════════════════════════════════════════════════

    private async Task HandleTriggerForwardAsync(TriggerForwardMsg msg)
    {
        try
        {
            var triggerDto = _mapper.MapToDto(msg.Trigger);

            var serverMessage = new ProtoRepo.ServerMessage
            {
                TriggerForward = new ProtoRepo.TriggerForward
                {
                    Payload = triggerDto,
                    CorrelationId = msg.CorrelationId
                }
            };

            await _responseStream.WriteAsync(serverMessage);

            Console.WriteLine(
                $"[EventProxy-{_sessionId}] Forwarded trigger {msg.Trigger.GetType().Name}, " +
                $"CorrelationId: {msg.CorrelationId}");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine($"[EventProxy-{_sessionId}] Stream cancelled during trigger forward");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[EventProxy-{_sessionId}] Error forwarding trigger: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════
    // QUERY FORWARD → STREAM (NEU)
    // ═══════════════════════════════════════════════════

    private async Task HandleQueryForwardAsync(QueryForwardMsg msg)
    {
        try
        {
            var queryDto = _mapper.MapToDto(msg.Query);

            var serverMessage = new ProtoRepo.ServerMessage
            {
                QueryForward = new ProtoRepo.QueryForward
                {
                    CorrelationId = msg.CorrelationId,
                    Payload = queryDto
                }
            };

            await _responseStream.WriteAsync(serverMessage);

            Console.WriteLine(
                $"[EventProxy-{_sessionId}] Forwarded query {msg.Query.GetType().Name}, " +
                $"CorrelationId: {msg.CorrelationId}");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine($"[EventProxy-{_sessionId}] Stream cancelled during query forward");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[EventProxy-{_sessionId}] Error forwarding query: {ex.Message}");
        }
    }
}
