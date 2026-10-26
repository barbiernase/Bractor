// REPO-PFAD: Infrastructure/GrpcClient/EventProxyActor.cs  (MODIFIZIERT)
using Abstractions;
using Grpc.Core;
using Infrastructure.Lebenszyklus;
using Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto;

namespace Infrastructure.GrpcClient;

// ═══════════════════════════════════════════════════
// INTERNE MESSAGE-TYPEN für Actor-Kommunikation
// ═══════════════════════════════════════════════════

/// <summary>
/// Server → ClientProxy: Query an den Client weiterleiten.
/// CqrsClientServiceImpl sendet diesen Typ und wartet auf QueryAnswerMsg.
/// </summary>
internal record QueryForwardMsg(IQuery Query, string CorrelationId);

/// <summary>
/// Server → Client: eine fertige ServerMessage über DIESEN Actor schreiben — so laufen Schreibvorgänge aus Hintergrund-Schleifen
/// (z. B. ArbeitsAufträge der Funktions-Anbieter-Sitzung) durch dieselbe Mailbox wie Events und Forwards (ein Schreiber).
/// </summary>
internal record ServerNachrichtMsg(Cqrs.Leitung.ServerMessage Nachricht);

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
/// - QueryForwardMsg empfangen          → ServerMessage.QueryForward   (NEU)
/// - In gRPC Stream schreiben
/// 
/// NICHT-Verantwortlichkeiten:
/// - Kein Subscription-Management (macht SubscriptionTracker)
/// - Kein Filtering (macht Client)
/// - Keine Response-Korrelation (macht CqrsClientServiceImpl)
/// - Kein State außer Stream-Referenz
/// </summary>
public class EventProxyActor : RahmenActor
{
    private readonly IServerStreamWriter<Cqrs.Leitung.ServerMessage> _responseStream;
    private readonly ProtoMessageMapper _mapper;
    private readonly string _sessionId;
    private readonly ILogger _logger;

    public EventProxyActor(
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        ProtoMessageMapper mapper,
        string sessionId,
        ILogger? logger = null)
        : base(logger)
    {
        _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _sessionId = sessionId;
        _logger = logger ?? NullLogger.Instance;
    }

    protected override Task BeginneAsync(IContext context)
    {
        _logger.LogDebug("[EventProxy-{Session}] Started", _sessionId);
        return Task.CompletedTask;
    }

    protected override Task BeimStoppenAsync(IContext context)
    {
        _logger.LogDebug("[EventProxy-{Session}] Stopping", _sessionId);
        return Task.CompletedTask;
    }

    protected override async Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case EventEnvelope envelope:
                await HandleEventEnvelopeAsync(envelope);
                break;

            case QueryForwardMsg queryFwd:
                await HandleQueryForwardAsync(queryFwd);
                break;

            case ServerNachrichtMsg nachricht:
                await HandleServerNachrichtAsync(nachricht);
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
            
            var serverMessage = new Cqrs.Leitung.ServerMessage
            {
                Event = new Cqrs.Leitung.EventNotification
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
    // FERTIGE SERVER-NACHRICHT → STREAM (Funktions-Aufträge u. a.)
    // ═══════════════════════════════════════════════════

    private async Task HandleServerNachrichtAsync(ServerNachrichtMsg msg)
    {
        try
        {
            await _responseStream.WriteAsync(msg.Nachricht);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug("[EventProxy-{Session}] Stream cancelled during {Case}", _sessionId, msg.Nachricht.MessageCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventProxy-{Session}] Error writing {Case}", _sessionId, msg.Nachricht.MessageCase);
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

            var serverMessage = new Cqrs.Leitung.ServerMessage
            {
                QueryForward = new Cqrs.Leitung.QueryForward
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
