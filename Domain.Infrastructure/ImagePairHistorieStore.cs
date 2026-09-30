using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der ImagePairHistorie (Append-Projektion, Muster B: Read + Write auf derselben
/// Klasse). Exactly-once-Naht in <see cref="MartenCoCommitStoreBase"/>; <see cref="AppendEintragAsync"/>
/// puffert einen load-or-create-append als Effekt. SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed class ImagePairHistorieStore
    : MartenCoCommitStoreBase, IImagePairHistorieStore
{
    public ImagePairHistorieStore(IDocumentStore store) : base(store) { }

    // ─── Write: nur puffern (load-or-create-append im Commit-Batch) ───
    public Task AppendEintragAsync(Guid pairId, HistorieEintrag eintrag)
    {
        Enqueue(async s =>
        {
            var existing = await s.LoadAsync<ImagePairHistorieReadModel>(pairId);
            s.Store(existing is null
                ? new ImagePairHistorieReadModel { Id = pairId, Eintraege = new List<HistorieEintrag> { eintrag } }
                : existing with { Eintraege = new List<HistorieEintrag>(existing.Eintraege) { eintrag } });
        });
        return Task.CompletedTask;
    }

    // ─── Read ───
    public async Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Guid pairId)
    {
        await using var s = Store.QuerySession();
        return await s.LoadAsync<ImagePairHistorieReadModel>(pairId);
    }
}
