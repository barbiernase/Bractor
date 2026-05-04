using Abstractions;
using Core;
using Infrastructure.Projections;
using Proto;
using Proto.Cluster;

namespace Infrastructure.PubSub;

/// <summary>
/// Basis-Klasse für Subscriber-Actors.
/// Handhabt Lifecycle (Subscribe/Unsubscribe) und delegiert an Logik-Klasse.
/// 
/// ★ Phase 3: DispatchAsync nutzt IAggregateEnvelope statt IMessageEnvelope.
///   Events haben immer Aggregate-Kontext (AggregateId, AggregateType).
///   Pattern-Match auf IAggregateEnvelope statt IMessageEnvelope.
/// </summary>
public abstract class SubscriberActorBase<TSubscriber> : IActor
    where TSubscriber : ISubscriber
{
    protected readonly TSubscriber _logic;
    private readonly BrokerPublisher? _publisher;
    private readonly ReadModelDepsWriter? _depsWriter;
    private BrokerSubscription? _subscription;

    protected SubscriberActorBase(
        TSubscriber logic,
        BrokerPublisher? publisher = null,
        ReadModelDepsWriter? depsWriter = null)
    {
        _logic = logic ?? throw new ArgumentNullException(nameof(logic));
        _publisher = publisher;
        _depsWriter = depsWriter;
    }

    public async Task ReceiveAsync(IContext context)
    {
        try
        {
            switch (context.Message)
            {
                case Started:
                    await OnStartedAsync(context);
                    break;

                // ★ Phase 3: Events kommen immer als IAggregateEnvelope
                // (EventEnvelope : IAggregateEnvelope)
                case IAggregateEnvelope envelope:
                    await OnEnvelopeAsync(envelope, context.CancellationToken);
                    break;

                case Stopping:
                    await OnStoppingAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_logic.SubscriberId}] ERROR: {ex.Message}");
        }
    }

    private async Task OnStartedAsync(IContext context)
    {
        Console.WriteLine($"[{_logic.SubscriberId}] Starting...");
        
        _subscription = new BrokerSubscription(
            context.System.Cluster(),
            _logic.SubscriberId,
            context.Self);
        
        var messageTypes = GetSubscribedMessageTypes();
        foreach (var type in messageTypes)
        {
            await _subscription.SubscribeAsync(type);
            Console.WriteLine($"[{_logic.SubscriberId}] Subscribed to {type.Name}");
        }
        
        await _logic.OnInitializeAsync();
        Console.WriteLine($"[{_logic.SubscriberId}] Ready");
    }

    private async Task OnEnvelopeAsync(IAggregateEnvelope envelope, CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"[{_logic.SubscriberId}] Received {envelope.Payload.GetType().Name}");
            
            var writer = new ProjectionWriter();
            
            await DispatchAsync(envelope, writer, async evt =>
            {
                if (_publisher == null)
                {
                    Console.WriteLine($"[{_logic.SubscriberId}] WARNING: Reactive event {evt.GetType().Name} produced but no publisher available");
                    return;
                }
                
                // Reaktive Events erben Aggregate-Kontext vom auslösenden Event
                var newEnvelope = new EventEnvelope
                {
                    Payload = evt,
                    AggregateId = envelope.AggregateId,
                    AggregateType = envelope.AggregateType,
                    CorrelationId = envelope.CorrelationId,
                    CausationId = envelope.MessageId.ToString(),
                    UserId = envelope.UserId
                };
                
                await _publisher.PublishAsync(newEnvelope, ct);
                Console.WriteLine($"[{_logic.SubscriberId}] → Published reactive event: {evt.GetType().Name}");
            });

            // Deps in Redis schreiben
            if (writer.HasResults && _depsWriter != null && envelope is EventEnvelope eventEnvelope)
            {
                await _depsWriter.WriteAsync(
                    _logic.SubscriberId,
                    eventEnvelope,
                    writer.GetResults(),
                    ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_logic.SubscriberId}] Error handling {envelope.Payload.GetType().Name}: {ex.Message}");
        }
    }

    private async Task OnStoppingAsync()
    {
        Console.WriteLine($"[{_logic.SubscriberId}] Stopping...");
        await _logic.OnShutdownAsync();
        
        if (_subscription != null)
        {
            await _subscription.UnsubscribeAllAsync();
        }
        
        Console.WriteLine($"[{_logic.SubscriberId}] Stopped");
    }

    protected abstract IReadOnlyList<Type> GetSubscribedMessageTypes();

    /// <summary>
    /// Dispatcht an den richtigen Handler.
    /// 
    /// ★ Phase 3: IAggregateEnvelope — Events haben immer Aggregate-Kontext.
    /// Der emit-Callback wird nur aufgerufen wenn ein Handler reaktive Events produziert.
    /// </summary>
    protected abstract Task DispatchAsync(
        IAggregateEnvelope envelope,
        ProjectionWriter writer,
        Func<IEvent, Task> emit);
}