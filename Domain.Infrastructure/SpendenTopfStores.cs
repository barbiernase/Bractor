using Abstractions;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

// ══════════════════════════════════════════════════════════════════════════════
// Zwei Tracker-Stores derselben Projektion — der EINE Unterschied zwischen exactly-once
// und at-least-once liegt AUSSCHLIESSLICH hier, in der Atomarität von Effekt + Marke.
// Der Fachcode (SpendenTopfProjection) ist bei beiden byte-identisch (Invariante 5).
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// EXACTLY-ONCE (Co-Commit): die Write-Op PUFFERT nur; <see cref="MarkProcessedAsync"/> spielt Effekt UND
/// <see cref="ProjectionCheckpoint"/> in EINE Marten-<c>IdentitySession</c> und committet sie mit EINEM
/// <c>SaveChanges</c>. Ein Absturz davor hat den DB nie berührt → nichts durabel → der nächste Read heilt →
/// die (nicht-idempotente) Summe wird genau einmal wirksam. Muster: <see cref="ImagePairStore"/>.
/// </summary>
public sealed class SpendenTopfCoCommitStore : IProjectionTracker, ISpendenTopfWriteStore
{
    private readonly IDocumentStore _store;
    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    public SpendenTopfCoCommitStore(IDocumentStore store) => _store = store;

    public Task AddiereAsync(Guid kampagne, decimal betrag, DateTimeOffset wann)
    {
        // NUR puffern (aufgeschoben) — kein Commit, bis Effekt + Marke gemeinsam gehen.
        _pending.Add(async s =>
        {
            var topf = await s.LoadAsync<SpendenTopfReadModel>(kampagne)
                       ?? new SpendenTopfReadModel { Id = kampagne };
            topf.Summe += betrag;
            topf.Anzahl += 1;
            topf.Aktualisiert = wann;
            s.Store(topf);
        });
        return Task.CompletedTask;
    }

    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();   // neuer Batch → alten Vorlauf verwerfen
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = _store.IdentitySession();          // read-your-writes im Batch
        foreach (var op in _pending) await op(s);              // Effekt(e)
        s.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId, StreamId = streamId, Version = version,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await s.SaveChangesAsync(ct);                          // EIN Commit → Effekt + Marke gemeinsam gültig
        _pending.Clear();
    }

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

/// <summary>
/// AT-LEAST-ONCE (getrennt): <see cref="AddiereAsync"/> committet den Effekt SOFORT in einer eigenen
/// Transaktion; <see cref="MarkProcessedAsync"/> committet die Marke DANACH, SEPARAT. Abgestürzt zwischen
/// beiden → Effekt durabel, Marke fehlt → die Re-Delivery verarbeitet dasselbe Event NOCHMAL → die
/// (nicht-idempotente) Summe wird zu hoch. Genau das Fenster, das der Co-Commit schließt. Ein Handler mit
/// idempotenter Wirkung (Upsert eines absoluten Werts) bliebe diesen Store — eine akkumulierende Summe NICHT.
/// </summary>
public sealed class SpendenTopfAtLeastOnceStore : IProjectionTracker, ISpendenTopfWriteStore
{
    private readonly IDocumentStore _store;

    public SpendenTopfAtLeastOnceStore(IDocumentStore store) => _store = store;

    public async Task AddiereAsync(Guid kampagne, decimal betrag, DateTimeOffset wann)
    {
        // SOFORT committen — getrennt von der Marke. DAS ist der at-least-once-Bruch.
        await using var s = _store.LightweightSession();
        var topf = await s.LoadAsync<SpendenTopfReadModel>(kampagne)
                   ?? new SpendenTopfReadModel { Id = kampagne };
        topf.Summe += betrag;
        topf.Anzahl += 1;
        topf.Aktualisiert = wann;
        s.Store(topf);
        await s.SaveChangesAsync();
    }

    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        // NUR die Marke — zweite, getrennte Transaktion (der Effekt ist längst durabel).
        await using var s = _store.LightweightSession();
        s.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId, StreamId = streamId, Version = version,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await s.SaveChangesAsync(ct);
    }

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
