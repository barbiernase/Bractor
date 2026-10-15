namespace Abstractions;

/// <summary>
/// Das EINE Emit-Primitiv (EM-1): „schicke ein Command an ein Aggregat, exactly-once-wirksam".
/// Aufrufer: <c>HandlerOutputRouter</c> (Reaktion), <c>ProzessManagerActor</c> (Dirigent), <c>FristScheduler</c>,
/// <c>PipelineActorBase</c> (Handle-Welt, W11-1).
///
/// Immer idempotent (deterministische CommandId aus <see cref="EmitKausalität"/>), bounded Token,
/// at-least-once auf dem Draht → exactly-once-wirksam via Empfänger-Inbox. Der Aufrufer bekommt den
/// <see cref="EmitAusgang"/>: nur „zugestellt" oder „abgelehnt" sind entschieden; „unbekannt" heißt, er muss
/// mit derselben CommandId wiederholen (Frist liegen lassen, Cursor nicht vorrücken). KEIN Versions-Argument:
/// interne Emitter behaupten NIE eine Version (Entwurfsaxiom §4.2). OCC ist ausschließlich der
/// externe Client-Vertrag; ein echtes read-modify-write gegen eine Version ist Sache des Deciders,
/// nicht des Emits. Genau EIN Weg → die W1/W2-Invariante (Retry ⟹ deterministische Id + Empfänger-
/// Dedup, jede await-Kante bounded) ist strukturell unausweichlich.
/// </summary>
public interface ICommandEmitter
{
    Task<EmitAusgang> EmitAsync(ICommand cmd, EmitKausalität k, CancellationToken ct);
}

/// <summary>
/// Was ein Emit über die Wirkung weiß (docs/11 §11.3). Der Ausgang ist eine Aussage über den Empfänger, nicht über den Draht:
/// <list type="bullet">
///   <item><see cref="Zugestellt"/> — das Ziel hat den Command verarbeitet (gewirkt oder als Wiederholung erkannt).</item>
///   <item><see cref="Abgelehnt"/> — das Ziel hat fachlich abgelehnt; die Ablehnung ist durabel (Marke <c>KommandoAbgelehnt</c>),
///     eine Wiederholung bringt dieselbe. Auch das ist entschieden.</item>
///   <item><see cref="Unbekannt"/> — keine Antwort in der Frist, ein technischer Fehler oder kein Ziel: vielleicht gewirkt, vielleicht
///     nicht. Nur eine Wiederholung mit derselben CommandId klärt es (die Inbox des Ziels verwirft ein Duplikat).</item>
/// </list>
/// </summary>
public enum EmitAusgang
{
    Zugestellt,
    Abgelehnt,
    Unbekannt,
}

public static class EmitAusgangErweiterung
{
    /// <summary>Zugestellt oder abgelehnt: der Command muss nicht wiederholt werden.</summary>
    public static bool IstEntschieden(this EmitAusgang a) => a != EmitAusgang.Unbekannt;
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
