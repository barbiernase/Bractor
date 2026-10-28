using Abstractions;
using Core;
using Domain.Projections;
using Marten;
using Domain.Projections.ImagePair;


namespace Domain.Infrastructure.ImagePair;

/// <summary>
/// Store (Klasse 1) der ImagePairHistorie (Append-Projektion, Muster B: Read + Write auf derselben
/// Klasse). Exactly-once-Naht in <see cref="MartenStore"/>; <see cref="AppendEintragAsync"/>
/// puffert einen load-or-create-append als Effekt. SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed class ImagePairHistorieStore
    : MartenStore, IImagePairHistorieStore
{
    public ImagePairHistorieStore(IDocumentStore store) : base(store, typeof(ImagePairHistorieReadModel)) { }

    // ─── Write: nur puffern (load-or-create-append im Commit-Batch) ───
    public Task AppendEintragAsync(Id<Domain.ImagePair.ImagePair> pairId, HistorieEintrag eintrag)
        => EnqueueAnlegenOderAendern<ImagePairHistorieReadModel>(pairId.Wert, existing => existing is null
            ? new ImagePairHistorieReadModel { Id = pairId, Eintraege = new List<HistorieEintrag> { eintrag } }
            : existing with { Eintraege = new List<HistorieEintrag>(existing.Eintraege) { eintrag } });

    // ─── Read ───
    public async Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Id<Domain.ImagePair.ImagePair> pairId)
    {
        await using var s = Store.QuerySession();
        return await s.LoadAsync<ImagePairHistorieReadModel>(pairId.Wert);
    }
}
