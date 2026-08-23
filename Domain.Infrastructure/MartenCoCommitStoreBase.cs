using Abstractions;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Marten-Implementierung der Co-Commit-Naht (<see cref="ICoCommitTracker"/>) — die
/// technik­spezifische, in ALLEN Projektions-Stores identische Exactly-once-Maschinerie an EINER
/// auditierten Stelle. Write-Effekte werden nur GEPUFFERT; <see cref="MarkProcessedAsync"/> spielt
/// sie + den <see cref="ProjectionCheckpoint"/> in EINER Marten-<c>IdentitySession</c> ab und
/// committet mit EINEM <c>SaveChangesAsync</c> → Effekt und Marke werden gemeinsam durabel
/// (exactly-once, auch für nicht-idempotente Append-Effekte).
///
/// Konkrete Stores erben und schreiben nur noch ihre FACHLICHEN Write-Methoden; sie puffern über
/// <see cref="Enqueue"/> / <see cref="EnqueueStore{T}"/>. Kein Domänenwissen hier, kein Generat —
/// reiner Marten-Adapter. TRANSIENT registrieren (frisch pro Stream-Actor → isolierter Puffer).
/// </summary>
public abstract class MartenCoCommitStoreBase : ICoCommitTracker
{
    /// <summary>Der geteilte Document-Store (eigene Sessions je Operation).</summary>
    protected IDocumentStore Store { get; }

    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    protected MartenCoCommitStoreBase(IDocumentStore store) => Store = store;

    /// <summary>
    /// Puffert einen Effekt für den Commit-Batch. Läuft in <see cref="MarkProcessedAsync"/> in der
    /// gemeinsamen <c>IdentitySession</c> (read-your-writes über die Identity-Map), in Reihenfolge.
    /// </summary>
    protected void Enqueue(Func<IDocumentSession, Task> op) => _pending.Add(op);

    /// <summary>Bequemlichkeit für den häufigsten Effekt: ein Dokument speichern. Gibt <c>Task.CompletedTask</c>
    /// zurück, damit fachliche Write-Methoden expression-bodied bleiben (<c>=> EnqueueStore(doc)</c>).</summary>
    protected Task EnqueueStore<T>(T document) where T : notnull
    {
        _pending.Add(s => { s.Store(document); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Puffert ein read-modify-store: lädt das Dokument im Batch (read-your-writes über die Identity-Map),
    /// transformiert und staged es. Fehlt es (Event vor dem erzeugenden Upsert), passiert nichts — der
    /// nächste Batch heilt. Gibt <c>Task.CompletedTask</c> zurück (expression-bodied Write-Methoden).
    /// </summary>
    protected Task EnqueueTransform<T>(Guid id, Func<T, T> transform) where T : notnull
    {
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<T>(id);
            if (existing is null) return;   // Event vor dem erzeugenden Upsert → im nächsten Batch geheilt
            s.Store(transform(existing));
        });
        return Task.CompletedTask;
    }

    // ── Marke lesen: reiner Read, klammert den Batch-Beginn ──
    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();   // neuer Batch → alten Vorlauf verwerfen
        await using var s = Store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    // ── Commit-Punkt: Effekt(e) + Marke in EINER Session, ein SaveChanges ──
    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = Store.IdentitySession();   // read-your-writes im Batch
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

        await s.SaveChangesAsync(ct);   // EIN Commit → Effekt(e) + Marke gemeinsam gültig
        _pending.Clear();
    }

    // ── Replay / Rebuild ──
    public async Task ResetAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var s = Store.LightweightSession();
        s.Delete<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projectionId, streamId));
        await s.SaveChangesAsync(ct);
    }

    public async Task ResetAllAsync(string projectionId, CancellationToken ct)
    {
        await using var s = Store.LightweightSession();
        s.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projectionId);
        await s.SaveChangesAsync(ct);
    }
}
