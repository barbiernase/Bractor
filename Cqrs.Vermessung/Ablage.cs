using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Der Ergebnis-Ordner eines Laufs (docs/12 §12.5): <c>vermessung/ergebnisse/&lt;JJJJ-MM-TT_HHMM&gt;_&lt;commit&gt;_&lt;anlass&gt;/</c>
/// mit <c>ergebnis.json</c>, <c>latenzen/&lt;szenario&gt;.csv[.gz]</c>, <c>pg_stat_statements.csv</c>, <c>host.csv</c>, <c>zusammenfassung.md</c>.
/// Nur anhängen: existiert der Ordner schon, bricht der Lauf ab. Host-Logs landen im Scratch-Verzeichnis (nicht im Repo), nur ihr Ende im Ordner.
/// </summary>
internal sealed class Ablage
{
    public string Ordner { get; }
    public string ScratchLogs { get; }
    public JsonObject Ergebnis { get; } = new();
    private readonly Dictionary<string, StreamWriter> _latenzen = new();
    private readonly StreamWriter _statements;

    /// <param name="probe">Funktionstest des Treibers (<c>--probe</c>) oder Diagnose (<c>--art leerlauf</c>): kein Messlauf — der Ordner liegt
    /// im Temp-Verzeichnis, nicht im Repo.</param>
    public Ablage(string wurzel, string anlass, string commit, bool probe)
    {
        var name = $"{DateTime.Now:yyyy-MM-dd_HHmm}_{commit}_{Bereinige(anlass)}";
        Ordner = probe ? Path.Combine(Path.GetTempPath(), "cqrs-vermessung-probe", name) : Path.Combine(wurzel, "vermessung", "ergebnisse", name);
        if (Directory.Exists(Ordner)) throw new InvalidOperationException($"Ergebnis-Ordner existiert schon (nur anhängen): {Ordner}");
        Directory.CreateDirectory(Path.Combine(Ordner, "latenzen"));
        ScratchLogs = Path.Combine(Path.GetTempPath(), "cqrs-vermessung-logs", name);
        Directory.CreateDirectory(ScratchLogs);
        _statements = new StreamWriter(Path.Combine(Ordner, "pg_stat_statements.csv"), false, Encoding.UTF8) { AutoFlush = true };
        _statements.WriteLine("szenario,wiederholung,aufrufe,zeit_ms,zeilen,query");
    }

    private static string Bereinige(string s) => System.Text.RegularExpressions.Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9-]+", "-").Trim('-');

    public string HostCsv => Path.Combine(Ordner, "host.csv");

    public void Latenz(string szenario, int wiederholung, Vorgang v, string? zusatz = null)
    {
        if (!_latenzen.TryGetValue(szenario, out var w))
        {
            w = new StreamWriter(Path.Combine(Ordner, "latenzen", szenario + ".csv"), false, Encoding.UTF8);
            w.WriteLine("wiederholung,phase,vorgang,start_utc,latenz_ms,fehler" + (zusatz != null ? ",zusatz" : ""));
            _latenzen[szenario] = w;
        }
        lock (w)
            w.WriteLine(string.Join(',', wiederholung, v.Phase, v.Schluessel,
                v.Start.ToString("O"), double.IsNaN(v.LatenzMs) ? "" : v.LatenzMs.ToString("F1", CultureInfo.InvariantCulture),
                Csv(v.Fehler)) + (zusatz != null ? "," + Csv(zusatz) : ""));
    }

    public void Statements(string szenario, int wiederholung, IEnumerable<Datenbank.Statement> st)
    {
        foreach (var s in st.Take(40))
            _statements.WriteLine(string.Join(',', szenario, wiederholung, s.Aufrufe, s.ZeitMs.ToString("F1", CultureInfo.InvariantCulture), s.Zeilen,
                Csv(s.Text.Replace('\n', ' '))));
    }

    private static string Csv(string? s) => s == null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

    /// <summary>Schreibt ergebnis.json und zusammenfassung.md; komprimiert Latenz-Dateien über 1 MB.</summary>
    public void Schliesse(string zusammenfassung)
    {
        foreach (var w in _latenzen.Values) w.Dispose();
        _statements.Dispose();
        foreach (var datei in Directory.GetFiles(Path.Combine(Ordner, "latenzen"), "*.csv"))
            if (new FileInfo(datei).Length > 1_000_000)
            {
                using (var ein = File.OpenRead(datei))
                using (var aus = new GZipStream(File.Create(datei + ".gz"), CompressionLevel.SmallestSize))
                    ein.CopyTo(aus);
                File.Delete(datei);
            }
        File.WriteAllText(Path.Combine(Ordner, "ergebnis.json"), Ergebnis.ToJsonString(new JsonSerializerOptions { WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        File.WriteAllText(Path.Combine(Ordner, "zusammenfassung.md"), zusammenfassung);
    }

    /// <summary>Hängt das Ende eines Host-Logs an (nur bei Fehlern — die vollen Logs bleiben im Scratch-Verzeichnis).</summary>
    public void LogEnde(string logDatei, int zeilen = 200)
    {
        if (!File.Exists(logDatei)) return;
        var ende = File.ReadLines(logDatei).TakeLast(zeilen);
        File.WriteAllLines(Path.Combine(Ordner, "log-ende-" + Path.GetFileName(logDatei)), ende);
    }

    public static JsonNode? Zahl(double d) => double.IsNaN(d) || double.IsInfinity(d) ? null : JsonValue.Create(Math.Round(d, 3));
}
