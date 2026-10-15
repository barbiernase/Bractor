using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Räumt Quittungen der Inbox ab, die älter als der Horizont sind (E2, Umgebung <c>Schreibpfad.InboxHorizontTage</c>, docs/04 §4.4). Innerhalb
/// des Horizonts wirkt eine wiederholte CommandId garantiert nicht erneut; danach vergisst die Inbox sie.
///
/// Läuft auf jedem Knoten im Takt von 10 min, seitenweise (je Seite eine Transaktion). Die Lease ist ein Transaktions-Advisory-Lock: je Seite
/// löscht höchstens ein Knoten, die anderen gehen leer aus und versuchen es im nächsten Takt — Löschen ist ohnehin idempotent. Die Zeit ist die
/// der Datenbank (Spalte <c>zeit</c> = <c>now()</c> beim Commit der Quittung, Vergleich gegen <c>now()</c>).
/// </summary>
public sealed class InboxAufraeumer(IDocumentStore store, TimeSpan horizont, ILogger<InboxAufraeumer>? logger = null) : BackgroundService
{
    private static readonly TimeSpan Takt = TimeSpan.FromMinutes(10);
    private const int Seite = 10_000;

    /// <summary>Schlüssel des Advisory-Locks (fest, je Datenbank; die Schemata teilen ihn — sie räumen dann nacheinander).</summary>
    private const long LeaseSchluessel = 0x4342_5153_494E_4258;   // "CBQSINBX"

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(Takt, ct); }
            catch (OperationCanceledException) { return; }
            try
            {
                var weg = await RaeumeAsync(store, horizont, ct);
                if (weg > 0) logger?.LogInformation("[Inbox] {Anzahl} Quittungen älter als {Horizont} entfernt", weg, horizont);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger?.LogWarning(ex, "[Inbox] Aufräumen fehlgeschlagen — der nächste Takt versucht es erneut");
            }
        }
    }

    /// <summary>Ein Durchlauf: Seiten löschen, bis keine alte Quittung mehr übrig ist oder ein anderer Knoten die Lease hält. Liefert die Zahl.</summary>
    public static async Task<int> RaeumeAsync(IDocumentStore store, TimeSpan horizont, CancellationToken ct)
    {
        await InboxTabelle.SicherAsync(store, ct);
        var tabelle = InboxTabelle.Name(store);
        var summe = 0;
        while (true)
        {
            await using var conn = store.Storage.Database.CreateConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            // Eine Anweisung = eine Transaktion: Lease nehmen (bis zum Ende der Anweisung), dann eine Seite löschen — ohne Lease nichts.
            cmd.CommandText = $"""
                with lease as (select pg_try_advisory_xact_lock(@schluessel) as frei)
                delete from {tabelle} where command_id in (
                  select i.command_id from {tabelle} i, lease where lease.frei and i.zeit < now() - @horizont limit @seite)
                """;
            cmd.Parameters.AddWithValue("schluessel", LeaseSchluessel);
            cmd.Parameters.AddWithValue("horizont", horizont);
            cmd.Parameters.AddWithValue("seite", Seite);
            var weg = await cmd.ExecuteNonQueryAsync(ct);
            summe += weg;
            if (weg < Seite) return summe;
        }
    }
}
