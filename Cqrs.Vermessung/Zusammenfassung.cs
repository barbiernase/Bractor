using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Die Zeilen der fortlaufenden Tabelle (docs/12 §12.5): je Szenario die Mediane der Kennzahlen als <c>{szenario, kennzahl, einheit, wert}</c>
/// — <c>scripts/vermessung-tabelle.sh</c> setzt daraus die Spalten zusammen — und dieselbe Tabelle als <c>zusammenfassung.md</c>.
/// </summary>
internal static class Zusammenfassung
{
    private static readonly (string Key, string Kennzahl, string Einheit)[] Spalten =
    [
        ("angenommen_je_s", "angenommen", "{V}/s"),
        ("sichtbar_je_s", "sichtbar im Fenster", "{V}/s"),
        ("durchsatz_je_s", "Durchsatz bis alles sichtbar", "{V}/s"),
        ("rueckstand", "Rückstand am Fensterende", "Vorgänge"),
        ("abbau_s", "Abbauzeit", "s"),
        ("prozessende_nachlauf_s", "Prozess-Ende nach dem Abbau", "s"),
        ("latenz_p50_ms", "Latenz p50", "ms"),
        ("latenz_p99_ms", "Latenz p99", "ms"),
        ("dauer_aufnahme_s", "Dauer Range aufnehmen", "s"),
        ("dauer_einfrieren_s", "Dauer Einfrieren", "s"),
        ("reaktion_p50_ms", "Reaktion p50", "ms"),
        ("reaktion_p99_ms", "Reaktion p99", "ms"),
        ("tx_je_vorgang", "Transaktionen je Vorgang", "1"),
        ("queries_je_vorgang", "Queries je Vorgang", "1"),
        ("tx_je_s", "Transaktionen", "/s"),
        ("queries_je_s", "Queries", "/s"),
        ("cpu_prozent", "CPU Host", "% eines Kerns"),
        ("worker_cpu_prozent", "CPU Worker", "% eines Kerns"),
        ("rss_mb", "RSS Host (max.)", "MB"),
        ("rss_ende_mb", "RSS Host am Fensterende", "MB"),
        ("rss_lauf_max_mb", "RSS Host Höchststand des Laufs", "MB"),
        ("rss_ende_anteil", "RSS am Ende / Höchststand", "1"),
        ("aktive_actors", "Aktive Actors am Fensterende", "1"),
        ("aktive_actors_hoechst", "Aktive Actors, Höchststand der Fensterenden", "1"),
    ];

    public static JsonArray Zeilen(JsonObject szenarien)
    {
        var zeilen = new JsonArray();
        foreach (var (name, s) in szenarien)
        {
            var median = s!["median"]!.AsObject();
            foreach (var (key, kennzahl, einheit) in Spalten)
                if (median[key] is { } wert)
                    zeilen.Add(new JsonObject
                    {
                        ["szenario"] = $"{name} {Messung.Titel(name)}", ["kennzahl"] = kennzahl,
                        ["einheit"] = einheit.Replace("{V}", Vorgaenge(name)), ["wert"] = wert.DeepClone(),
                    });
        }
        return zeilen;
    }

    private static string Vorgaenge(string s) => s switch
    {
        "S1" or "S2" or "S4" => "Paare", _ when s.StartsWith("S3") => "Teile", _ when s.StartsWith("S5") => "Vorgänge", _ => "",
    };

    public static string Markdown(JsonObject e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Vermessung {e["anlass"]} ({e["art"]})");
        sb.AppendLine();
        sb.AppendLine($"Commit `{e["commit"]}` ({e["arbeitsbaum"]}), Branch `{e["branch"]}`, {e["zeit"]}  ");
        sb.AppendLine($"Rechner: {e["rechner"]}  ");
        sb.AppendLine($"Fremdlast vorher: {e["fremdlast_vorher"]}  ");
        sb.AppendLine($"Fremdlast nachher: {e["fremdlast_nachher"]}  ");
        sb.AppendLine($"Gültig: **{((bool?)e["gueltig"] == true ? "ja" : "nein")}**" +
                      (e["begruendung"] is JsonArray b && b.Count > 0 ? " — " + string.Join("; ", b.Select(x => x!.ToString())) : ""));
        sb.AppendLine($"Exactly-once: **{((bool?)e["exactly_once_ok"] == true ? "grün" : "ROT")}**");
        sb.AppendLine();
        if (e["zusammenfassung"] is JsonArray zeilen && zeilen.Count > 0)
        {
            sb.AppendLine("| Szenario | Kennzahl | Median |");
            sb.AppendLine("|---|---|---:|");
            foreach (var z in zeilen)
                sb.AppendLine($"| {z!["szenario"]} | {z["kennzahl"]} ({z["einheit"]}) | {Zahl(z["wert"])} |");
            sb.AppendLine();
        }
        if (e["exactly_once"] is JsonArray ex)   // Lang- und Störungsläufe
        {
            sb.AppendLine("| Prüfung | Soll | Ist | ok |");
            sb.AppendLine("|---|---:|---:|:-:|");
            foreach (var p in ex) sb.AppendLine($"| {p!["name"]} | {p["soll"]} | {p["ist"]} | {((bool?)p["ok"] == true ? "✓" : "✗")} |");
            sb.AppendLine();
        }
        if (e["szenarien"] is JsonObject sz)
            foreach (var (name, s) in sz)
            {
                var fehl = s!["exactly_once"]!["pruefungen"]!.AsArray().Where(p => (bool?)p!["ok"] != true).ToList();
                sb.AppendLine($"- {name}: Exactly-once {(fehl.Count == 0 ? "grün" : "ROT: " + string.Join("; ", fehl.Select(p => $"{p!["name"]} (soll {p["soll"]}, ist {p["ist"]})")))}");
            }
        return sb.ToString();
    }

    private static string Zahl(JsonNode? n)
    {
        if (n is null) return "–";
        var d = n.GetValue<double>();
        return d >= 100 ? Math.Round(d).ToString("0", CultureInfo.InvariantCulture) : d.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
