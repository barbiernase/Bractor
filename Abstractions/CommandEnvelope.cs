namespace Abstractions;

/// <summary>
/// Envelope für Commands mit Metadaten.
/// Implementiert IAggregateEnvelope — Commands haben immer Aggregate-Kontext.
/// </summary>
public record CommandEnvelope : IAggregateEnvelope
{
    /// <summary>
    /// Sentinel für <see cref="ExpectedVersion"/>: „keine OCC-Assertion" — wende den Command an der
    /// aktuellen Aggregat-Version an, ohne eine erwartete Version zu behaupten. Für Reaktionen/
    /// Prozess-Commands (Spec 9.3), deren Sender die Empfänger-Version weder kennt noch kennen will:
    /// Idempotenz sichert die deterministische <c>CommandId</c> + der (Noop-)Decider, nicht die Version.
    /// </summary>
    public const int AnyVersion = -1;

    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public int ExpectedVersion { get; init; } = AnyVersion;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string UserId { get; init; } = "system";
    public ICommand Payload { get; init; }
    public string AggregateType { get; init; }
    
    /// <summary>
    /// Session-ID des Clients der den Command gesendet hat.
    /// Wird für Targeted Delivery von CommandFailed Events verwendet.
    /// </summary>
    public string? OriginSessionId { get; init; }

    // Explizite Interface-Implementierung
    Guid IMessageEnvelope.MessageId => CommandId;
    IMessagePayload IMessageEnvelope.Payload => Payload;
}

/// <summary>
/// Envelope für Events mit Metadaten.
/// Implementiert IEventEnvelope — Events haben immer Aggregate-Kontext UND eine
/// Position im Stream (AggregateVersion). Die Property existiert bereits konkret;
/// die zusätzliche Interface-Deklaration ist rein additiv (kein Verhaltenswechsel).
/// </summary>
public record EventEnvelope : IEventEnvelope
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public int AggregateVersion { get; init; } = 0;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string CausationId { get; init; } = Guid.NewGuid().ToString();
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string UserId { get; init; } = "system";
    public IEvent Payload { get; init; }
    public string AggregateType { get; init; }
    
    /// <summary>
    /// Wenn gesetzt: Event wird nur an diesen Subscriber gesendet (Targeted Delivery).
    /// Wenn null: Event wird an alle Subscriber gesendet (Broadcast).
    /// </summary>
    public string? TargetSubscriberId { get; init; }

    // Explizite Interface-Implementierung
    Guid IMessageEnvelope.MessageId => EventId;
    IMessagePayload IMessageEnvelope.Payload => Payload;
}

/// <summary>
/// Envelope für Queries — Transport-Metadaten ohne Aggregate-Kontext.
/// Queries haben kein Aggregat, daher nur IMessageEnvelope (nicht IAggregateEnvelope).
///
/// Wird vom ProjectionQueryService beim Query-Handling erstellt.
/// Ermöglicht CorrelationId/UserId-Zugriff in Read-Handlern.
/// </summary>
public record QueryEnvelope : IMessageEnvelope
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = "";
    public string UserId { get; init; } = "anonymous";
    public IMessagePayload Payload { get; init; }
}

public record MessageEnvelope(string PayloadType, string Payload);