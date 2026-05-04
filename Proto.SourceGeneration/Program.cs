using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abstractions.SourceGeneration;
using Core.SourceGeneration;
using Proto.SourceGeneration;

MSBuildLocator.RegisterDefaults();

// ============================================================================
// KONFIGURATION
// ============================================================================

// Projekte die analysiert werden sollen (nach Name)
var targetProjects = new[] { "Domain", "Domain.Projections" };

const string OutputFileName = "domain.proto";
const string OutputProjectDir = "ProtoRepo";
const string ProtoNamespace = "CqrsSolution";

// ============================================================================
// HAUPTPROGRAMM
// ============================================================================

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║              Proto Generator                              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Solution finden
var solutionPath = FindSolutionFile();
if (solutionPath == null)
{
    Console.Error.WriteLine("❌ Keine Solution gefunden!");
    return 1;
}

Console.WriteLine($"📁 Solution: {solutionPath}");
Console.WriteLine($"📋 Ziel-Projekte: {string.Join(", ", targetProjects)}");
Console.WriteLine();

using var workspace = MSBuildWorkspace.Create();
workspace.WorkspaceFailed += (o, e) => 
{ 
    if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
        Console.WriteLine($"⚠️  {e.Diagnostic.Message}"); 
};

// Solution laden
Console.WriteLine("Lade Solution...");
var solution = await workspace.OpenSolutionAsync(solutionPath);
Console.WriteLine($"   {solution.Projects.Count()} Projekte in Solution");
Console.WriteLine();

// Ziel-Projekte filtern und laden
var compilations = new List<Compilation>();

Console.WriteLine("Lade Ziel-Projekte:");
foreach (var projectName in targetProjects)
{
    var project = solution.Projects.FirstOrDefault(p => 
        p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
    
    if (project == null)
    {
        Console.WriteLine($"   ⚠️  {projectName} nicht gefunden");
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
    Console.Error.WriteLine("❌ Keine Projekte geladen.");
    return 1;
}

Console.WriteLine();

// Typen analysieren
Console.WriteLine("Analysiere Typen...");
var analyzer = new MultiCompilationAnalyzer(compilations);

var messagePayloadGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IMessagePayload");
var queryGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IQuery");
var queryResponseGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IQueryResponse");

var allGraphs = messagePayloadGraphs
    .Concat(queryGraphs)
    .Concat(queryResponseGraphs)
    .ToList();

if (allGraphs.Count == 0)
{
    Console.WriteLine("⚠️  Keine Domain-Typen gefunden!");
    return 0;
}

// Aggregieren
var aggregator = new TypeAggregator();
aggregator.AggregateGraphs(allGraphs);

var objectTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Object");
var commandTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Command");
var eventTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Event");
var queryTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Query");
var queryResponseTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "QueryResponse");

Console.WriteLine();
Console.WriteLine("📊 Gefundene Typen:");
Console.WriteLine($"   Commands:        {commandTypes.Count}");
Console.WriteLine($"   Events:          {eventTypes.Count}");
Console.WriteLine($"   Queries:         {queryTypes.Count}");
Console.WriteLine($"   QueryResponses:  {queryResponseTypes.Count}");
Console.WriteLine($"   Value Objects:   {objectTypes.Count}");
Console.WriteLine();

// Proto generieren
var generator = new FileGenerator();
var protoContent = generator.GenerateProtoFile(
    ProtoNamespace, 
    objectTypes, 
    commandTypes, 
    eventTypes,
    queryTypes,
    queryResponseTypes);

// Output im ProtoRepo-Projektordner
var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";
var outputDir = Path.Combine(solutionDir, OutputProjectDir);

if (!Directory.Exists(outputDir))
{
    Console.Error.WriteLine($"❌ Zielverzeichnis nicht gefunden: {outputDir}");
    return 1;
}

var outputPath = Path.Combine(outputDir, OutputFileName);
await File.WriteAllTextAsync(outputPath, protoContent);

Console.WriteLine($"✅ {outputPath}");
Console.WriteLine();

return 0;

// ============================================================================
// HELPER
// ============================================================================

static string? FindSolutionFile()
{
    var currentDir = Directory.GetCurrentDirectory();
    var searchDir = currentDir;
    
    for (int i = 0; i < 10; i++)
    {
        if (searchDir == null) break;
        
        var slnFiles = Directory.GetFiles(searchDir, "*.sln");
        if (slnFiles.Length >= 1)
        {
            return slnFiles[0];
        }
        
        searchDir = Path.GetDirectoryName(searchDir);
    }
    
    return null;
}