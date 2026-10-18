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
/// der Datenbank (Spalte <c>zeit</c> = <c>now()</c> beim Commit der Quittung, Vergleich gegen ihre Uhr).
///
/// Die Kosten eines Durchlaufs hängen nur an dem, was er löscht, nie an der Größe der Tabelle (PF-27): er sucht über den B-Baum auf <c>zeit</c>,
/// die ältesten zuerst, und erst ab der Grenze, bis zu der er zuletzt gelöscht hat (abzüglich <see cref="Spielraum"/>) — die Einträge davor
/// sind gelöschte Zeilen, die bis zum nächsten VACUUM im Index stehen; ohne die Untergrenze ginge jeder Durchlauf erneut über alle. (Ein
/// BRIN-Index taugte nicht: nach dem ersten Aufräumen füllt Postgres die frei gewordenen Seiten mit neuen Quittungen, jeder Block-Bereich deckt
/// dann alt und neu ab, und die Suche nach alten las die ganze Tabelle.)
/// </summary>
public sealed class InboxAufraeumer(IDocumentStore store, TimeSpan horizont, ILogger<InboxAufraeumer>? logger = null) : BackgroundService
{
    private static readonly TimeSpan Takt = TimeSpan.FromMinutes(10);
    private const int Seite = 10_000;

    /// <summary>
    /// Um so viel unter der letzten Grenze beginnt der nächste Durchlauf: eine Quittung trägt die Zeit ihrer Transaktion, eine, die beim letzten
    /// Durchlauf noch nicht committet war, kann knapp unter dessen Grenze liegen.
    /// </summary>
    public static readonly TimeSpan Spielraum = TimeSpan.FromMinutes(5);

    /// <summary>Schlüssel des Advisory-Locks (fest, je Datenbank; die Schemata teilen ihn — sie räumen dann nacheinander).</summary>
    private const long LeaseSchluessel = 0x4342_5153_494E_4258;   // "CBQSINBX"

    /// <summary>Bis hierher ist gelöscht (null = noch kein vollständiger Durchlauf dieses Knotens).</summary>
    private DateTimeOffset? _untergrenze;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(Takt, ct); }
            catch (OperationCanceledException) { return; }
            try
            {
                var weg = await RaeumeAsync(ct);
                if (weg > 0) logger?.LogInformation("[Inbox] {Anzahl} Quittungen älter als {Horizont} entfernt", weg, horizont);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger?.LogWarning(ex, "[Inbox] Aufräumen fehlgeschlagen — der nächste Takt versucht es erneut");
            }
        }
    }

    /// <summary>Ein Durchlauf mit einem frischen Aufräumer (ohne Untergrenze) — für Tests.</summary>
    public static Task<int> RaeumeAsync(IDocumentStore store, TimeSpan horizont, CancellationToken ct)
        => new InboxAufraeumer(store, horizont).RaeumeAsync(ct);

    /// <summary>
    /// Ein Durchlauf: Seiten löschen, bis keine alte Quittung mehr übrig ist oder ein anderer Knoten die Lease hält. Liefert die Zahl. Lief er
    /// mit Lease bis zum Ende, gilt seine Grenze (abzüglich <see cref="Spielraum"/>) als Untergrenze des nächsten.
    /// </summary>
    public async Task<int> RaeumeAsync(CancellationToken ct)
    {
        await InboxTabelle.SicherAsync(store, ct);
        var sql = Abfrage(InboxTabelle.Name(store));
        var summe = 0;
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        DateTimeOffset grenze;
        await using (var jetzt = new Npgsql.NpgsqlCommand("select now()", (Npgsql.NpgsqlConnection)conn))
        await using (var r = await jetzt.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            grenze = r.GetFieldValue<DateTimeOffset>(0) - horizont;
        }
        while (true)
        {
            await using var cmd = new Npgsql.NpgsqlCommand(sql, (Npgsql.NpgsqlConnection)conn);
            foreach (var w in Werte(grenze)) cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = w });
            bool frei;
            long weg;
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                await r.ReadAsync(ct);
                frei = r.GetBoolean(0);
                weg = r.GetInt64(1);
                // Die nächste Seite beginnt hinter dieser (die ältesten gingen zuerst) — sonst ginge jede Seite eines langen Durchlaufs wieder
                //   über die toten Einträge aller vorigen.
                if (!r.IsDBNull(2)) _untergrenze = r.GetFieldValue<DateTimeOffset>(2) - Spielraum;
            }
            summe += (int)weg;
            if (!frei) return summe;   // ein anderer Knoten räumt — die Untergrenze bleibt
            if (weg < Seite)
            {
                _untergrenze = grenze - Spielraum;
                return summe;
            }
        }
    }

    /// <summary>
    /// Eine Seite löschen — eine Anweisung = eine Transaktion: Lease nehmen ($1, bis zum Ende der Anweisung), dann höchstens $4 Quittungen mit
    /// Zeit in [$2, $3), die ältesten zuerst (über den Index), löschen; ohne Lease nichts. Liefert, ob die Lease frei war, wie viele weg sind
    /// und die jüngste gelöschte Zeit.
    /// Eine Quelle für den Aufräumer und den Plan-Test.
    /// </summary>
    public static string Abfrage(string tabelle) => $"""
        with lease as (select pg_try_advisory_xact_lock($1) as frei),
        weg as (delete from {tabelle} where command_id in (
          select i.command_id from {tabelle} i, lease where lease.frei and i.zeit >= coalesce($2, '-infinity'::timestamptz) and i.zeit < $3
          order by i.zeit limit $4) returning zeit)
        select (select frei from lease), (select count(*) from weg), (select max(zeit) from weg)
        """;

    /// <summary>Die Werte zu <see cref="Abfrage"/>: Lease, die Untergrenze dieses Aufräumers, die Grenze, die Seitengröße.</summary>
    public object[] Werte(DateTimeOffset grenze, int seite = Seite)
        => [LeaseSchluessel, _untergrenze is { } u ? u : DBNull.Value, grenze, seite];
}
