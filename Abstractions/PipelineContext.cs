namespace Abstractions;

/// <summary>
/// Kontext für einen Pipeline-Handler-Aufruf.
///
/// Bei Trigger-Input: Nur CorrelationId gesetzt, kein Aggregate-Kontext.
/// Bei Event-Input: Voller Aggregate-Kontext aus dem IAggregateEnvelope.
/// Bei Self-Message: frische CorrelationId.
///
/// Analog zu MessageContext (Client) und WriteContext (Subscriber). Reine Daten — Planen (Selbst-Nachricht,
/// Frist) ist ein typisierter AUSGANG der Signatur (<see cref="Selbst{T}"/>, <see cref="Frist{TCmd}"/>), kein Aufruf hier.
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Korrelations-ID für Tracing. Wird an alle erzeugten Commands propagiert.
    /// Bei Trigger-Input: neue GUID. Bei Event-Input: aus Envelope übernommen.
    /// </summary>
    public string CorrelationId { get; init; } = "";

    /// <summary>Nur gesetzt bei Event-Input (PubSub).</summary>
    public Guid? SourceAggregateId { get; init; }

    /// <summary>Nur gesetzt bei Event-Input (PubSub).</summary>
    public string? SourceAggregateType { get; init; }

    /// <summary>Nur gesetzt bei Event-Input (PubSub).</summary>
    public int? SourceAggregateVersion { get; init; }
}
