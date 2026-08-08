namespace Abstractions;

/// <summary>
/// Das EINE Emit-Primitiv (EM-1): „schicke ein Command an ein Aggregat, exactly-once-wirksam".
/// Ersetzt in P3 die vier heutigen Emit-Pfade (Dispatcher-intern, <c>HandlerOutputRouter</c>,
/// <c>ProzessManager</c>/<c>DetachedProzessSend</c>, <c>PipelineActorBase</c>).
///
/// Immer idempotent (deterministische CommandId aus <see cref="EmitKausalität"/>), bounded Token,
/// at-least-once auf dem Draht → exactly-once-wirksam via Empfänger-Inbox. KEIN Versions-Argument:
/// interne Emitter behaupten NIE eine Version (Entwurfsaxiom §4.2). OCC ist ausschließlich der
/// externe Client-Vertrag; ein echtes read-modify-write gegen eine Version ist Sache des Deciders,
/// nicht des Emits. Genau EIN Weg → die W1/W2-Invariante (Retry ⟹ deterministische Id + Empfänger-
/// Dedup, jede await-Kante bounded) ist strukturell unausweichlich.
/// </summary>
public interface ICommandEmitter
{
    Task EmitAsync(ICommand cmd, EmitKausalität k, CancellationToken ct);
}

/// <summary>
/// Die Kausalität eines Emits — aus ihr wird die deterministische CommandId abgeleitet:
/// zwei Weckungen derselben Ursache → gleiche Id → Noop am Empfänger (Framework-Inbox).
/// </summary>
/// <param name="Korrelation">
/// Fachliche Korrelation (z. B. der Prozess). Reist als CorrelationId ins Ziel-Event-Metadatum und
/// ist die Basis des P5-Terminal-Fix (Poll-Routing per CorrelationId).
/// </param>
/// <param name="Ursache">Das auslösende Ereignis (die Ursache dieses Emits).</param>
/// <param name="Diskriminator">
/// Trennt mehrere Emits derselben Ursache (Regel-/Ziel-Unterscheidung) — verhindert Id-Kollision bei
/// Fan-out.
/// </param>
public readonly record struct EmitKausalität(Guid Korrelation, Guid Ursache, string Diskriminator);
