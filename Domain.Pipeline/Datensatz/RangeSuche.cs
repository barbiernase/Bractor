using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;      // Klassifikation
using Domain.Projections;    // ImagePairFilter, ImagePairReadModel
using Microsoft.Extensions.Logging;
using Domain.Projections.ImagePair;

namespace Domain.Pipeline.Datensatz;

/// <summary>
/// <see cref="IRangeSuche"/> gegen den ImagePair-Read-Store: der 1:1 aus <see cref="RangeKriterien"/> gebaute
/// <see cref="ImagePairFilter"/>, durchpaginiert. Die Fähigkeit kommt als Parameter (je Aufruf ein Bereich), nie über den Ctor.
/// </summary>
public sealed class RangeSuche(ILogger<RangeSuche> logger) : IRangeSuche
{
    private const int SeitenGroesse = 500;

    public async Task<OneOf<RangeGefunden, RangeOhneTreffer>> RufeAsync(SucheRange auftrag, IAusfuehrung x, ISearchImagePairs suche)
    {
        var ids = new List<Id<Domain.ImagePair.ImagePair>>();
        for (var seite = 1; ; seite++)
        {
            x.Abbruch.ThrowIfCancellationRequested();
            var (items, gesamt) = await suche.SearchAsync(ZuFilter(auftrag.Kriterien, seite, SeitenGroesse));
            ids.AddRange(items.Select(m => new Id<Domain.ImagePair.ImagePair>(m.Id)));
            if (items.Count == 0 || ids.Count >= gesamt) break;
        }

        if (ids.Count == 0)
        {
            logger.LogInformation("Range-Suche {Id}: 0 Paare — nichts aufzunehmen", x.AusfuehrungsId);
            return new RangeOhneTreffer(auftrag.Kriterien);
        }
        logger.LogInformation("Range-Suche {Id}: {Count} Paare aufgelöst", x.AusfuehrungsId, ids.Count);
        return new RangeGefunden(ids, new RangeHerkunft(auftrag.Kriterien, ids.Count));
    }

    private static ImagePairFilter ZuFilter(RangeKriterien k, int seite, int seitenGroesse) => new(
        Von: k.Von,
        Bis: k.Bis,
        KiKlassifikation: k.KiKlassifikation,
        MenschLabel: k.MenschLabel,
        ProduktLabel: k.ProduktLabel,
        NurKomplette: k.NurKomplette,
        HatKiKlassifikation: k.HatKiKlassifikation,
        HatMenschLabel: k.HatMenschLabel,
        NurNichtInspizierte: k.NurNichtInspizierte,
        Seite: seite,
        SeitenGroesse: seitenGroesse);
}
