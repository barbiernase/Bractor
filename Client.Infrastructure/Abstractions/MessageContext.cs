namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Transport-Metadaten die jede Nachricht auf dem Bus begleiten.
/// 
/// Bei Server-Events: wird vom ConnectionModule aus dem EventEnvelope befüllt.
/// Bei lokalen Events: wird via Local() mit Minimalwerten erzeugt.
/// 
/// Der MessageContext ist immutable (record) und wird durch die gesamte
/// sync-Chain (Store → Handler → Store) durchgereicht.
/// </summary>
public record MessageContext
{
    /// <summary>Aggregate-ID aus dem EventEnvelope. Leer bei lokalen Events.</summary>
    public Guid AggregateId { get; init; }

    /// <summary>Aggregate-Typ (z.B. "Lagerartikel", "Todo"). Leer bei lokalen Events.</summary>
    public string AggregateType { get; init; } = "";

    /// <summary>
    /// Version des Aggregats NACH diesem Event.
    /// Wird vom VersioningModule gecached für ExpectedVersion bei Commands.
    /// 0 bei lokalen Events.
    /// </summary>
    public int AggregateVersion { get; init; }

    /// <summary>
    /// Der Stream-Head nach dem Commit dieses Events — inklusive der co-committeten
    /// <c>KommandoVerarbeitet</c>-Inbox-Marke. Das VersioningModule zieht diese (wenn gesetzt)
    /// der <see cref="AggregateVersion"/> vor, damit die nächste <c>ExpectedVersion</c> den Marker
    /// mitzählt und nicht am OCC scheitert. 0 = ungesetzt → Fallback auf AggregateVersion.
    /// </summary>
    public int StreamHeadVersion { get; init; }

    /// <summary>Korrelations-ID für Request-Tracking über die gesamte Kette.</summary>
    public string CorrelationId { get; init; } = "";

    /// <summary>Zeitstempel der Nachricht.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Factory für lokal publizierte Nachrichten (Commands, Queries, ClientEvents).
    /// Infrastruktur-Module befüllen die anderen Felder bei Server-Events.
    /// </summary>
    public static MessageContext Local()
        => new() { CreatedAtUtc = DateTimeOffset.UtcNow };
}