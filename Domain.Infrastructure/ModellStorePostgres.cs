using Domain.Projections;
using Marten;
using Microsoft.Extensions.Logging;

namespace Domain.Infrastructure;

/// <summary>
/// PostgreSQL-Read-Store der Modell-Projektion (Marten Document Store, Schema "rm"). Singleton,
/// eigene Query-Sessions — analog <see cref="DatensatzStorePostgres"/>.
/// </summary>
public class ModellStorePostgres : IModellReadStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<ModellStorePostgres> _logger;

    public ModellStorePostgres(IDocumentStore store, ILogger<ModellStorePostgres> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ModellReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<ModellReadModel>(id);
    }

    public async Task<IReadOnlyList<ModellReadModel>> GetAlleAsync()
    {
        await using var session = _store.QuerySession();
        return await session.Query<ModellReadModel>()
            .OrderByDescending(m => m.RegistriertAm)
            .ToListAsync();
    }

    public async Task<AktivesModellReadModel?> GetAktivesAsync()
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<AktivesModellReadModel>(AktivesModellReadModel.Singleton);
    }
}
