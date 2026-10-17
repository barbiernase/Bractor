using Abstractions;
using Marten;
using Marten.Patching;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IProzessOffenIndex"/>: ein durables <see cref="ProzessOffen"/>-Dokument je
/// offener Prozess-Korrelation. Mit dem <c>ProzessGestartet</c> gesetzt, mit dem <c>ProzessBeendet</c> gelöscht (Beilagen des Appends,
/// <see cref="BeilagenSchreiber"/>);
/// der Backstop-Scan liest die (kleine) Menge der offenen Einträge — O(offen), nicht O(Historie).
///
/// Eigene, kurze Session je Zugriff (der Index ist unkritisch — siehe Vertrags-Doku am Interface: fehlt/lügt
/// ein Eintrag, ist das Verhalten nie schlechter als ohne Backstop).
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;ProzessOffen&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenProzessOffenIndex : IProzessOffenIndex
{
    private readonly IDocumentStore _store;

    public MartenProzessOffenIndex(IDocumentStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task MarkiereBeendetAsync(Guid korrelation, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Delete<ProzessOffen>(korrelation);
        await session.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListeOffeneAsync(CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await session.Query<ProzessOffen>().Select(x => x.Id).ToListAsync(ct);
    }

    /// <summary>Patch statt Store: ein inzwischen beendeter (gelöschter) Eintrag entsteht nicht neu.</summary>
    public async Task SetzeFristAsync(Guid korrelation, DateTimeOffset? frist, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Patch<ProzessOffen>(korrelation).Set(x => x.Frist, frist);
        await session.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListeFaelligeAsync(DateTimeOffset bis, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await FaelligeAbfrage(session, bis).ToListAsync(ct);
    }

    /// <summary>Die Abfrage der fälligen Vorgänge (jede Sekunde vom Backstop) — eine Quelle für den Index und den Plan-Test.</summary>
    public static IQueryable<Guid> FaelligeAbfrage(IQuerySession session, DateTimeOffset bis)
        => session.Query<ProzessOffen>().Where(x => x.Frist != null && x.Frist <= bis).Select(x => x.Id);
}
