using System.Globalization;
using System.Text;

namespace Cqrs.Vermessung;

/// <summary>
/// Diagnose des Leerlaufs (PF-14, <c>--art leerlauf</c>): statt eines Fensters eine Zeitreihe in Scheiben — je Scheibe Transaktionen und
/// Queries je Sekunde, CPU des Hosts, die häufigsten Statements und wo die Konsumenten stehen (Kopf des Logs, Poll-Cursor, kleinste Position
/// je Projektion, zuletzt bewegte Emittenten-Cursor; gelesen über die Lese-Rolle, also nicht mitgezählt). Zeigt, ob Last ohne Eingang
/// abklingt (Nachlauf) oder bleibt (Grundlast).
/// </summary>
internal static class Zeitreihe
{
    private static readonly object Sperre = new();

    public static async Task LaufeAsync(Kontext k, Anwendungslauf lauf, string name, TimeSpan dauer, TimeSpan takt)
    {
        Console.WriteLine($"  Zeitreihe {name}: {dauer.TotalSeconds:F0} s in Scheiben zu {takt.TotalSeconds:F0} s");
        k.Ressourcen.Abschnitt(name);
        var reihe = Path.Combine(k.Ablage.Ordner, "zeitreihe.csv");
        var statements = Path.Combine(k.Ablage.Ordner, "zeitreihe_statements.csv");
        lock (Sperre)
        {
            if (!File.Exists(reihe))
                File.WriteAllText(reihe, "reihe,t_s,tx_je_s,queries_je_s,cpu_prozent,rss_mb,kopf_seq,poll_cursor,projektion_min_position,emittenten_bewegt\n", Encoding.UTF8);
            if (!File.Exists(statements)) File.WriteAllText(statements, "reihe,t_s,aufrufe,zeit_ms,zeilen,query\n", Encoding.UTF8);
        }
        var start = DateTimeOffset.UtcNow;
        var a = await lauf.Db.PgStandAsync();
        var leserA = lauf.Db.LeserStatements;
        while (DateTimeOffset.UtcNow - start < dauer)
        {
            await Task.Delay(takt);
            var b = await lauf.Db.PgStandAsync();
            var (txs, qs, _, _, _, st) = Datenbank.Differenz(a, b, lauf.Db.LeserStatements - leserA);
            var (cpu, rss) = k.Ressourcen.Fenster(lauf.Rolle, a.Zeit, b.Zeit);
            leserA = lauf.Db.LeserStatements;
            var stand = await StandAsync(lauf.Db);
            var t = (b.Zeit - start).TotalSeconds;
            var top = st.Where(s => !IstSteuerung(s.Text)).Take(12).ToList();
            var zeile = string.Join(',', name, F(t), F(txs), F(qs), F(cpu), F(rss), stand.Kopf, stand.Poll, stand.Projektion, stand.Emittenten);
            lock (Sperre)
            {
                File.AppendAllText(reihe, zeile + "\n");
                File.AppendAllLines(statements, top.Select(s => string.Join(',', name, F(t), s.Aufrufe, F(s.ZeitMs), s.Zeilen,
                    "\"" + s.Text.Replace('\n', ' ').Replace("\"", "\"\"") + "\"")));
            }
            Console.WriteLine($"    t={t,5:F0} s · {txs,6:F1} Tx/s · {qs,7:F1} Q/s · CPU {cpu,5:F1} % · Kopf {stand.Kopf} Poll {stand.Poll} " +
                $"Proj.min {stand.Projektion} Emit.bewegt {stand.Emittenten} · " +
                string.Join(" | ", top.Take(4).Select(s => $"{s.Aufrufe}× {Kurz(s.Text)}")));
            a = b;
        }
    }

    private sealed record Stand(long Kopf, string Poll, long Projektion, long Emittenten);

    private static async Task<Stand> StandAsync(Datenbank db)
    {
        var s = db.Schema;
        async Task<long> Zahl(string sql) { try { return await db.ZahlAsync(sql); } catch { return -1; } }
        var kopf = await Zahl($"select coalesce(max(seq_id), 0) from {s}.mt_events");
        string poll;
        try
        {
            poll = string.Join('/', await db.LiesAsync($"select id, (data->>'HighWater')::bigint from {s}.mt_doc_pollcursor order by id",
                r => $"{r.GetString(0)}={r.GetInt64(1)}"));
        }
        catch { poll = "-"; }
        var projektion = await Zahl($"select min((data->>'Position')::bigint) from {s}.mt_doc_partitionscheckpoint");
        var emittenten = await Zahl($"select count(*) from {s}.mt_doc_emittentencursordoc where mt_last_modified > now() - interval '10 seconds'");
        return new Stand(kopf, poll.Length == 0 ? "-" : poll, projektion, emittenten);
    }

    private static bool IstSteuerung(string text)
    {
        var t = text.TrimStart().ToUpperInvariant();
        return t.StartsWith("BEGIN") || t.StartsWith("COMMIT") || t.StartsWith("ROLLBACK") || t.StartsWith("DISCARD") || t.StartsWith("SET ") || t.StartsWith("SHOW ");
    }

    private static string Kurz(string sql)
    {
        var t = sql.Replace('\n', ' ');
        var i = t.IndexOf(" from ", StringComparison.OrdinalIgnoreCase);
        var tabelle = i < 0 ? "" : new string(t[(i + 6)..].TakeWhile(c => c != ' ').ToArray());
        return t.StartsWith("select data, type", StringComparison.OrdinalIgnoreCase) ? $"events-read({(t.Contains("stream_id = $1") ? "stream" : "seite")})"
            : tabelle.Length > 0 ? $"{t[..Math.Min(12, t.Length)]}…{tabelle}" : t[..Math.Min(40, t.Length)];
    }

    private static string F(double d) => double.IsNaN(d) ? "" : d.ToString("F1", CultureInfo.InvariantCulture);
}
