using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IFristplan"/>: ein durables <see cref="Frist"/>-Dokument je offener Deadline.
/// Die Fälligkeits-Abfrage vergleicht gegen die übergebene DB-Zeit (der Scheduler holt sie aus
/// <see cref="IDbClock"/>) — nie gegen Node-Zeit.
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;Frist&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenFristplan : IFristplan
{
    private readonly IDocumentStore _store;

    public MartenFristplan(IDocumentStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task PlaneAsync(Frist frist, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        session.Store(frist);          // idempotent über die Id (Upsert)
        await session.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Frist>> FälligeAsync(DateTimeOffset jetzt, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        return await FaelligeAbfrage(session, jetzt).ToListAsync(ct);
    }

    /// <summary>
    /// Die Abfrage der fälligen Fristen (je Takt des Schedulers) — eine Quelle für den Index und den Plan-Test. Sortiert und begrenzt, damit auch
    /// der allgemeine Plan den Index nimmt; den Rest holt der nächste Takt.
    /// </summary>
    public static IQueryable<Frist> FaelligeAbfrage(IQuerySession session, DateTimeOffset jetzt)
        => session.Query<Frist>().Where(f => f.Fällig <= jetzt).OrderBy(f => f.Fällig).Take(HoechstensJeTakt);

    /// <summary>So viele fällige Fristen liefert eine Abfrage höchstens (die frühesten).</summary>
    public const int HoechstensJeTakt = 1000;

    public async Task EntferneAsync(Guid id, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        session.Delete<Frist>(id);
        await session.SaveChangesAsync(ct);
    }
}
