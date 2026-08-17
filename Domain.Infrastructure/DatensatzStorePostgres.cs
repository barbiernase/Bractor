using Domain.Projections;
using Marten;
using Microsoft.Extensions.Logging;

namespace Domain.Infrastructure;

/// <summary>
/// PostgreSQL-Read-Store der Datensatz-Projektion (Marten Document Store, Schema "rm").
/// Eigene Query-Sessions (sieht committete Daten) — Singleton, analog
/// <see cref="ImagePairStorePostgres"/>. Die Write-Seite ist der Co-Commit-Store
/// <see cref="DatensatzStore"/>.
/// </summary>
public class DatensatzStorePostgres : IDatensatzReadStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<DatensatzStorePostgres> _logger;

    public DatensatzStorePostgres(IDocumentStore store, ILogger<DatensatzStorePostgres> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatensatzReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<DatensatzReadModel>(id);
    }

    public async Task<IReadOnlyList<DatensatzReadModel>> GetAlleAsync()
    {
        await using var session = _store.QuerySession();
        return await session.Query<DatensatzReadModel>()
            .OrderByDescending(m => m.LetzteAktualisierung)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<DatensatzSampleReadModel> Items, int GesamtAnzahl)> HoleSamplesAsync(
        Guid datensatzId, int version, int seite, int seitenGroesse)
    {
        await using var session = _store.QuerySession();

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
}
