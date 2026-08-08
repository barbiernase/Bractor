using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierte Fortschrittsmarke.
///
/// ⚠ PHASE-0-STAND — EIGENE SESSION (at-least-once):
/// Dieser Tracker öffnet für Mark/Reset seine EIGENE Session. Fortschritt und
/// fachlicher Effekt landen damit in GETRENNTEN Transaktionen (Dual-Write) →
/// die Garantie ist at-least-once, die Handler müssen idempotent sein.
///
/// Das ist bewusst so: Der Nahtpunkt (IProjectionTracker) bleibt identisch,
/// egal ob at-least-once oder exactly-once. Der CO-COMMIT-Umbau (Fortschritt +
/// Effekt über DIESELBE Session, ein SaveChangesAsync) ist die Phase-2-Aufgabe
/// und berührt den Session-Zuschnitt der Effekt-Stores — nicht dieses Interface.
/// Bis dahin sichert der Dedup-Schlüssel (AggregateId, AggregateVersion)
/// append-artige Projektionen ab.
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;ProjectionCheckpoint&gt;().Identity(x =&gt; x.Id);
/// </summary>
public class MartenProjectionTracker : IProjectionTracker, IReplaybarerTracker
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenProjectionTracker> _logger;

    public MartenProjectionTracker(
        IDocumentStore store,
        ILogger<MartenProjectionTracker> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> LastProcessedVersionAsync(
        string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var id = ProjectionCheckpoint.MakeId(projectionId, streamId);
        var doc = await session.LoadAsync<ProjectionCheckpoint>(id, ct);
        return doc?.Version ?? -1;
    }

    public async Task MarkProcessedAsync(
        string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId,
            StreamId = streamId,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
    }

    // ─── Replay / Rebuild ───

    public async Task ResetAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Delete<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId));
        await session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Checkpoint zurückgesetzt: {Projection} / Stream {Stream}",
            projectionId, streamId);
    }

    public async Task ResetAllAsync(string projectionId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projectionId);
        await session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Alle Checkpoints zurückgesetzt: {Projection}", projectionId);
    }
}
