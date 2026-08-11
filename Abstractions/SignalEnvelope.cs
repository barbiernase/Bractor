namespace Abstractions;

/// <summary>
/// Transport-Hülle für ein Signal (`StateChangeVia{Event}`) auf dem PubSub.
///
/// Ein Signal ist nur ein Weckruf — die Hülle trägt keine fachliche Nutzlast; die
/// einzigen Daten `(StreamId, Version)` stecken im Signal selbst. Bewusst KEIN
/// EventEnvelope: Signale sind keine Events (Payload = IStateChangeSignal, nicht IEvent),
/// und sie werden nie im Log persistiert.
///
/// Der BrokerPublisher shardet über <c>Payload.GetType()</c> — die konkrete
/// Signal-Klasse — sodass nur die Receiver geweckt werden, die diesen Event-Typ
/// behandeln (typbasiertes Routing, Invariante 3).
/// </summary>
public record SignalEnvelope : IMessageEnvelope, IWireMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = "";
    public string UserId { get; init; } = "system";

    /// <summary>Das eigentliche Signal (trägt StreamId + Version).</summary>
    public required IStateChangeSignal Signal { get; init; }

    // Explizite Interface-Implementierung: das Signal IST die Payload.
    IMessagePayload IMessageEnvelope.Payload => Signal;
}
