using Abstractions;

namespace Domain.Projections.ImagePair;

/// <summary>
/// Query-Handler für die Historie-Projektion.
///
/// Beantwortet GetImagePairHistorie-Queries gegen den ReadStore.
/// TrackDeps=true damit der Client Aggregate-Versionen für
/// Stale Detection bekommt.
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class ImagePairHistorieReader : IReader<ImagePairHistorieProjektion>
{
    public async Task<OneOf<ImagePairHistorieAntwort, ImagePairHistorieNichtGefunden>>
        Handle(GetImagePairHistorie query, IMessageEnvelope envelope, ReadContext ctx, IGetHistorie getHistorie)
    {
        var model = await getHistorie.GetByPairIdAsync(query.PairId.Wert);

        if (model != null)
        {
            ctx.Track(query.PairId.ToString());
            return new ImagePairHistorieAntwort(
                query.PairId,
                model.Eintraege);
        }

        return new ImagePairHistorieNichtGefunden(query.PairId);
    }
}
