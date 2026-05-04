using Abstractions;
using Grpc.Core;
using Infrastructure.Serialization;
using Proto;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Minimaler Actor pro gRPC-Verbindung.
/// 
/// EXISTIERT NUR FÜR DIE PID!
/// 
/// Der BrokerShard braucht eine PID um Events zu senden.
/// Dieser Actor ist der "Briefkasten" der Events empfängt
/// und in den gRPC Stream schreibt.
/// 
/// Verantwortlichkeiten:
/// - EventEnvelope vom PubSub empfangen
/// - In ServerMessage mappen
/// - In gRPC Stream schreiben
/// 
/// NICHT-Verantwortlichkeiten:
/// - Kein Subscription-Management (macht SubscriptionTracker)
/// - Kein Filtering (macht Client)
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

            case Stopping:
                Console.WriteLine($"[EventProxy-{_sessionId}] Stopping");
                break;

            case Stopped:
                Console.WriteLine($"[EventProxy-{_sessionId}] Stopped");
                break;
        }
    }

    private async Task HandleEventEnvelopeAsync(EventEnvelope envelope)
    {
        try
        {
            // 1. Domain → DTO mappen
            var eventDto = _mapper.MapToDto(envelope);
            
            // 2. ServerMessage erstellen
            var serverMessage = new ProtoRepo.ServerMessage
            {
                Event = new ProtoRepo.EventNotification
                {
                    Envelope = eventDto
                }
            };
            
            // 3. In Stream schreiben
            await _responseStream.WriteAsync(serverMessage);
            
            Console.WriteLine($"[EventProxy-{_sessionId}] Sent {envelope.Payload.GetType().Name}");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            // Client hat Verbindung geschlossen - normal bei Disconnect
            Console.WriteLine($"[EventProxy-{_sessionId}] Stream cancelled (client disconnected)");
        }
        catch (Exception ex)
        {
            // Andere Fehler loggen aber nicht crashen
            Console.WriteLine($"[EventProxy-{_sessionId}] Error writing to stream: {ex.Message}");
        }
    }
}