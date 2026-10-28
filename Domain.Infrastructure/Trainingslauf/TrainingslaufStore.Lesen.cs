using Abstractions;
using Domain.Projections;
using Marten;
using Domain.Projections.Trainingslauf;

using TrainingslaufAgg = Domain.Trainingslauf.Trainingslauf;

namespace Domain.Infrastructure.Trainingslauf;

/// <summary>
/// Lese-Seite des <see cref="TrainingslaufStore"/> (Teil derselben Klasse, eine Instanz je DI-Bereich): eigene
/// Query-Sessions, sieht committete Daten. Jede Methode bedient genau eine Lese-Fähigkeit.
/// </summary>
public sealed partial class TrainingslaufStore
{
    public async Task<TrainingslaufReadModel?> FindByIdAsync(Id<TrainingslaufAgg> id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<TrainingslaufReadModel>(id.Wert);
    }

    public async Task<IReadOnlyList<TrainingslaufReadModel>> GetAlleAsync()
    {
        await using var session = Store.QuerySession();
        return await session.Query<TrainingslaufReadModel>()
            .OrderByDescending(m => m.LetzteAktualisierung)
            .ToListAsync();
    }
}
