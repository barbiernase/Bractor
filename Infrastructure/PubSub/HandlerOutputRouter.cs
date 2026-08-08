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
    private readonly Cluster _cluster;
    private readonly BrokerPublisher? _publisher;
    private readonly string _subscriberId;
    private readonly ILogger _logger;

    public HandlerOutputRouter(Cluster cluster, BrokerPublisher? publisher, string subscriberId, ILogger? logger = null)
    {
        _cluster = cluster;
        _publisher = publisher;
        _subscriberId = subscriberId;
        _logger = logger ?? NullLogger.Instance;
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

    private async Task SendReaktionAsync(ICommand command, IAggregateEnvelope trigger, CancellationToken ct)
    {
        if (!GeneratedPipelines.CommandAggregateTypes.TryGetValue(command.GetType(), out var aggregateType))
        {
            _logger.LogWarning("[{Subscriber}] keine AggregateType-Zuordnung für Reaktions-Command {Command}", _subscriberId, command.GetType().Name);
            return;
        }

        var triggerVersion = (trigger as IEventEnvelope)?.AggregateVersion ?? 0;
        var commandId = ReaktionsId.For(trigger.AggregateId, triggerVersion, command.GetType().Name);

        // Reaktion behauptet KEINE Empfänger-Version (Spec 9.3): der Sender kennt sie nicht und
        //   soll sie nicht kennen. AnyVersion → der Empfänger-Actor konfligiert nie über OCC, sondern
        //   dedupliziert am (Noop-)Decider über die deterministische CommandId. Damit entfällt der
        //   frühere garantierte „expected 0 vs. actual N"-Konflikt (+ das nicht-publizierbare CommandFailed).
        var envelope = new CommandEnvelope
        {
            CommandId = commandId,               // deterministisch (Spec 9.3)
            AggregateId = command.AggregateId,
            AggregateType = aggregateType,
            ExpectedVersion = CommandEnvelope.AnyVersion,
            CorrelationId = trigger.CorrelationId,
            Payload = command
        };
        var identity = ClusterIdentity.Create(command.AggregateId.ToString(), aggregateType);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            CommandResult? result;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(3));   // begrenzt — kein Infinit-Retry (Spec: at-least-once, Re-Wake heilt)
                try { result = await _cluster.RequestAsync<CommandResult>(identity, envelope, cts.Token); }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("[{Subscriber}] Reaktion-Send Timeout (Versuch {Attempt}) an {AggregateType}/{AggregateId}", _subscriberId, attempt, aggregateType, command.AggregateId);
                    result = null;
                }
            }

            if (result == null) continue;                         // keine Antwort → erneut (Re-Wake heilt ebenfalls)

            if (result.Success)
            {
                _logger.LogDebug("[{Subscriber}] → Reaktion {Command} an {AggregateType}/{AggregateId} (v{Version})", _subscriberId, command.GetType().Name, aggregateType, command.AggregateId, result.NewVersion);
                return;
            }

            return;   // fachliche Ablehnung / technischer Fehler → kein Retry (kein OCC-Konflikt mehr möglich)
        }
    }
}
