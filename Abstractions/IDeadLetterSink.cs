namespace Abstractions;

/// <summary>
/// Senke für „tote" ausgehende Commands (§5): ein Command, den eine Pipeline nach Erschöpfung der Retries
/// oder wegen eines technischen/Routing-Fehlers NICHT zustellen konnte. Bisher verschwand er still (nur ein
/// <c>LogError</c>). Jetzt wird er ein durabler, beobachtbarer und replay-barer Eintrag — bewusst KEIN
/// Retry-Outbox: der OCC-Pipeline-Pfad hat keine Idempotenz-Dedup, ein automatischer Retry könnte bei
/// verlorener Quittung doppelt wirken. Der DLQ-Eintrag macht den Verlust sichtbar, ohne eine Garantie
/// vorzutäuschen, die der Pfad nicht hergibt. Reines Vertragsinterface; die Ablage ist Store-Sache.
/// </summary>
public interface IDeadLetterSink
{
    /// <summary>Schreibt einen Dead-Letter-Eintrag. Best-effort/beobachtend — wirft NIE in den Aufrufer zurück.</summary>
    Task WriteAsync(DeadLetter eintrag, CancellationToken ct = default);
}

/// <summary>
/// Ein nicht zustellbarer ausgehender Command. Reines POCO ohne Persistenz-Abhängigkeit (analog
/// <see cref="ProjectionCheckpoint"/>); die Marten-Schema-Registrierung erfolgt in der Host-/Store-Konfiguration.
/// </summary>
public class DeadLetter
{
    public Guid Id { get; set; }
    public string Quelle { get; set; } = default!;      // z.B. die PipelineId
    public string CommandType { get; set; } = default!;
    public Guid AggregateId { get; set; }
    public string? AggregateType { get; set; }
    public string? CorrelationId { get; set; }
    public string Grund { get; set; } = default!;       // warum der Command tot ist
    public int Versuche { get; set; }
    public DateTimeOffset ErfasstUtc { get; set; }
}
