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
//    • Sagas/Prozesse aus dem Prozess-DSL (Bedingung/Sende/RückgängigDurch/Fan-out).
//    • Projektionen, Queries, Pipelines.
//  Emittiert: knowledge-graph.json, domain-model.json (Editor-Modell) + editor.html (die Oberfläche).
// ════════════════════════════════════════════════════════════════════════════

MSBuildLocator.RegisterDefaults();

Console.WriteLine("\n╔══════════════════════════════════════════════╗");
Console.WriteLine("║        Wissensgraph-Extractor (Neubau)        ║");
Console.WriteLine("╚══════════════════════════════════════════════╝\n");

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
var solution = await workspace.OpenSolutionAsync(solutionPath);

// Welche Projekte analysiert werden, ist ABGELEITET (Vertrag → Laufzeit mit generierter Routing-Tabelle → deren
// Referenz-Hülle; Domäne/Framework über die Aggregate; Hosts über ihren handgeschriebenen Einstiegspunkt).
Console.WriteLine("\n── Projektlage (abgeleitet) ──");
var lage = await Projektlage.ErmittleAsync(solution, Console.WriteLine);
var compilations = lage.Compilations;

if (sonde)
{
    Console.WriteLine("\n── Agnostik-Sonde ──");
    // --sonde-board <datei>: das Board-Modell inkl. Sonde ausgeben (um die Sonde im Editor anzusehen).
    var bi = Array.IndexOf(args, "--sonde-board");
    return await Sonde.PruefeAsync(solution, lage, Path.GetDirectoryName(solutionPath) ?? ".", args.Contains("--sonde-ist"),
        bi >= 0 && bi + 1 < args.Length ? args[bi + 1] : null);
}

Console.WriteLine("\n── Routing-Wahrheit ──");
var routing = RoutingTruth.FromCompilations(compilations);
Console.WriteLine($"   Quelle: {routing.Source}  ({routing.CommandToAggregate.Count} Command→Aggregat, {routing.CommandToEvents.Count} Command→Events)");

Console.WriteLine("\n── Domänen-Extraktion ──");
var dom = new DomainExtractor(compilations, lage.DomänenAssemblies).Extract();
dom.Wurzel = Path.GetDirectoryName(solutionPath) ?? ".";
foreach (var (ns, dir) in lage.ProjektWurzeln()) dom.ProjektWurzeln[ns] = dir;
Console.WriteLine($"   {dom.Aggregates.Count} Aggregate, {dom.Processes.Count} Sagas, {dom.Projections.Count} Projektionen, {dom.Queries.Count} Queries, {dom.Pipelines.Count} Pipelines");
Console.WriteLine($"   Prozesse registriert: {string.Join(", ", dom.RegisteredProcesses)}");

Console.WriteLine("\n── Graph aufbauen ──");
var graph = new GraphBuilder(routing, dom).Build();
foreach (var (k, v) in graph.Meta.Counts) Console.WriteLine($"   {k,-12}: {v}");

var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";

Console.WriteLine("\n── Composition-Root (Betrieb/Host) ──");
var compositionRoot = await new CompositionRootExtractor(solution, lage, routing, dom).ExtractAsync();
Console.WriteLine($"   {compositionRoot.Frists.Count} Frist(en), {compositionRoot.Triggers.Count} Trigger, {compositionRoot.Dienste.Count} Dienst-Bindung(en), {compositionRoot.HostSettings.Count} HostSetting(s)");
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
    var boardJson = ModellMapper.ZuBoardJson(graph, dom, compositionRoot);
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
// Query/Pipeline/Trigger). Das Board lädt dies als Seed und baut es weiter (autoLayout platziert).
var modellJson = ModellMapper.ZuBoardJson(graph, dom, compositionRoot);
var modellPath = Path.Combine(solutionDir, "domain-model.json");
await File.WriteAllTextAsync(modellPath, modellJson);
Console.WriteLine($"✅ {modellPath}  ({modell.Aggregate.Count} Aggregate, {modell.Sagas.Count} Sagas)");

// Die EINE Oberfläche (Route /editor): Editor + Simulation. Das Modell lädt sie live vom SimHost.
var editorPath = Path.Combine(solutionDir, "editor.html");
await File.WriteAllTextAsync(editorPath, HtmlPresenter.EditorPage());
Console.WriteLine($"✅ {editorPath}");

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
