using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Die wenigen Kennzahlen, die ein Mensch liest (docs/12 §12.5), als zwei kleine Tabellen: <b>Last</b> (Szenarien mit Vorgängen) und
/// <b>Ohne Eingang</b> (Leerlauf, Neuaufbau) — je Zelle Median und Spanne über die Wiederholungen. Alle übrigen Kennzahlen bleiben in
/// <c>ergebnis.json</c>. Die Spalten stehen als <c>spalten</c> im Ergebnis; <c>scripts/vermessung-tabelle.sh</c> setzt daraus die Tabelle in
/// docs/12 zusammen (eine Quelle).
/// </summary>
internal static class Zusammenfassung
{
    public static readonly (string Tabelle, string Key, string Titel)[] Spalten =
    [
        ("last", "durchsatz_je_s", "Durchsatz (/s)"),
        ("last", "angenommen_je_s", "Schreiben angenommen (/s)"),
        ("last", "latenz_p50_ms", "Latenz p50 (ms)"),
        ("last", "latenz_p99_ms", "Latenz p99 (ms)"),
        ("last", "reaktion_p99_ms", "Reaktion p99 (ms)"),
        ("last", "tx_je_vorgang", "Transaktionen je Vorgang"),
        ("last", "queries_je_vorgang", "Queries je Vorgang"),
        ("last", "cpu_ms_je_vorgang", "CPU je Vorgang (ms)"),
        ("ruhe", "queries_je_s", "Queries (/s)"),
        ("ruhe", "cpu_prozent", "CPU (% eines Kerns)"),
        ("ruhe", "nachlauf_s", "Nachlauf (s)"),
        ("ruhe", "rss_ende_mb", "RSS am Ende (MB)"),
        ("ruhe", "events_je_s", "Neuaufbau (Events/s)"),
        ("ruhe", "dauer_s", "Neuaufbau (s)"),
        ("ruhe", "events", "Neuaufbau (Events)"),
    ];

    public static JsonArray SpaltenJson() => new(Spalten.Select(s => (JsonNode)new JsonObject
        { ["tabelle"] = s.Tabelle, ["key"] = s.Key, ["titel"] = s.Titel }).ToArray());

    public static string Markdown(JsonObject e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Vermessung {e["anlass"]} ({e["art"]})");
        sb.AppendLine();
        sb.AppendLine($"Commit `{e["commit"]}` ({e["arbeitsbaum"]}), {e["zeit"]}  ");
        sb.AppendLine($"Rechner: {e["rechner"]}  ");
        sb.AppendLine($"Fremdlast vorher: {e["fremdlast_vorher"]}  ");
        sb.AppendLine($"Fremdlast nachher: {e["fremdlast_nachher"]}  ");
        sb.AppendLine($"Gültig: **{((bool?)e["gueltig"] == true ? "ja" : "nein")}**" +
                      (e["begruendung"] is JsonArray b && b.Count > 0 ? " — " + string.Join("; ", b.Select(x => x!.ToString())) : ""));
        sb.AppendLine($"Exactly-once: **{((bool?)e["exactly_once_ok"] == true ? "grün" : "ROT")}**");
        sb.AppendLine();
        if (e["szenarien"] is JsonObject sz)
        {
            foreach (var tabelle in new[] { "last", "ruhe" })
            {
                var spalten = Spalten.Where(s => s.Tabelle == tabelle).ToList();
                var zeilen = sz.Where(kv => (string?)kv.Value!["tabelle"] == tabelle).ToList();
                if (zeilen.Count == 0) continue;
                sb.AppendLine("| Szenario | " + string.Join(" | ", spalten.Select(s => s.Titel)) + " |");
                sb.AppendLine("|---|" + string.Concat(spalten.Select(_ => "---:|")));
                foreach (var (_, s) in zeilen)
                    sb.AppendLine($"| {s!["titel"]} | " + string.Join(" | ", spalten.Select(sp => Zelle(s["median"]![sp.Key], s["min"]![sp.Key], s["max"]![sp.Key]))) + " |");
                sb.AppendLine();
            }
            foreach (var (_, s) in sz)
            {
                var fehl = s!["exactly_once"]!["pruefungen"]!.AsArray().Where(p => (bool?)p!["ok"] != true).ToList();
                sb.AppendLine($"- {s["titel"]}: Exactly-once {(fehl.Count == 0 ? "grün" : "ROT: " + string.Join("; ", fehl.Select(p => $"{p!["name"]} (soll {p["soll"]}, ist {p["ist"]})")))}");
            }
        }
        if (e["exactly_once"] is JsonArray ex)   // Störungslauf
        {
            if (e["kennzahlen"] is JsonObject kz)
                sb.AppendLine(string.Join(" · ", kz.Select(kv => $"{kv.Key} {Zahl(kv.Value)}")) + "\n");
            sb.AppendLine("| Prüfung | Soll | Ist | ok |");
            sb.AppendLine("|---|---:|---:|:-:|");
            foreach (var p in ex) sb.AppendLine($"| {p!["name"]} | {p["soll"]} | {p["ist"]} | {((bool?)p["ok"] == true ? "✓" : "✗")} |");
        }
        return sb.ToString();
    }

    /// <summary>Median, dahinter die Spanne, wenn sie mehr als 5 % des Medians ausmacht.</summary>
    private static string Zelle(JsonNode? median, JsonNode? min, JsonNode? max)
    {
        if (median is null) return "–";
        if (min is null || max is null) return Zahl(median);
        double m = median.GetValue<double>(), lo = min.GetValue<double>(), hi = max.GetValue<double>();
        return hi - lo > 0.05 * Math.Abs(m) ? $"{Zahl(median)} ({Zahl(min)}–{Zahl(max)})" : Zahl(median);
    }

    private static string Zahl(JsonNode? n)
    {
        if (n is null) return "–";
        var d = n.GetValue<double>();
        return d >= 100 ? Math.Round(d).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ')
            : d.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
