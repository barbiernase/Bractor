namespace Abstractions;

/// <summary>
/// Envelope für Commands mit Metadaten.
/// Implementiert IAggregateEnvelope — Commands haben immer Aggregate-Kontext.
/// </summary>
public record CommandEnvelope : IAggregateEnvelope, IWireMessage
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }

    /// <summary>
    /// Die explizite Auslieferungs-Art (EM-2): <see cref="CommandModus.Client"/> (OCC gegen eine
    /// behauptete Version, externer Vertrag) oder <see cref="CommandModus.Emittiert"/> (interner
    /// Emitter, KEINE Version — Idempotenz über die deterministische <see cref="CommandId"/> + Inbox).
    /// <c>required</c>: es gibt keinen Default-Pfad mehr (der alte <c>AnyVersion=-1</c>-Sentinel ist weg).
    /// </summary>
    public required CommandModus Modus { get; init; }

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
public record EventEnvelope : IEventEnvelope, IWireMessage
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public int AggregateVersion { get; init; } = 0;

    /// <summary>
    /// Der Stream-Head NACH dem Commit dieses Batches — inklusive der co-committeten
    /// <c>KommandoVerarbeitet</c>-Inbox-Marke. Anders als <see cref="AggregateVersion"/> (die
    /// unveränderliche Position dieses Events, Spec 4.2) sagt dies dem Client, welche
    /// <c>ExpectedVersion</c> sein nächster Command tragen muss — sonst scheitert er am Marker
    /// (Head = Event-Version + 1). 0 = nicht gesetzt → Client fällt auf AggregateVersion zurück.
    /// </summary>
    public int StreamHeadVersion { get; init; } = 0;

    /// <summary>Unter-Position bei Upcasting-Splits; Default 0 (ein Stored-Event → ein materialisiertes Event). Siehe <see cref="IEventEnvelope.SubIndex"/>.</summary>
    public int SubIndex { get; init; } = 0;
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