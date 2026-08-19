using Abstractions;
using Domain.Modell;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Modell-Projektion (Naht-Muster wie <see cref="DatensatzStore"/> /
/// <see cref="ImagePairStore"/>). Write-Operationen PUFFERN nur; <see cref="MarkProcessedAsync"/>
/// spielt sie in EINER Marten-Session ab und staged den <see cref="ProjectionCheckpoint"/> →
/// EIN <c>SaveChanges</c> (Effekt + Marke gemeinsam, exactly-once). TRANSIENT registriert.
/// </summary>
public sealed class ModellStore : ICoCommitTracker, IModellWriteStore
{
    private readonly IDocumentStore _store;
    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    public ModellStore(IDocumentStore store) => _store = store;

    // ── Write-Seite: nur puffern ──

    public Task UpsertAsync(ModellReadModel model)
    {
        _pending.Add(s => { s.Store(model); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    public Task SetzeAktivAsync(Guid modellId, string? name, string? pfad, DateTimeOffset aktiviertAm)
    {
        _pending.Add(s =>
        {
            s.Store(new AktivesModellReadModel
            {
                Id = AktivesModellReadModel.Singleton,
                ModellId = modellId,
                Name = name,
                Pfad = pfad,
                AktiviertAm = aktiviertAm
            });
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }

    public Task ArchiviereAsync(Guid modellId, DateTimeOffset aktualisierung)
    {
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<ModellReadModel>(modellId);
            if (existing is null) return;
            s.Store(existing with { Status = ModellStatus.Archiviert });
        });
        return Task.CompletedTask;
    }

    // ── Marke lesen: reiner Read, klammert den Batch-Beginn ──

    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    // ── Commit-Punkt: Effekt(e) + Marke in EINER Session, ein SaveChanges ──

    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = _store.IdentitySession();
        foreach (var op in _pending)
            await op(s);

        s.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId,
            StreamId = streamId,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await s.SaveChangesAsync(ct);
        _pending.Clear();
    }

    // ── Replay / Rebuild ──

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
}
