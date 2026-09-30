using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Lese-Seite des <see cref="TrainingslaufStore"/> (Teil derselben Klasse, eine Instanz je DI-Bereich): eigene
/// Query-Sessions, sieht committete Daten. Jede Methode bedient genau eine Lese-Fähigkeit.
/// </summary>
public sealed partial class TrainingslaufStore
{
    public async Task<TrainingslaufReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<TrainingslaufReadModel>(id);
    }

    public async Task<IReadOnlyList<TrainingslaufReadModel>> GetAlleAsync()
    {
        await using var session = Store.QuerySession();
        return await session.Query<TrainingslaufReadModel>()
            .OrderByDescending(m => m.LetzteAktualisierung)
            .ToListAsync();
    }
}
