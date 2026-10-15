// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// E2E-MESSUNG (docs/plan-garantien-und-durchsatz.md §0.1, §0.2) — der echte Host, die realen Anwendungen, nur echte Eingänge.
//   scripts/vermessung-e2e.sh --anlass baseline --spalte Baseline        (Höchstlast S1–S5, wenige Minuten)
//   scripts/vermessung-e2e.sh --art stoerung --anlass baseline-stoerung   (Störungslauf, etwa drei Minuten)
// Jeder Lauf legt seinen Ordner unter vermessung/ergebnisse/ an (nur anhängen) und prüft Exactly-once; schlägt eine Prüfung fehl, ist
// der Lauf ungültig.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

using System.Text.Json.Nodes;
using Cqrs.Vermessung;

var opt = Optionen.Lies(args);
var wurzel = Werkzeuge.RepoWurzel();
var commit = Werkzeuge.Git(wurzel, "rev-parse --short HEAD");
var geaendert = Werkzeuge.Git(wurzel, "status --porcelain").Split('\n', StringSplitOptions.RemoveEmptyEntries)
    .Count(z => !z.Contains("vermessung/ergebnisse/"));
var ablage = new Ablage(wurzel, opt.Anlass, commit + (geaendert > 0 ? "-geaendert" : ""), opt.Probe);
Console.WriteLine($"Vermessung · {opt.Art} · Anlass {opt.Anlass} · Commit {commit}{(geaendert > 0 ? $" (+{geaendert} geänderte Dateien)" : "")}");
Console.WriteLine($"  Ergebnis: {ablage.Ordner}");
Console.WriteLine($"  Host-Logs: {ablage.ScratchLogs}");

var e = ablage.Ergebnis;
e["art"] = opt.Art == "messung" ? "e2e" : opt.Art;
e["anlass"] = opt.Anlass;
e["spalte"] = opt.Spalte;
e["skript"] = "scripts/vermessung-e2e.sh " + string.Join(' ', args);
e["zeit"] = DateTimeOffset.UtcNow.ToString("O");
e["commit"] = commit;
e["branch"] = Werkzeuge.Git(wurzel, "rev-parse --abbrev-ref HEAD");
e["arbeitsbaum"] = geaendert == 0 ? "sauber" : $"{geaendert} geänderte Dateien (nicht committet)";
e["rechner"] = Werkzeuge.Rechner();
e["umgebungen"] = new JsonArray("vermessung", "vermessung-pruefdomaene");
e["parameter"] = opt.Json();
e["fremdlast_vorher"] = Werkzeuge.Fremdlast();

var beispiel = Umgebungsdatei.Lies(wurzel, "vermessung");
var pruef = Umgebungsdatei.Lies(wurzel, "vermessung-pruefdomaene");
var res = new Ressourcen(ablage.HostCsv);
var k = new Kontext { Wurzel = wurzel, Ablage = ablage, Ressourcen = res, Optionen = opt, Beispiel = beispiel, Pruef = pruef };
await new Datenbank(beispiel.Verbindung, beispiel.Datenbank, beispiel.Schema).VorbereitenAsync();

var fehler = new List<string>();
JsonObject? laufErgebnis = null;
try
{
    laufErgebnis = opt.Art switch
    {
        "messung" => await Messung.LaufeAsync(k),
        "stoerung" => await Stoerungslauf.LaufeAsync(k),
        _ => throw new ArgumentException($"--art {opt.Art} unbekannt (messung, stoerung)"),
    };
}
catch (Exception ex)
{
    fehler.Add($"{ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"FEHLER: {ex}");
    foreach (var log in Directory.GetFiles(ablage.ScratchLogs, "*.log")) ablage.LogEnde(log);
}
finally { await res.DisposeAsync(); }

e["fremdlast_nachher"] = Werkzeuge.Fremdlast();
if (laufErgebnis != null) foreach (var (key, wert) in laufErgebnis.ToList()) { laufErgebnis.Remove(key); e[key] = wert; }
var exactlyOnceOk = e["exactly_once_ok"]?.GetValue<bool>() ?? false;
if (!exactlyOnceOk && fehler.Count == 0) fehler.Add("Exactly-once-Prüfung fehlgeschlagen");
if (opt.Probe) fehler.Add("Probelauf (--probe): verkürzte Parameter, nicht vergleichbar");
e["gueltig"] = fehler.Count == 0;
e["begruendung"] = new JsonArray(fehler.Select(f => (JsonNode)f).ToArray());
var zusammenfassung = Zusammenfassung.Markdown(e);
ablage.Schliesse(zusammenfassung);
Console.WriteLine();
Console.WriteLine(zusammenfassung);
Console.WriteLine($"Gültig: {(fehler.Count == 0 ? "ja" : "NEIN — " + string.Join("; ", fehler))}");
return fehler.Count == 0 ? 0 : 1;
