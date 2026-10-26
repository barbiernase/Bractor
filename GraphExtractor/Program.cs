using System.Text.Json;
using System.Text.Json.Serialization;
using GraphExtractor;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

// ════════════════════════════════════════════════════════════════════════════
//  Wissensgraph-Extractor (Neubau)
//
//  Baut aus der aktuellen Solution einen typisierten, kausalen Property-Graph:
//    • Kern-Kausalität (command→aggregat→event) aus dem AUTORITATIVEN
//      GeneratedCommandRouting (aus der gebauten Infrastructure-Compilation).
//    • Funktionen (je Auf Eingang → Ausgaben), Akteure und Clients.
//    • Projektionen, Queries, Reader, Stores.
//  Emittiert: knowledge-graph.json, domain-model.json (Editor-Modell). Die Oberfläche liegt statisch in Werkbank/wwwroot/editor.
// ════════════════════════════════════════════════════════════════════════════

MSBuildLocator.RegisterDefaults();

Console.WriteLine("\n╔══════════════════════════════════════════════╗");
Console.WriteLine("║        Wissensgraph-Extractor (Neubau)        ║");
Console.WriteLine("╚══════════════════════════════════════════════╝\n");

// --grammatik: die Grammatik der Kompositions-Sprache als Liste „Regel → Build-Gegenstück" (braucht keine Solution).
if (args.Contains("--grammatik"))
{
    foreach (var r in DomainEditor.Grammatik.Regeln)
        Console.WriteLine($"{r.Id,-28} {r.Schwere,-8} {r.Name}\n{"",37}Build: {string.Join(" · ", r.Build.Select(g => (g.Kennung ?? g.Art) + " — " + g.Ort))}");
    Console.WriteLine("\nKonsum (Sorte → Eingang, Kardinalität je Nachricht):");
    foreach (var k in DomainEditor.Grammatik.Konsume) Console.WriteLine($"  {k.Sorte,-11} → {k.Baustein,-11} {k.Kardinalitaet,-9} {k.Regel}");
    Console.WriteLine("Erzeugung (Baustein → Sorte):");
    foreach (var e in DomainEditor.Grammatik.Erzeugungen) Console.WriteLine($"  {e.Baustein,-11} → {e.Sorte,-11} {e.Regel}");
    return 0;
}

// --check: reine Paritäts-Prüfung (schreibt NICHTS), Exit-Code ≠ 0 bei Abweichung — das CI-/Vorab-Gate.
var check = args.Contains("--check");
// --sonde: die Agnostik-Sonde (unbekannte Domäne im Speicher) gegen ihr handgeschriebenes Soll; --sonde-ist zeigt das Ist.
var sonde = args.Contains("--sonde") || args.Contains("--sonde-ist") || args.Contains("--sonde-board");
var solutionPath = args.FirstOrDefault(a => a.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)) ?? FindSolution();
if (solutionPath == null) { Console.Error.WriteLine("❌ Keine .sln gefunden."); return 1; }
Console.WriteLine($"📁 {solutionPath}");

using var workspace = MSBuildWorkspace.Create();
workspace.WorkspaceFailed += (_, e) =>
{
    if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
        Console.WriteLine($"⚠️  {e.Diagnostic.Message}");
};

Console.WriteLine("Lade Solution…");
var uhr = System.Diagnostics.Stopwatch.StartNew();
var solution = await workspace.OpenSolutionAsync(solutionPath);
var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";
var ladeZeit = uhr.Elapsed;

// --warm: Messung des warmen Workspaces (Werkbank P7.1, E7) — kalt lesen, dann nur aus den Domänen-Compilations, dann nach einer
//   Text-Änderung. Schreibt nichts.
if (args.Contains("--warm"))
    return await WarmMessung.LaufAsync(solution, solutionDir, ladeZeit);

// Welche Projekte analysiert werden, ist ABGELEITET (Vertrag → Laufzeit mit generierter Routing-Tabelle → deren
// Referenz-Hülle; Domäne/Framework über die Aggregate; Hosts über ihren handgeschriebenen Einstiegspunkt).
Console.WriteLine("\n── Projektlage (abgeleitet) ──");
uhr.Restart();
var lage = await Projektlage.ErmittleAsync(solution, Console.WriteLine);
var compilations = lage.Compilations;

if (sonde)
{
    Console.WriteLine("\n── Agnostik-Sonde ──");
    // --sonde-board <datei>: das Board-Modell inkl. Sonde ausgeben (um die Sonde im Editor anzusehen).
    var bi = Array.IndexOf(args, "--sonde-board");
    return await Sonde.PruefeAsync(solution, lage, solutionDir, args.Contains("--sonde-ist"),
        bi >= 0 && bi + 1 < args.Length ? args[bi + 1] : null);
}

