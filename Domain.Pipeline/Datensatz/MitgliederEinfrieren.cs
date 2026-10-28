using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;      // Klassifikation
using Domain.Projections;    // ImagePairReadModel
using Domain.Projections.ImagePair;

namespace Domain.Pipeline.Datensatz;

/// <summary>
/// Ein angefordertes Einfrieren abschließen: je (autoritativem) Mitglied Label-Stand + Bildpfade aus dem Read-Model, Split deterministisch und
/// stratifiziert (<see cref="SplitZuteiler"/>), Snapshot in stabiler Reihenfolge. Fehlt ein Mitglied im Read-Model, wird es übersprungen.
/// </summary>
public sealed class MitgliederEinfrieren : IFunktion
{
    public async Task<OneOf<SchliesseEinfrierenAb, KeinMitgliedAuffindbar>> Auf(EinfrierenAngefordert e, Herkunft h, IFindImagePair finde)
    {
        var modelle = new Dictionary<Guid, ImagePairReadModel>();
        foreach (var pid in e.Mitglieder)
            if (await finde.FindByIdAsync(pid) is { } ip) modelle[pid.Wert] = ip;
        if (modelle.Count == 0) return new KeinMitgliedAuffindbar(e.Mitglieder.Count);

        var splits = SplitZuteiler.Zuteilen(modelle.Select(kv => (kv.Key, BestimmeLabel(kv.Value))).ToList(), e.Split);
        var mitglieder = modelle
            .OrderBy(kv => kv.Key)
            .Select(kv => new DatensatzMitglied(
                ImagePairId: new Id<Domain.ImagePair.ImagePair>(kv.Key),
                Label: BestimmeLabel(kv.Value),
                Split: splits[kv.Key],
                Dc0Pfad: kv.Value.Dc0Pfad ?? "",
                Dc2Pfad: kv.Value.Dc2Pfad ?? ""))
            .ToList();
        return new SchliesseEinfrierenAb(new Id<Domain.Datensatz.Datensatz>(h.Stream), mitglieder);
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
