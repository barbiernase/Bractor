using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Reader der Datensatz-Projektion (analog <see cref="ImagePairReader"/>). Beantwortet die
/// Datensatz-Queries über den <see cref="IDatensatzReadStore"/> — derselbe Kanal für Blazor
/// und Python (Konzept §6/§7).
///
/// Die Samples sind ein <em>immutabler</em> Snapshot → kein Deps-Tracking nötig (read-only,
/// Konzept §7.2). Beim Kopf-Read wird die Datensatz-Id getrackt (Stale-Detection wie ImagePair).
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class DatensatzReader : IReader<DatensatzProjektion>
{
    private readonly IDatensatzReadStore _store;
    private readonly IImagePairReadStore _imagePairs;

    public DatensatzReader(IDatensatzReadStore store, IImagePairReadStore imagePairs)
    {
        _store = store;
        _imagePairs = imagePairs;
    }

    public async Task<DatensatzSamples> Handle(
        HoleDatensatzSamples query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var (items, gesamtAnzahl) = await _store.HoleSamplesAsync(
            query.DatensatzId, query.Version, query.Seite, query.SeitenGroesse);

        var samples = items
            .Select(s => new DatensatzSample(s.ImagePairId, s.Dc0Pfad, s.Dc2Pfad, s.Label, s.Split))
            .ToList();

        // Das Datensatz-Aggregat als Dep tracken: die Samples sind zwar ein immutabler Snapshot, aber
        // DIREKT nach dem Einfrieren kann die (asynchrone) Projektion die Sample-Zeilen noch nicht
        // geschrieben haben. Der Dep (auf die DatensatzEingefroren-Version) lässt den Client per
        // Read-Your-Writes nachfassen, bis die Balance-Grundlage wirklich da ist.
        ctx.Track(query.DatensatzId.ToString());

        return new DatensatzSamples(samples, gesamtAnzahl, query.Seite, query.SeitenGroesse);
    }

    public async Task<OneOf<DatensatzAntwort, DatensatzNichtGefunden>> Handle(
        HoleDatensatz query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.FindByIdAsync(query.DatensatzId);

        if (model is null)
            return new DatensatzNichtGefunden(query.DatensatzId);

        ctx.Track(query.DatensatzId.ToString());
        return ToAntwort(model);
    }

    public async Task<ImagePairSuchergebnis> Handle(
        HoleDatensatzPaare query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.FindByIdAsync(query.DatensatzId);
        var alle = model is null
            ? new List<Guid>()
            : (query.Modus == DatensatzPaarModus.Ausgeschlossen ? model.Ausgeschlossen : model.Mitglieder);

        var gesamt = alle.Count;
        var seitenIds = alle
            .Skip((query.Seite - 1) * query.SeitenGroesse)
            .Take(query.SeitenGroesse)
            .ToList();

        var records = await _imagePairs.LadeVieleAsync(seitenIds);
        var nachId = records.ToDictionary(r => r.Id);

        var items = new List<ImagePairAntwort>();
        foreach (var id in seitenIds)                         // Reihenfolge der Id-Liste bewahren
            if (nachId.TryGetValue(id, out var m))
            {
                ctx.Track(id.ToString());
                items.Add(ImagePairReader.ToAntwort(m));
            }

        ctx.Track(query.DatensatzId.ToString());              // RYW: Sicht folgt dem Delta
        return new ImagePairSuchergebnis(items, gesamt, query.Seite, query.SeitenGroesse);
    }

    public async Task<DatensaetzeFuerPaar> Handle(
        HoleDatensaetzeFuerPaar query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var modelle = await _store.HoleDatensaetzeFuerPaarAsync(query.ImagePairId);

        var tags = modelle
            .Select(m => new DatensatzTag(m.Id, m.Name, m.Status, m.EingefroreneVersion))
            .ToList();

        // Rückwärts-Index gegen die betroffenen Aggregate tracken (Read-Your-Writes nach dem Taggen).
        ctx.Track(query.ImagePairId.ToString());
        foreach (var m in modelle) ctx.Track(m.Id.ToString());

        return new DatensaetzeFuerPaar(query.ImagePairId, tags);
    }

    public async Task<DatensatzListe> Handle(
        HoleDatensaetze query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var modelle = await _store.GetAlleAsync();

        var items = modelle.Select(m =>
        {
            ctx.Track(m.Id.ToString());
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
