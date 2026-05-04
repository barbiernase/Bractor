using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Query-Handler für die Historie-Projektion.
///
/// Beantwortet GetImagePairHistorie-Queries gegen den ReadStore.
/// TrackDeps=true damit der Client Aggregate-Versionen für
/// Stale Detection bekommt.
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class ImagePairHistorieReader : IReader<ImagePairHistorieProjection>
{
    private readonly IImagePairHistorieReadStore _store;

    public ImagePairHistorieReader(IImagePairHistorieReadStore store)
    {
        _store = store;
    }

    public async Task<OneOf<ImagePairHistorieAntwort, ImagePairHistorieNichtGefunden>>
        Handle(GetImagePairHistorie query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.GetByPairIdAsync(query.PairId);

        if (model != null)
        {
            ctx.Track(query.PairId.ToString());
            return new ImagePairHistorieAntwort(
                model.Id,
                model.Eintraege);
        }

        return new ImagePairHistorieNichtGefunden(query.PairId);
    }
}