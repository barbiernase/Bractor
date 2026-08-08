// REPO-PFAD: Infrastructure/GrpcClient/EventProxyActor.cs  (MODIFIZIERT)
using Abstractions;
using Grpc.Core;
using Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger _logger;

    public EventProxyActor(
        IServerStreamWriter<ProtoRepo.ServerMessage> responseStream,
        ProtoMessageMapper mapper,
        string sessionId,
        ILogger? logger = null)
    {
        _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _sessionId = sessionId;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                _logger.LogDebug("[EventProxy-{Session}] Started", _sessionId);
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
                _logger.LogDebug("[EventProxy-{Session}] Stopping", _sessionId);
                break;

            case Stopped:
                _logger.LogDebug("[EventProxy-{Session}] Stopped", _sessionId);
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
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug("[EventProxy-{Session}] Stream cancelled (client disconnected)", _sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventProxy-{Session}] Error writing to stream", _sessionId);
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

            _logger.LogDebug("[EventProxy-{Session}] Forwarded trigger {Trigger}, CorrelationId {CorrelationId}",
                _sessionId, msg.Trigger.GetType().Name, msg.CorrelationId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug("[EventProxy-{Session}] Stream cancelled during trigger forward", _sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventProxy-{Session}] Error forwarding trigger", _sessionId);
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

            _logger.LogDebug("[EventProxy-{Session}] Forwarded query {Query}, CorrelationId {CorrelationId}",
                _sessionId, msg.Query.GetType().Name, msg.CorrelationId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug("[EventProxy-{Session}] Stream cancelled during query forward", _sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventProxy-{Session}] Error forwarding query", _sessionId);
        }
    }
}