var lesung = await Lesen.LiesAsync(solution, lage, solutionDir, uebersetzen: uhr.Elapsed);
var (routing, dom, graph, compositionRoot) = (lesung.Routing, lesung.Dom, lesung.Graph, lesung.CompositionRoot);
Console.WriteLine("\n── Routing-Wahrheit ──");
Console.WriteLine($"   Quelle: {routing.Source}  ({routing.CommandToAggregate.Count} Command→Aggregat, {routing.CommandToEvents.Count} Command→Events)");
Console.WriteLine("\n── Domänen-Extraktion ──");
Console.WriteLine($"   {dom.Aggregates.Count} Aggregate, {dom.BausteinFunktionen.Count} Funktionen, {dom.Projections.Count} Projektionen, {dom.Queries.Count} Queries, {dom.Readers.Count} Reader");
Console.WriteLine("\n── Graph ──");
foreach (var (k, v) in graph.Meta.Counts) Console.WriteLine($"   {k,-12}: {v}");
Console.WriteLine($"\n   Laden {ladeZeit.TotalSeconds:0.0} s · Übersetzen {lesung.Uebersetzen.TotalSeconds:0.0} s · Lesen {lesung.Extrahieren.TotalSeconds:0.0} s");
foreach (var b in lesung.Befunde)
    Console.WriteLine($"   ⚠ {b["code"]} {b["karte"]}: {b["text"]} ({b["datei"]}:{b["zeile"]})");

// --slots: Isolation aller Code-Block-Stellen (Spielraum vs. Nachbarn vs. Wissen von außen).
if (args.Contains("--slots"))
{
    Console.WriteLine();
    Console.WriteLine(new SlotInventar(lage, dom).Bericht());
    return 0;
}


// --kontext <Disc|Besitzer.Disc> [--auftrag "…"]: ein LLM-Kontext auf die Konsole (schreibt sonst nichts).
if (args.Contains("--kontext"))
    return KontextCli.Lauf(args, lage, dom, graph, compositionRoot, solutionDir);
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var json = JsonSerializer.Serialize(graph, jsonOptions);

if (check)
{
    Console.WriteLine("\n── Paritäts-Prüfung (Code ⇄ Extraktion ⇄ Editor-Modell) ──");
    var boardJson = lesung.BoardJson;
    var befunde = await ParitaetsPruefung.PruefeAsync(solution, lage,
        new ParitaetsPruefung.Analyse(routing, dom, graph, compilations), boardJson);
    foreach (var g in befunde.GroupBy(b => b.Bereich))
    {
        Console.WriteLine($"\n   [{g.Key}] {g.Count(b => b.Schweregrad != "info")} Befund(e)");
        foreach (var b in g) Console.WriteLine($"     {(b.Schweregrad switch { "error" => "✗", "warning" => "⚠", _ => "·" })} {b.Meldung}");
    }
    foreach (var f in graph.Views.Diagnostics.Where(f => f.Severity != "info"))
        Console.WriteLine($"   [Graph-Diagnose {f.Severity}] {f.Code}: {f.Message}");
    var fehler = befunde.Count(b => b.Schweregrad == "error");
    Console.WriteLine(fehler == 0
        ? "\n✅ Parität: Code, Extraktion und Editor-Modell stimmen überein (Fixpunkt + Inventar)."
        : $"\n❌ Parität verletzt: {fehler} Fehler.");
    return fehler == 0 ? 0 : 2;
}

var jsonPath = Path.Combine(solutionDir, "knowledge-graph.json");
await File.WriteAllTextAsync(jsonPath, json);
Console.WriteLine($"\n✅ {jsonPath}");

// Round-trip: denselben Graph als editierbares Domänen-Modell zurückschreiben (Umkehrung
// C# → Board). Der Editor lädt dieses Modell und baut Vorhandenes weiter.
var modell = ModellMapper.ZuEditorModell(graph, dom);
// Volles Board-Modell: Schreibseite + rekonstruierte Leseseiten-Topologie (Projektion/Reader/
// Query/Store). Das Board lädt dies als Seed und baut es weiter (autoLayout platziert).
var modellJson = lesung.BoardJson;
var modellPath = Path.Combine(solutionDir, "domain-model.json");
await File.WriteAllTextAsync(modellPath, modellJson);
Console.WriteLine($"✅ {modellPath}  ({modell.Aggregate.Count} Aggregate, {modell.BausteinFunktionen.Count} Funktionen)");

// --kontexte <verz>: zusätzlich zu den Modell-Dateien alle LLM-Kontexte + index.json (Quelle der LLM-Konsole der Werkbank).
if (args.Contains("--kontexte"))
    return KontextCli.Lauf(args, lage, dom, graph, compositionRoot, solutionDir);

Console.WriteLine($"\n   Diagnosen: {graph.Views.Diagnostics.Count}");
foreach (var f in graph.Views.Diagnostics.Take(12))
    Console.WriteLine($"     [{f.Severity}] {f.Message}");

return 0;

static string? FindSolution()
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10 && dir != null; i++)
    {
        var sln = Directory.GetFiles(dir, "*.sln");
        if (sln.Length > 0) return sln[0];
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}
