using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Lese-Seite des <see cref="DatensatzStore"/> (Teil derselben Klasse, eine Instanz je DI-Bereich): eigene
/// Query-Sessions, sieht committete Daten. Jede Methode bedient genau eine Lese-Fähigkeit.
/// </summary>
public sealed partial class DatensatzStore
{
    public async Task<DatensatzReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<DatensatzReadModel>(id);
    }

    public async Task<IReadOnlyList<DatensatzReadModel>> GetAlleAsync()
    {
        await using var session = Store.QuerySession();
        return await session.Query<DatensatzReadModel>()
            .OrderByDescending(m => m.LetzteAktualisierung)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<DatensatzSampleReadModel> Items, int GesamtAnzahl)> HoleSamplesAsync(
        Guid datensatzId, int version, int seite, int seitenGroesse)
    {
        await using var session = Store.QuerySession();

        var query = session.Query<DatensatzSampleReadModel>()
            .Where(s => s.DatensatzId == datensatzId && s.Version == version);

        var gesamtAnzahl = await query.CountAsync();

        var page = await query
            .OrderBy(s => s.Id)                          // zusammengesetzte Id → stabil, reproduzierbar
            .Skip((seite - 1) * seitenGroesse)
            .Take(seitenGroesse)
            .ToListAsync();

        return (page, gesamtAnzahl);
    }

    public async Task<IReadOnlyList<DatensatzReadModel>> HoleDatensaetzeFuerPaarAsync(Guid imagePairId)
    {
        await using var session = Store.QuerySession();

        var doc = await session.LoadAsync<DatensatzMitgliedschaftReadModel>(imagePairId);
        if (doc is null || doc.DatensatzIds.Count == 0)
            return Array.Empty<DatensatzReadModel>();

        // Join: Kopf-Daten (Name/Status/Version) frisch aus den Datensatz-Read-Models.
        var modelle = await session.LoadManyAsync<DatensatzReadModel>(doc.DatensatzIds.ToArray());
        return modelle.ToList();
    }
}
