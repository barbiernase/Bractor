using Abstractions;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der ImagePairHistorie (Domänen-Infra, nicht im Framework).
///
/// Naht-Muster (Spec 7.4, Fall 1): <see cref="AppendEintragAsync"/> PUFFERT nur.
/// <see cref="MarkProcessedAsync"/> ist der Commit-Punkt — EINE Marten-Session staged alle
/// gepufferten Effekte UND den <see cref="ProjectionCheckpoint"/> und committet mit EINEM
/// SaveChanges → Effekt und Marke gemeinsam gültig (exactly-once). Ein Absturz davor hat den
/// DB nie berührt → nichts durabel → nächster Read heilt.
///
/// Wird pro Stream frisch instanziiert (Transient; der Single-Writer-Actor sorgt für Isolation
/// des Puffers). Der Puffer wird zu Batch-Beginn in <see cref="LastProcessedVersionAsync"/>
/// verworfen, damit ein abgebrochener Vorlauf nicht in den nächsten Batch leckt.
/// </summary>
public sealed class ImagePairHistorieStore
    : IProjectionTracker, IImagePairHistorieWriteStore, IImagePairHistorieReadStore
{
    private readonly IDocumentStore _store;
    private readonly List<(Guid PairId, HistorieEintrag Eintrag)> _pending = new();

    public ImagePairHistorieStore(IDocumentStore store) => _store = store;

    // ─── Effekt: nur puffern, KEIN Commit ───

    public Task AppendEintragAsync(Guid pairId, HistorieEintrag eintrag)
    {
        _pending.Add((pairId, eintrag));
        return Task.CompletedTask;
    }

    // ─── Marke lesen: reiner Read, klammert den Batch-Beginn ───

    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();   // neuer Batch → alten Vorlauf verwerfen
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    // ─── Commit-Punkt: Effekt(e) + Marke in EINER Session, ein SaveChanges ───

    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = _store.IdentitySession();   // read-your-writes im Batch
        foreach (var (pairId, eintrag) in _pending)
        {
            var existing = await s.LoadAsync<ImagePairHistorieReadModel>(pairId, ct);
            s.Store(existing is null
                ? new ImagePairHistorieReadModel { Id = pairId, Eintraege = new List<HistorieEintrag> { eintrag } }
                : existing with { Eintraege = new List<HistorieEintrag>(existing.Eintraege) { eintrag } });
        }

        s.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId,
            StreamId = streamId,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await s.SaveChangesAsync(ct);   // EIN Commit → Effekt(e) + Marke gemeinsam gültig
        _pending.Clear();
    }

    // ─── Replay / Rebuild (eigene Session) ───

    public async Task ResetAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var s = _store.LightweightSession();
        s.Delete<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projectionId, streamId));
        await s.SaveChangesAsync(ct);
    }

    public async Task ResetAllAsync(string projectionId, CancellationToken ct)
    {
        await using var s = _store.LightweightSession();
        s.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projectionId);
        await s.SaveChangesAsync(ct);
    }

    // ─── Read ───

    public async Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Guid pairId)
    {
        await using var s = _store.QuerySession();
        return await s.LoadAsync<ImagePairHistorieReadModel>(pairId);
    }
}
