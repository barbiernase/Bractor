using Abstractions;
using Domain.Projections;
using Marten;
using Domain.Projections.Modell;

using ModellAgg = Domain.Modell.Modell;

namespace Domain.Infrastructure.Modell;

/// <summary>
/// Lese-Seite des <see cref="ModellStore"/> (Teil derselben Klasse, eine Instanz je DI-Bereich): eigene
/// Query-Sessions, sieht committete Daten. Jede Methode bedient genau eine Lese-Fähigkeit.
/// </summary>
public sealed partial class ModellStore
{
    public async Task<ModellReadModel?> FindByIdAsync(Id<ModellAgg> id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<ModellReadModel>(id.Wert);
    }

    public async Task<IReadOnlyList<ModellReadModel>> GetAlleAsync()
    {
        await using var session = Store.QuerySession();
        return await session.Query<ModellReadModel>()
            .OrderByDescending(m => m.RegistriertAm)
            .ToListAsync();
    }

    public async Task<AktivesModellReadModel?> GetAktivesAsync()
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<AktivesModellReadModel>(AktivesModellReadModel.Singleton);
    }
}
