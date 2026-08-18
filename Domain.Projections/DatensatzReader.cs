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

    public DatensatzReader(IDatensatzReadStore store)
    {
        _store = store;
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
        Ranges: model.Ranges);
}
