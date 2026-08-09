using Abstractions;
using Core;

namespace Infrastructure.Pipeline;

/// <summary>
/// P6.2 — die Brücke, die den EVENT-Pfad einer Pipeline (Kanal 2: Event rein → Command raus) an die
/// geordnete Pull-/Signal-Maschine (<see cref="Infrastructure.Projections.ProjectionAdapter"/>) hängt,
/// statt an den verlustbehafteten Push-Broker (<c>BrokerSubscription</c> in <c>PipelineActorBase</c>).
///
/// Der Event-Pfad IST eine Reaktion (Ein-Strom/Emittierend, Achse B): er faltet nicht, sondern
/// emittiert Commands (idempotent am Empfänger). Diese Brücke übersetzt den Pull-Dispatch
/// (<c>Func&lt;EventEnvelope, ProjectionWriter, Task&gt;</c>) auf die generierte
/// <c>DispatchEventAsync</c> der Pipeline — der Entwickler-Code (<c>Handle(evt, ctx)</c>) bleibt
/// unverändert, nur der TRANSPORT darunter wechselt (Zielbild §4.5).
///
/// Rein (kein Cluster/Proto): die drei Ausgabe-Nähte werden injiziert →
///   - <paramref name="emitFactory"/> baut je Auslöse-Event die Command-/Event-Route
///     (live: <c>DetachedEmit.Wrap(HandlerOutputRouter.EmitFor(e))</c> — bounded, deterministische Id),
///   - <c>sendTrigger</c> die pipeline→pipeline-Route.
/// So ist der Fold in-memory mit Fakes prüfbar (ohne echten Cluster/OpenCV).
/// </summary>
public static class PipelineEventPullBridge
{
    /// <summary>Signatur der generierten <c>DispatchEventAsync</c> einer Pipeline.</summary>
    public delegate Task EventDispatch(
        IAggregateEnvelope envelope,
        PipelineContext ctx,
        Func<ICommand, Task> sendCommand,
        Func<IPipelineTrigger, Task> sendTrigger,
        Func<ITransientEvent, Task> broadcastTransient);

    /// <summary>
    /// Adaptiert <paramref name="dispatchEvent"/> auf den Pull-Maschinen-Dispatch. Baut je Event den
    /// <see cref="PipelineContext"/> aus dem Envelope (Quell-Aggregat/Version/Korrelation — sonst bräche
    /// die deterministische Emit-Id) und verdrahtet die drei Ausgabe-Nähte. Der <c>ProjectionWriter</c>
    /// wird nicht genutzt: ein Emittent hat keinen co-committeten Read-Model-Effekt (Achse B).
    /// </summary>
    public static Func<EventEnvelope, ProjectionWriter, Task> Wrap(
        EventDispatch dispatchEvent,
        Func<EventEnvelope, Func<IPipelineOutput, Task>> emitFactory,
        Func<IPipelineTrigger, string, Task> sendTrigger)
    {
        if (dispatchEvent is null) throw new ArgumentNullException(nameof(dispatchEvent));
        if (emitFactory is null) throw new ArgumentNullException(nameof(emitFactory));
        if (sendTrigger is null) throw new ArgumentNullException(nameof(sendTrigger));

        return (e, _writer) =>
        {
            var ctx = new PipelineContext
            {
                CorrelationId = e.CorrelationId ?? "",
                SourceAggregateId = e.AggregateId,
                SourceAggregateType = e.AggregateType,
                SourceAggregateVersion = e.AggregateVersion,
            };

            var emit = emitFactory(e);   // Func<IPipelineOutput,Task> — ICommand→Emit, IEvent→Publish (Router)

            return dispatchEvent(
                e, ctx,
                cmd => emit(cmd),                              // ICommand : IPipelineOutput
                trig => sendTrigger(trig, ctx.CorrelationId),  // pipeline→pipeline
                te => emit(te));                               // ITransientEvent : IEvent : IPipelineOutput
        };
    }
}
