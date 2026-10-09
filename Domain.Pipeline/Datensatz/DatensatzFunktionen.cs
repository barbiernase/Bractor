using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;      // Klassifikation
using Domain.Projections;    // ImagePairFilter, ImagePairReadModel
using Microsoft.Extensions.Logging;

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
        var ids = new List<Guid>();
        for (var seite = 1; ; seite++)
        {
            x.Abbruch.ThrowIfCancellationRequested();
            var (items, gesamt) = await suche.SearchAsync(ZuFilter(auftrag.Kriterien, seite, SeitenGroesse));
            ids.AddRange(items.Select(m => m.Id));
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

/// <summary>
/// <see cref="IMitgliederEinfrieren"/>: je (autoritativem) Mitglied Label-Stand + Bildpfade aus dem Read-Model, Split deterministisch
/// und stratifiziert (<see cref="SplitZuteiler"/>), Snapshot in stabiler Reihenfolge.
/// </summary>
public sealed class MitgliederEinfrieren(ILogger<MitgliederEinfrieren> logger) : IMitgliederEinfrieren
{
    public async Task<OneOf<MitgliederEingefroren, KeinMitgliedAuffindbar>> RufeAsync(FriereMitgliederEin auftrag, IAusfuehrung x, IFindImagePair finde)
    {
        var modelle = new Dictionary<Guid, ImagePairReadModel>();
        foreach (var pid in auftrag.Mitglieder)
        {
            x.Abbruch.ThrowIfCancellationRequested();
            if (await finde.FindByIdAsync(pid) is { } ip) modelle[pid] = ip;
            else logger.LogWarning("Einfrieren {Id}: ImagePair {Pid} fehlt im Read-Model — übersprungen", x.AusfuehrungsId, pid);
        }
        if (modelle.Count == 0) return new KeinMitgliedAuffindbar(auftrag.Mitglieder.Count);

        var splits = SplitZuteiler.Zuteilen(modelle.Select(kv => (kv.Key, BestimmeLabel(kv.Value))).ToList(), auftrag.Split);
        var mitglieder = modelle
            .OrderBy(kv => kv.Key)
            .Select(kv => new DatensatzMitglied(
                ImagePairId: kv.Key,
                Label: BestimmeLabel(kv.Value),
                Split: splits[kv.Key],
                Dc0Pfad: kv.Value.Dc0Pfad ?? "",
                Dc2Pfad: kv.Value.Dc2Pfad ?? ""))
            .ToList();
        logger.LogInformation("Einfrieren {Id}: {Count} Mitglieder", x.AusfuehrungsId, mitglieder.Count);
        return new MitgliederEingefroren(mitglieder);
    }

    /// <summary>
    /// Das Trainings-Label eines Paares: das physische Produkt-Label ist die stärkste Wahrheit, danach das menschliche
    /// Bildpaar-Label, zuletzt die KI-Klassifikation.
    /// </summary>
    internal static Klassifikation BestimmeLabel(ImagePairReadModel m) =>
        m.PhysischesProduktLabel
        ?? m.MenschBildpaarLabel
        ?? m.KiBildpaarKlassifikation
        ?? Klassifikation.KeineAnomalie;
}
