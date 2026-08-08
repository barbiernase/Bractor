using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;

namespace Infrastructure.PubSub;

/// <summary>
/// Das Ausgabe-Routing eines Handlers (Spec 4.6) — herausgezogen aus dem Push-Actor, sodass
/// BEIDE Transporte (Push-Subscriber und Pull-Adapter) dieselbe Logik nutzen:
///   <c>IEvent</c>   → reaktives Event re-publishen (Broker),
///   <c>ICommand</c> → Reaktion an ein Fremd-Aggregat (deterministische CommandId + OCC-Retry;
///                     die Wirksamkeit sichert der Noop-Decider des Empfängers, Spec 9.3).
///
/// Context-frei: der <see cref="Cluster"/> kommt aus dem <c>ActorSystem</c>, nicht aus einem
/// per-Message-Context — deshalb aus dem Pull-Adapter (der keinen Message-Context hat) nutzbar.
/// </summary>
public sealed class HandlerOutputRouter
{
    private readonly ICommandEmitter _emitter;
    private readonly BrokerPublisher? _publisher;
    private readonly string _subscriberId;
    private readonly ILogger _logger;

    public HandlerOutputRouter(Cluster cluster, BrokerPublisher? publisher, string subscriberId, ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _emitter = new CommandEmitter(cluster, _logger);   // ★ P3: das EINE Emit-Primitiv (EM-1)
        _publisher = publisher;
        _subscriberId = subscriberId;
    }

    /// <summary>Das <c>emit</c> für EIN auslösendes Event — Ausgaben erben dessen Kontext.</summary>
    public Func<IPipelineOutput, Task> EmitFor(IAggregateEnvelope trigger, CancellationToken ct)
        => payload => RouteAsync(payload, trigger, ct);

    private async Task RouteAsync(IPipelineOutput payload, IAggregateEnvelope trigger, CancellationToken ct)
    {
        switch (payload)
        {
            case ICommand command:
                await SendReaktionAsync(command, trigger, ct);
                break;
            case IEvent evt:
                await PublishReactiveAsync(evt, trigger, ct);
                break;
        }
        // Prozess-Start läuft NICHT mehr über eine Plan-Yield-Route: der KorrelationsRouter startet
        // den Prozess direkt aus dem Auslöse-Event (dessen Signal abonniert er). Kein Handler-Glue nötig.
    }

    private async Task PublishReactiveAsync(IEvent evt, IAggregateEnvelope trigger, CancellationToken ct)
    {
        if (_publisher == null)
        {
            _logger.LogWarning("[{Subscriber}] reaktives Event {Event} ohne Publisher", _subscriberId, evt.GetType().Name);
            return;
        }

        // Reaktive Events erben den Aggregate-Kontext vom auslösenden Event.
        var newEnvelope = new EventEnvelope
        {
            Payload = evt,
            AggregateId = trigger.AggregateId,
            AggregateType = trigger.AggregateType,
            CorrelationId = trigger.CorrelationId,
            CausationId = trigger.MessageId.ToString(),
            UserId = trigger.UserId
        };

        await _publisher.PublishAsync(newEnvelope, ct);
        _logger.LogDebug("[{Subscriber}] → reaktives Event: {Event}", _subscriberId, evt.GetType().Name);
    }

    private Task SendReaktionAsync(ICommand command, IAggregateEnvelope trigger, CancellationToken ct)
    {
        // ★ P3: über das EINE Emit-Primitiv (EM-1). Deterministische CommandId (W1-Dedup am Empfänger)
        //   + bounded Token (W2) stecken jetzt im Primitiv — kein eigener Retry/Envelope/RequestAsync mehr.
        //   Die Auslöse-Position (Stream + Version) reist in die Kausalität → stabile Id über Re-Wakes;
        //   die Wirksamkeit sichert der Noop-Decider/Inbox des Empfängers (Spec 9.3).
        var triggerVersion = (trigger as IEventEnvelope)?.AggregateVersion ?? 0;
        var korrelation = Guid.TryParse(trigger.CorrelationId, out var kr) ? kr : Guid.Empty;
        var k = new EmitKausalität(korrelation, trigger.AggregateId, $"{triggerVersion}:{command.GetType().Name}");
        return _emitter.EmitAsync(command, k, ct);
    }
}
