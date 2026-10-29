namespace Abstractions;

/// <summary>
/// Envelope für Commands mit Metadaten.
/// Implementiert IAggregateEnvelope — Commands haben immer Aggregate-Kontext.
/// </summary>
public record CommandEnvelope : IAggregateEnvelope, IWireMessage
{
    public Guid CommandId { get; init; } = Guid.NewGuid();

    /// <summary>Das Ziel — aus dem Command abgeleitet (<see cref="ICommand.Ziel"/>), keine zweite Angabe: Routing und Aggregat-Actor sehen
    /// dieselbe Id, die der Command typisiert trägt (docs/10 §10.4).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid AggregateId => Payload.Ziel;

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
    /// Die Sitzung, die den Command gesendet hat (Knoten und Nummer) — ihr gehen Ablehnung und <c>CommandFailed</c> gezielt zu.
    /// </summary>
    public string? OriginSessionId { get; init; }

    /// <summary>Spur (<see cref="Spur"/>): der Log-Eintrag, der diesen Command ausgelöst hat (<c>Stream:Version</c>) — das Aggregat
    /// stempelt ihn als Ursache an seine Events. Null: von außen (Akteur), ohne Vorgänger im Log.</summary>
    public string? Ursache { get; init; }

    /// <summary>Spur: der Baustein, der den Command abgeschickt hat (Wirker-Id) — für Hinweise wie Ablehnungen.</summary>
    public string? Wirker { get; init; }

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
    /// Der Kopf des Streams NACH dem Commit dieses Events — dieselbe Version wie <see cref="AggregateVersion"/> (eine Version je Aggregat, Plan
    /// Pub/Sub-Neustrukturierung Phase 5), nur am Ende des Commits: schrieb er nach diesem Event weitere (die der Client vielleicht nicht hört,
    /// etwa eine Frist), muss der nächste Command des Clients den Kopf tragen. 0 = nicht gesetzt → Client fällt auf AggregateVersion zurück.
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
    /// Gesetzt: geht nur an diese Sitzung (eine Antwort auf ihren Command: Ablehnung, <c>CommandFailed</c>) — über den Verteiler des Knotens,
    /// den die Sitzungs-Id nennt, gleich, was sie hört. Null: an jede Sitzung, die den Typ hört.
    /// </summary>
    public string? ZielSitzung { get; init; }

    /// <summary>Spur (<see cref="Spur"/>): der auslösende Log-Eintrag (<c>Stream:Version</c>); null = Wurzel (von außen).</summary>
    public string? Ursache { get; init; }

    /// <summary>Spur: der Baustein, der dieses Event geschrieben hat (Wirker-Id = Karte im Editor).</summary>
    public string? Wirker { get; init; }

    /// <summary>Spur: wer den Command bzw. Auftrag abgeschickt hat, den der Wirker verarbeitete (Fluss-Knoten, Regel, Reaktion, Akteur).</summary>
    public string? Ueber { get; init; }

    /// <summary>Spur: der Build-Stand, unter dem es geschrieben wurde.</summary>
    public string? Stand { get; init; }

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