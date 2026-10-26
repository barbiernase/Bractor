using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;

namespace Infrastructure.PubSub;

/// <summary>
/// Das Ausgabe-Routing eines Handlers (docs/04 §4.4) — herausgezogen aus dem Push-Actor, sodass
/// BEIDE Transporte (Push-Subscriber und Pull-Adapter) dieselbe Logik nutzen:
///   <c>IEvent</c>   → reaktives Event re-publishen (Broker),
///   <c>ICommand</c> → Reaktion an ein Fremd-Aggregat über das eine Emit-Primitiv (deterministische CommandId,
///                     Ausgang in <see cref="EmitAusgaenge"/>; die Wirksamkeit sichert die Inbox des Empfängers, docs/04 §4.4).
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

    // Spur: die Wirker-Id dieses Konsumenten (reaktion:&lt;Typ&gt;) — vom generierten Pull-Pfad gesetzt.
    private readonly string? _wirker;

    public HandlerOutputRouter(Cluster cluster, BrokerPublisher? publisher, string subscriberId, ILogger? logger = null, string? wirker = null)
        : this(new CommandEmitter(cluster, logger), publisher, subscriberId, logger, wirker) { }   // ★ P3: das EINE Emit-Primitiv (EM-1)

    /// <summary>Naht/Test: das Emit-Primitiv mit injiziertem Send (Prüfstand, ohne Cluster).</summary>
    public HandlerOutputRouter(ICommandEmitter emitter, BrokerPublisher? publisher, string subscriberId, ILogger? logger = null, string? wirker = null)
    {
        _wirker = wirker;
        _logger = logger ?? NullLogger.Instance;
        _emitter = emitter;
        _publisher = publisher;
        _subscriberId = subscriberId;
    }

    /// <summary>
    /// Das <c>emit</c> für EIN auslösendes Event — Ausgaben erben dessen Kontext. Ein Command wird sofort gesendet und sein Ausgang in
    /// <paramref name="ausgaenge"/> eingetragen (nicht abgewartet: die Commands eines Tails laufen parallel, der Adapter wartet sie danach
    /// gemeinsam ab). Ein reaktives Event geht verlierbar über den Broker (Inv. 6); sein Fehlschlag hält niemanden auf.
    /// </summary>
    public Func<IPipelineOutput, Task> EmitFor(IAggregateEnvelope trigger, EmitAusgaenge ausgaenge)
        => payload =>
        {
            switch (payload)
            {
                case ICommand command:
                    ausgaenge.Erfasse(SendReaktionAsync(command, trigger, ausgaenge.Token));
                    return Task.CompletedTask;
                case IEvent evt:
                    return PublishReactiveAsync(evt, trigger, ausgaenge.Token);
                default:
                    // Prozess-Start läuft NICHT über eine Plan-Yield-Route: der KorrelationsRouter startet
                    // den Prozess direkt aus dem Auslöse-Event (dessen Signal abonniert er). Kein Handler-Glue nötig.
                    return Task.CompletedTask;
            }
        };

    private Task PublishReactiveAsync(IEvent evt, IAggregateEnvelope trigger, CancellationToken ct)
    {
        // Verlierbar (Inv. 6): ein reaktives Event ist kein durabler Ausgang, der Konsument rückt trotzdem vor.
        Melde(evt, trigger);
        return Task.CompletedTask;
    }

    private void Melde(IEvent evt, IAggregateEnvelope trigger)
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

        _publisher.Melde(newEnvelope);
        _logger.LogDebug("[{Subscriber}] → reaktives Event: {Event}", _subscriberId, evt.GetType().Name);
        // Spur: ein reaktives Event geht nur über den Broker (verlierbar) — als Hinweis an die Werkbank.
        Infrastructure.Einblick.SpurHinweise.Melde(Infrastructure.Einblick.SpurHinweise.Transient, _wirker,
            Spur.Ursache(trigger.AggregateId, (trigger as IEventEnvelope)?.AggregateVersion ?? 0), trigger.CorrelationId, evt.GetType().Name);
    }

    private Task<EmitAusgang> SendReaktionAsync(ICommand command, IAggregateEnvelope trigger, CancellationToken ct)
    {
        // ★ P3: über das EINE Emit-Primitiv (EM-1). Deterministische CommandId (W1-Dedup am Empfänger)
        //   + bounded Token (W2) stecken jetzt im Primitiv — kein eigener Retry/Envelope/RequestAsync mehr.
        //   Die Auslöse-Position (Stream + Version) reist in die Kausalität → stabile Id über Re-Wakes;
        //   die Wirksamkeit sichert der Noop-Decider/Inbox des Empfängers (docs/04 §4.4).
        var triggerVersion = (trigger as IEventEnvelope)?.AggregateVersion ?? 0;
        // ★ Upcasting-Split: bei einem Split teilen sich Geschwister dieselbe AggregateVersion; der SubIndex
        //   disambiguiert die Auslöse-Position, damit ihre deterministischen Emit-Ids nicht kollidieren.
        //   BACKWARD-KOMPATIBEL: SubIndex 0 (jedes heutige, nicht-gesplittete Event) lässt den Positions-String
        //   unverändert → bytegleiche Ids wie bisher; nur >0 hängt ".{sub}" an.
        var triggerSub = (trigger as IEventEnvelope)?.SubIndex ?? 0;
        var pos = triggerSub == 0 ? $"{triggerVersion}" : $"{triggerVersion}.{triggerSub}";
        var korrelation = Guid.TryParse(trigger.CorrelationId, out var kr) ? kr : Guid.Empty;
        var k = new EmitKausalität(korrelation, trigger.AggregateId, $"{pos}:{command.GetType().Name}");
        // Kausalkette: die Reaktion handelt im Auftrag des Akteurs, von dem das auslösende Event stammt.
        // Spur: das auslösende Event ist die Ursache, die Reaktion der Wirker des Commands.
        using (Spur.Von(Spur.Ursache(trigger.AggregateId, triggerVersion), _wirker))
        using (Infrastructure.Akteure.AkteurHerkunft.Aus(trigger.UserId))
            return _emitter.EmitAsync(command, k, ct);
    }
}
