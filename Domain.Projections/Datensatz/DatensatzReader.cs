using Abstractions;
using Domain.Projections.ImagePair;
using DatensatzId = Abstractions.Id<Domain.Datensatz.Datensatz>;
using ImagePairId = Abstractions.Id<Domain.ImagePair.ImagePair>;

namespace Domain.Projections.Datensatz;

/// <summary>
/// Reader der Datensatz-Projektion (analog <see cref="ImagePairReader"/>). Beantwortet die
/// Datensatz-Queries über Lese-Fähigkeiten des <see cref="IDatensatzStore"/> — derselbe Kanal für Blazor
/// und Python.
///
/// Was eine Antwort zeigt, steht in ihren <c>Id&lt;T&gt;</c>-Feldern und in der Query (etwa die Datensatz-Id der Samples: direkt nach dem Einfrieren
/// sind die Sample-Zeilen vielleicht noch nicht geschrieben — der Stand des Datensatzes lässt den Client nachfassen).
/// </summary>
public sealed class DatensatzReader : IReader
{
    public async Task<DatensatzSamples> Frage(
        HoleDatensatzSamples query, IHoleSamples holeSamples)
    {
        var (items, gesamtAnzahl) = await holeSamples.HoleSamplesAsync(
            query.DatensatzId, query.Version, query.Seite, query.SeitenGroesse);

        var samples = items
            .Select(s => new DatensatzSample(s.ImagePairId, s.Dc0Pfad, s.Dc2Pfad, s.Label, s.Split))
            .ToList();

        // Das Datensatz-Aggregat als Dep tracken: die Samples sind zwar ein immutabler Snapshot, aber
        // DIREKT nach dem Einfrieren kann die (asynchrone) Projektion die Sample-Zeilen noch nicht
        // geschrieben haben. Der Dep (auf die DatensatzEingefroren-Version) lässt den Client per
        // Read-Your-Writes nachfassen, bis die Balance-Grundlage wirklich da ist.

        return new DatensatzSamples(samples, gesamtAnzahl, query.Seite, query.SeitenGroesse);
    }

    public async Task<OneOf<DatensatzAntwort, DatensatzNichtGefunden>> Frage(
        HoleDatensatz query, IFindDatensatz findDatensatz)
    {
        var model = await findDatensatz.FindByIdAsync(query.DatensatzId);

        if (model is null)
            return new DatensatzNichtGefunden(query.DatensatzId);

        return ToAntwort(model);
    }

    public async Task<ImagePairSuchergebnis> Frage(
        HoleDatensatzPaare query, IFindDatensatz findDatensatz, ILadeVieleImagePairs ladeVieleImagePairs)
    {
        var model = await findDatensatz.FindByIdAsync(query.DatensatzId);
        var alle = model is null
            ? new List<ImagePairId>()
            : (query.Modus == DatensatzPaarModus.Ausgeschlossen ? model.Ausgeschlossen : model.Mitglieder);

        var gesamt = alle.Count;
        var seitenIds = alle
            .Skip((query.Seite - 1) * query.SeitenGroesse)
            .Take(query.SeitenGroesse)
            .ToList();

        var records = await ladeVieleImagePairs.LadeVieleAsync(seitenIds);
        var nachId = records.ToDictionary(r => r.Id);

        var items = new List<ImagePairAntwort>();
        foreach (var id in seitenIds)                         // Reihenfolge der Id-Liste bewahren
            if (nachId.TryGetValue(id, out var m))
            {
                items.Add(ImagePairReader.ToAntwort(m));
            }

        return new ImagePairSuchergebnis(items, gesamt, query.Seite, query.SeitenGroesse);
    }

    public async Task<DatensaetzeFuerPaar> Frage(
        HoleDatensaetzeFuerPaar query, IHoleDatensaetzeFuerPaar holeDatensaetzeFuerPaar)
    {
        var modelle = await holeDatensaetzeFuerPaar.HoleDatensaetzeFuerPaarAsync(query.ImagePairId);

        var tags = modelle
            .Select(m => new DatensatzTag(m.Id, m.Name, m.Status, m.EingefroreneVersion))
            .ToList();

        return new DatensaetzeFuerPaar(query.ImagePairId, tags);
    }

    public async Task<DatensatzListe> Frage(
        HoleDatensaetze query, IGetAlleDatensaetze getAlleDatensaetze)
    {
        var modelle = await getAlleDatensaetze.GetAlleAsync();

        var items = modelle.Select(m =>
        {
            return ToAntwort(m);
        }).ToList();

        return new DatensatzListe(items);
    }

    private static DatensatzAntwort ToAntwort(DatensatzReadModel model) => new(
        Id: model.Id,
        Name: model.Name,
        Status: model.Status,
        AnzahlMitglieder: model.AnzahlMitglieder,
        EingefroreneVersion: model.EingefroreneVersion,
        Split: model.Split,
        Ranges: model.Ranges,
        Mitglieder: model.Mitglieder,
        Ausgeschlossen: model.Ausgeschlossen);
}
