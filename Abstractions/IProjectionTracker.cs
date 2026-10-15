namespace Abstractions;

/// <summary>
/// Die Batch-Naht einer replaybaren Projektion (docs/04 §4.3): der Store, den die Schreib-Fähigkeiten der Handles auflösen, trägt den
/// Fortschritt. Der Batch-Leser liest eine Seite einer Partition, wendet sie je Stream an und committet über diese Naht — Effekte vieler Streams,
/// ihre Guards und den Partitions-Checkpoint gemeinsam.
///
/// Das Framework schreibt nicht vor, WIE der Store das ablegt. Ob Effekte und Fortschritt gemeinsam durabel werden, verspricht erst
/// <see cref="ICoCommitTracker"/>; der GA-1-Check verlangt es für append-artige Projektionen.
///
/// Reihenfolge eines Batches: <see cref="BeginneBatch"/> → je Event <see cref="Stream"/> und der Dispatch (der Store puffert) →
/// <see cref="CommitAsync"/>. Ein geworfener Dispatch committet nie; der nächste <see cref="BeginneBatch"/> verwirft den Puffer.
/// </summary>
public interface IProjectionTracker
{
    /// <summary>Der Stand einer Partition; noch nie geschrieben: Position 0, Version 0.</summary>
    Task<PartitionsCheckpoint> LadePartitionAsync(string projektionsId, int partition, CancellationToken ct);

    /// <summary>Die Guards der Streams — höchste angewandte Version je Stream, fehlt einer: -1. Eine Abfrage.</summary>
    Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektionsId, IReadOnlyCollection<Guid> streams, CancellationToken ct);

    /// <summary>Beginnt einen Batch: verwirft, was noch gepuffert ist.</summary>
    void BeginneBatch();

    /// <summary>Der Stream des nächsten Events — die Schreib-Regel je Dokument prüft gegen ihn.</summary>
    void Stream(Guid streamId);

    /// <summary>
    /// Commit des Batches: die gepufferten Effekte, die <paramref name="guards"/> (Stream → angewandte Version) und <paramref name="stand"/>
    /// (Position, Geparkt) mit der erwarteten Version <c>stand.Version</c>. Liefert die neue Version. Wirft
    /// <see cref="PartitionsKonfliktException"/>, wenn die Partition dazwischen geschrieben wurde, bzw. <see cref="SchreibKonfliktException"/>
    /// bei einem geteilten Dokument; dann ist nichts durabel.
    /// </summary>
    Task<int> CommitAsync(PartitionsCheckpoint stand, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct);

    /// <summary>
    /// Neuaufbau: alle Partitionen der Projektion auf Anfang (Position 0, nichts geparkt, neue Version — ein laufender Leser verliert seinen
    /// nächsten Commit) und alle Guards weg. Zusammen mit dem Leeren des Ziels (<see cref="IRebuildableProjection"/>).
    /// </summary>
    Task ResetAllAsync(string projektionsId, CancellationToken ct);
}
