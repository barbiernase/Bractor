using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IProzessOffenIndex"/>: ein durables <see cref="ProzessOffen"/>-Dokument je
/// offener Prozess-Korrelation. Beim <c>ProzessGestartet</c> gesetzt, beim <c>ProzessBeendet</c> gelöscht;
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

    public async Task MarkiereOffenAsync(Guid korrelation, string prozessName, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(new ProzessOffen
        {
            Id = korrelation,
            ProzessName = prozessName,
            SeitUtc = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
    }

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
}
