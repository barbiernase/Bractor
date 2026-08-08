namespace Abstractions;

/// <summary>
/// Durables Checkpoint-Dokument pro (Projektion, Stream) — die Ablageform der
/// Fortschrittsmarke. Id-Form: "{projectionId}:{streamId}" → ein O(1)-Load je Stream.
///
/// Ein reines POCO (keine Persistenz-Abhängigkeit), damit es sowohl der generische
/// Framework-Tracker (Infrastructure) als auch domänen-eigene Co-Commit-Stores
/// (Domain.Infrastructure) teilen können. Die Marten-Schema-Registrierung
/// (<c>opts.Schema.For&lt;ProjectionCheckpoint&gt;().Identity(x =&gt; x.Id)</c>) erfolgt
/// in der Host-/Store-Konfiguration.
/// </summary>
public class ProjectionCheckpoint
{
    public string Id { get; set; } = default!;
    public string ProjectionId { get; set; } = default!;
    public Guid StreamId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static string MakeId(string projectionId, Guid streamId)
        => $"{projectionId}:{streamId}";
}
