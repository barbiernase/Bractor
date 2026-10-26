using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;      // Klassifikation
using Domain.Projections;    // ImagePairFilter, ImagePairReadModel
using Microsoft.Extensions.Logging;
using Domain.Projections.ImagePair;

namespace Domain.Pipeline.Datensatz;

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
            if (await finde.FindByIdAsync(pid.Wert) is { } ip) modelle[pid.Wert] = ip;
            else logger.LogWarning("Einfrieren {Id}: ImagePair {Pid} fehlt im Read-Model — übersprungen", x.AusfuehrungsId, pid);
        }
        if (modelle.Count == 0) return new KeinMitgliedAuffindbar(auftrag.Mitglieder.Count);

        var splits = SplitZuteiler.Zuteilen(modelle.Select(kv => (kv.Key, BestimmeLabel(kv.Value))).ToList(), auftrag.Split);
        var mitglieder = modelle
            .OrderBy(kv => kv.Key)
            .Select(kv => new DatensatzMitglied(
                ImagePairId: new Id<Domain.ImagePair.ImagePair>(kv.Key),
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
