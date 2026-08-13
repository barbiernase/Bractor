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
//  Emittiert: knowledge-graph.json  +  knowledge-graph.html (interaktive Präsentation).
// ════════════════════════════════════════════════════════════════════════════

// Infrastructure ist dabei, damit GeneratedCommandRouting (das Generat) sichtbar ist.
var targetProjects = new[]
{
    "Abstractions", "Core", "Domain", "Domain.Projections", "Domain.Pipeline", "Infrastructure",
};

MSBuildLocator.RegisterDefaults();

Console.WriteLine("\n╔══════════════════════════════════════════════╗");
Console.WriteLine("║        Wissensgraph-Extractor (Neubau)        ║");
Console.WriteLine("╚══════════════════════════════════════════════╝\n");

var solutionPath = args.Length > 0 ? args[0] : FindSolution();
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

var compilations = new List<Compilation>();
foreach (var name in targetProjects)
{
    var project = solution.Projects.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (project == null) { Console.WriteLine($"   ⚠️  {name} nicht gefunden"); continue; }
    var comp = await project.GetCompilationAsync();
    if (comp != null) { compilations.Add(comp); Console.WriteLine($"   ✓ {name}"); }
}
if (compilations.Count == 0) { Console.Error.WriteLine("❌ Keine Compilations."); return 1; }

Console.WriteLine("\n── Routing-Wahrheit ──");
var routing = RoutingTruth.FromCompilations(compilations);
Console.WriteLine($"   Quelle: {routing.Source}  ({routing.CommandToAggregate.Count} Command→Aggregat, {routing.CommandToEvents.Count} Command→Events)");

Console.WriteLine("\n── Domänen-Extraktion ──");
var dom = new DomainExtractor(compilations).Extract();
Console.WriteLine($"   {dom.Aggregates.Count} Aggregate, {dom.Processes.Count} Sagas, {dom.Projections.Count} Projektionen, {dom.Queries.Count} Queries, {dom.Pipelines.Count} Pipelines");
Console.WriteLine($"   Prozesse registriert: {string.Join(", ", dom.RegisteredProcesses)}");

Console.WriteLine("\n── Graph aufbauen ──");
var graph = new GraphBuilder(routing, dom).Build();
foreach (var (k, v) in graph.Meta.Counts) Console.WriteLine($"   {k,-12}: {v}");

var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var json = JsonSerializer.Serialize(graph, jsonOptions);

var jsonPath = Path.Combine(solutionDir, "knowledge-graph.json");
await File.WriteAllTextAsync(jsonPath, json);
Console.WriteLine($"\n✅ {jsonPath}");

// Round-trip: denselben Graph als editierbares Domänen-Modell zurückschreiben (Umkehrung
// C# → Board). Der Editor lädt dieses Modell und baut Vorhandenes weiter.
var modell = ModellMapper.ZuEditorModell(graph);
var modellJson = modell.AlsJson();
var modellPath = Path.Combine(solutionDir, "domain-model.json");
await File.WriteAllTextAsync(modellPath, modellJson);
Console.WriteLine($"✅ {modellPath}  ({modell.Aggregate.Count} Aggregate, {modell.Sagas.Count} Sagas)");

var htmlPath = Path.Combine(solutionDir, "knowledge-graph.html");
await File.WriteAllTextAsync(htmlPath, HtmlPresenter.Render(graph, json, modellJson));
Console.WriteLine($"✅ {htmlPath}");

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
