using Abstractions;

namespace Domain.Projections.ImagePair;

/// <summary>
/// Reader der Historie-Projektion: beantwortet GetImagePairHistorie über den Historien-Store.
/// </summary>
public sealed class ImagePairHistorieReader : IReader
{
    public async Task<OneOf<ImagePairHistorieAntwort, ImagePairHistorieNichtGefunden>>
        Frage(GetImagePairHistorie query, IGetHistorie getHistorie)
    {
        var model = await getHistorie.GetByPairIdAsync(query.PairId);

        if (model != null)
        {
            return new ImagePairHistorieAntwort(
                query.PairId,
                model.Eintraege);
        }

        return new ImagePairHistorieNichtGefunden(query.PairId);
    }
}
