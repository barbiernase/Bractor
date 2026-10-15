using Abstractions;
using Core;

namespace Infrastructure.Projections;

/// <summary>
/// Der Neuaufbau einer Projektion (docs/04 §4.3): „Neuaufbau IST der Batch-Konsum ab Position 0“ — kein zweiter Codepfad:
///   1. Ziel leeren (<see cref="IRebuildableProjection.ClearAsync"/>),
///   2. Partitionen und Guards zurücksetzen (<see cref="IProjectionTracker.ResetAllAsync"/>; die Partitionen bekommen eine neue Version — ein
///      noch laufender Leser verliert seinen nächsten Commit),
///   3. je Partition einen <see cref="BatchLeser"/> ab 0 laufen lassen — seitenweise, als Strom (nie der ganze Log als Liste).
///
/// Replay-Grenze: nur REPLAYBARE Konsumenten (Projektionen) haben einen <see cref="IProjectionTracker"/> und damit Reset. Emittenten
/// (<see cref="IEmittentenCursor"/> ohne Reset) liegen nie auf diesem Pfad — geld-bewegende Ausgänge werden nie blind replayt.
/// </summary>
public sealed class ProjectionRebuilder
{
    private readonly IEventStoreRepository _eventStore;
    private readonly Konsum _konsum;

    public ProjectionRebuilder(IEventStoreRepository eventStore, Konsum? konsum = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _konsum = konsum ?? Konsum.Standard;
    }

    /// <summary>Vollständiger Neuaufbau: Ziel leeren, Partitionen und Guards auf Anfang, jede Partition ab 0 seitenweise anwenden.</summary>
    /// <param name="typen">Typfilter der Seiten (<see cref="BatchLeser.TypenAus(IEnumerable{Type})"/>); null = ohne.</param>
    public async Task RebuildAsync(
        string projektionsId,
        IProjectionTracker tracker,
        IRebuildableProjection ziel,
        IReadOnlyCollection<string>? typen,
        Func<EventEnvelope, ProjectionWriter, Task> dispatch,
        CancellationToken ct = default)
    {
        await ziel.ClearAsync(ct);                       // 1. Ziel leeren (nie in befüllten Zustand replayen)
        await tracker.ResetAllAsync(projektionsId, ct);  // 2. Partitionen und Guards auf Anfang
        for (var p = 0; p < _konsum.Partitionen; p++)    // 3. ab 0, seitenweise
            await new BatchLeser(_eventStore, tracker, projektionsId, p, _konsum, typen, dispatch).LaufAsync(ct);
    }
}
