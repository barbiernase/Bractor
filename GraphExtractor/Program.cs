using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;


// ============================================================================
// KONFIGURATION
// ============================================================================

// Projekte die analysiert werden sollen.
// Alle Projekte in denen Domain-Code, Projektionen, Pipelines oder Client-Code liegt.
var targetProjects = new[]
{
    "Abstractions",
    "Core",
    "Domain",
    "Domain.Projections",
    "Domain.Pipeline",
    "Domain.Client",
    "Client.Infrastructure",
};

const string OutputFileName = "knowledge-graph.json";

// ============================================================================
// MSBuild initialisieren (muss VOR jeder Roslyn-Nutzung passieren)
// ============================================================================

MSBuildLocator.RegisterDefaults();

// ============================================================================
// HAUPTPROGRAMM
// ============================================================================

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║            Knowledge Graph Extractor                      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Solution finden
var solutionPath = args.Length > 0 ? args[0] : FindSolutionFile();
if (solutionPath == null)
{
    Console.Error.WriteLine("❌ Keine Solution gefunden!");
    Console.Error.WriteLine("   Nutzung: dotnet run [path/to/solution.sln]");
    return 1;
}

Console.WriteLine($"📁 Solution: {solutionPath}");
Console.WriteLine($"📋 Ziel-Projekte: {string.Join(", ", targetProjects)}");
Console.WriteLine();

// ─── Solution laden ───

using var workspace = MSBuildWorkspace.Create();
workspace.WorkspaceFailed += (_, e) =>
{
    if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
        Console.WriteLine($"⚠️  {e.Diagnostic.Message}");
};

Console.WriteLine("Lade Solution...");
var solution = await workspace.OpenSolutionAsync(solutionPath);
Console.WriteLine($"   {solution.Projects.Count()} Projekte in Solution");
Console.WriteLine();

// ─── Ziel-Projekte kompilieren ───

var compilations = new List<Compilation>();

Console.WriteLine("Lade Ziel-Projekte:");
foreach (var projectName in targetProjects)
{
    var project = solution.Projects.FirstOrDefault(p =>
        p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

    if (project == null)
    {
        Console.WriteLine($"   ⚠️  {projectName} nicht gefunden — übersprungen");
        continue;
    }

    var compilation = await project.GetCompilationAsync();
    if (compilation != null)
    {
        compilations.Add(compilation);
        Console.WriteLine($"   ✓ {projectName}");
    }
}

if (compilations.Count == 0)
{
    Console.Error.WriteLine("\n❌ Keine Projekte geladen.");
    return 1;
}

// ─── Graph extrahieren ───

Console.WriteLine("\n════════════════════════════════════════");
Console.WriteLine("  Extrahiere Wissensgraph...");
Console.WriteLine("════════════════════════════════════════");

var extractor = new GraphExtractor.GraphExtractor(compilations);
var graph = extractor.Extract();

// ─── JSON serialisieren ───

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

var json = JsonSerializer.Serialize(graph, options);

// Output neben die Solution legen
var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";
var outputPath = Path.Combine(solutionDir, OutputFileName);
await File.WriteAllTextAsync(outputPath, json);

// ─── Zusammenfassung ───

Console.WriteLine();
Console.WriteLine("════════════════════════════════════════");
Console.WriteLine("  Ergebnis");
Console.WriteLine("════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine($"   Aggregate:     {graph.Aggregates.Count}");
Console.WriteLine($"   Decides:       {graph.Aggregates.Sum(a => a.Decides.Count)}");
Console.WriteLine($"   Applies:       {graph.Aggregates.Sum(a => a.Applies.Count)}");
Console.WriteLine($"   Pipelines:     {graph.Pipelines.Count} ({graph.Pipelines.Sum(p => p.Handles.Count)} Handles)");
Console.WriteLine($"   Projektionen:  {graph.Projections.Count} ({graph.Projections.Sum(p => p.Handles.Count)} Handles)");
Console.WriteLine($"   Reader:        {graph.Readers.Count} ({graph.Readers.Sum(r => r.Handles.Count)} Handles)");
Console.WriteLine($"   Queries:       {graph.Queries.Count}");
Console.WriteLine($"   Client-Stores: {graph.ClientStores.Count}");
Console.WriteLine($"   Client-Handler:{graph.ClientHandlers.Count}");
Console.WriteLine($"   Event-Fanouts: {graph.EventFanouts.Count}");
Console.WriteLine();
Console.WriteLine($"✅ {outputPath}");
Console.WriteLine();

return 0;

// ============================================================================
// HELPER
// ============================================================================

static string? FindSolutionFile()
{
    var searchDir = Directory.GetCurrentDirectory();

    for (int i = 0; i < 10; i++)
    {
        if (searchDir == null) break;

        var slnFiles = Directory.GetFiles(searchDir, "*.sln");
        if (slnFiles.Length >= 1)
            return slnFiles[0];

        searchDir = Path.GetDirectoryName(searchDir);
    }

    return null;
}