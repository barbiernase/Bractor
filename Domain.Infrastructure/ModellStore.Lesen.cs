using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Lese-Seite des <see cref="ModellStore"/> (Teil derselben Klasse, eine Instanz je DI-Bereich): eigene
/// Query-Sessions, sieht committete Daten. Jede Methode bedient genau eine Lese-Fähigkeit.
/// </summary>
public sealed partial class ModellStore
{
    public async Task<ModellReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<ModellReadModel>(id);
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
