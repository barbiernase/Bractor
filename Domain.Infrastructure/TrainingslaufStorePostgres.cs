using Domain.Projections;
using Marten;
using Microsoft.Extensions.Logging;

namespace Domain.Infrastructure;

/// <summary>
/// PostgreSQL-Read-Store der Trainingslauf-Projektion (Marten, Schema "rm"), Singleton —
/// analog <see cref="DatensatzStorePostgres"/>. Write-Seite ist der Co-Commit-Store
/// <see cref="TrainingslaufStore"/>.
/// </summary>
public class TrainingslaufStorePostgres : ITrainingslaufReadStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<TrainingslaufStorePostgres> _logger;

    public TrainingslaufStorePostgres(IDocumentStore store, ILogger<TrainingslaufStorePostgres> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TrainingslaufReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<TrainingslaufReadModel>(id);
    }

    public async Task<IReadOnlyList<TrainingslaufReadModel>> GetAlleAsync()
    {
        await using var session = _store.QuerySession();
        return await session.Query<TrainingslaufReadModel>()
            .OrderByDescending(m => m.LetzteAktualisierung)
            .ToListAsync();
    }
}
