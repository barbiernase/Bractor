using System.Globalization;
using System.Text;

namespace Cqrs.Vermessung;

/// <summary>
/// Diagnose des Leerlaufs (PF-14, <c>--art leerlauf</c>): statt eines Fensters eine Zeitreihe in Scheiben — je Scheibe Transaktionen und
/// Queries je Sekunde, CPU des Hosts, die häufigsten Statements und wo die Konsumenten stehen (Kopf des Logs, kleinste Position
/// der Projektionen und der Funktionen, zuletzt bewegte Partitions-Stände; gelesen über die Lese-Rolle, also nicht mitgezählt). Zeigt, ob Last ohne Eingang
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
                File.WriteAllText(reihe, "reihe,t_s,tx_je_s,queries_je_s,cpu_prozent,rss_mb,kopf_seq,projektion_min_position,funktion_min_position,staende_bewegt\n", Encoding.UTF8);
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
            var stand = await Konsumentenstand.LiesAsync(lauf.Db);
            var t = (b.Zeit - start).TotalSeconds;
            var top = st.Where(s => !IstSteuerung(s.Text)).Take(12).ToList();
            var zeile = string.Join(',', name, F(t), F(txs), F(qs), F(cpu), F(rss), stand.Kopf, stand.Projektion, stand.Funktion, stand.Bewegt);
            lock (Sperre)
            {
                File.AppendAllText(reihe, zeile + "\n");
                File.AppendAllLines(statements, top.Select(s => string.Join(',', name, F(t), s.Aufrufe, F(s.ZeitMs), s.Zeilen,
                    "\"" + s.Text.Replace('\n', ' ').Replace("\"", "\"\"") + "\"")));
            }
            Console.WriteLine($"    t={t,5:F0} s · {txs,6:F1} Tx/s · {qs,7:F1} Q/s · CPU {cpu,5:F1} % · Kopf {stand.Kopf} " +
                $"Proj.min {stand.Projektion} Funk.min {stand.Funktion} bewegt {stand.Bewegt} · " +
                string.Join(" | ", top.Take(4).Select(s => $"{s.Aufrufe}× {Kurz(s.Text)}")));
            a = b;
        }
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
