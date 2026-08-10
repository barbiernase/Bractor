using Abstractions;
using Core;

namespace Infrastructure.Projections;

/// <summary>
/// Der Projektions-Rebuild-Runner (Feature-Strom): „Replay IST der Adapter-Pfad mit Fortschritt = -1"
/// (Spec 7.7 / IProjectionRebuilder). EINE Schleife, die den bestehenden <see cref="ProjectionAdapter"/>
/// wiederverwendet — kein zweiter Codepfad:
///   1. Ziel leeren (<see cref="IRebuildableProjection.ClearAsync(System.Threading.CancellationToken)"/>),
///   2. Marken auf -1 (<see cref="IProjectionTracker.ResetAllAsync"/>),
///   3. je Stream ab 0 lesen + über den generierten Dispatch anwenden (der Adapter liest ab Marke+1 = 0,
///      re-dispatcht und schreibt die Marke neu fort) — mit garantiert leerem Ausgangszustand.
///
/// Replay-Grenze (Phase 3, festgeklopft): Replay ist PROJEKTIONS-lokal. Nur REPLAYBARE Konsumenten
/// (Projektionen, Achse B) haben einen <see cref="IProjectionTracker"/> und damit Reset — der P4.2-
/// Compile-Zeit-Schnitt macht Emittenten (Reaktion/Pipeline-Event, <see cref="IEmittentenCursor"/> ohne
/// Reset) strukturell un-rebuildbar. Deshalb re-dispatcht der Rebuilder ausschließlich Repo-Write-Effekte;
/// geld-bewegende reaktive Ausgänge werden NIE blind replayt (sie liegen gar nicht auf diesem Pfad).
/// </summary>
public sealed class ProjectionRebuilder
{
    private readonly IEventStoreRepository _eventStore;

    public ProjectionRebuilder(IEventStoreRepository eventStore)
        => _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));

    /// <summary>Vollständiger Neuaufbau: Ziel leeren, alle Marken auf -1, jeden geänderten Stream ab 0 re-dispatchen.</summary>
    public async Task RebuildAsync(
        string projectionId,
        IProjectionTracker tracker,
        IRebuildableProjection clearable,
        Func<EventEnvelope, ProjectionWriter, Task> dispatch,
        CancellationToken ct = default)
    {
        await clearable.ClearAsync(ct);                 // 1. Ziel leeren (nie in befüllten Zustand replayen)
        await tracker.ResetAllAsync(projectionId, ct);  // 2. Marken -1

        // 3. Alle Streams (globaler Scan ab Sequenz 0) über den normalen Adapter-Pfad neu anwenden.
        //    Irrelevante Events überspringt der generierte Dispatch (Switch ohne Case = no-op) —
        //    Waken jedes Streams ist also korrekt, nicht nur der „eigenen".
        var changes = await _eventStore.ReadChangedStreamsAsync(0, ct);
        var adapter = new ProjectionAdapter(_eventStore, tracker, emittentenCursor: null, projectionId, dispatch);
        foreach (var streamId in changes.StreamIds)
            await adapter.WakeAsync(streamId, ct);
    }

    /// <summary>Neuaufbau EINES Streams: nur dessen Ziel leeren, dessen Marke auf -1, ab 0 re-dispatchen.</summary>
    public async Task RebuildStreamAsync(
        string projectionId,
        Guid streamId,
        IProjectionTracker tracker,
        IRebuildableProjection clearable,
        Func<EventEnvelope, ProjectionWriter, Task> dispatch,
        CancellationToken ct = default)
    {
        await clearable.ClearAsync(streamId, ct);
        await tracker.ResetAsync(projectionId, streamId, ct);
        var adapter = new ProjectionAdapter(_eventStore, tracker, emittentenCursor: null, projectionId, dispatch);
        await adapter.WakeAsync(streamId, ct);
    }
}
