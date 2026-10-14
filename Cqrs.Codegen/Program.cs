using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abstractions.SourceGeneration;
using Core.SourceGeneration;
using Proto.SourceGeneration;
using Cqrs.Codegen;

// ============================================================================
// VEREINTER CODEGEN-PREPASS  (Reflection über die GEBAUTEN Domain-DLLs)
//
// Erzeugt ALLE Artefakte, die ein NACHGELAGERTES Werkzeug braucht und die ein
// In-Compilation-Roslyn-Generator daher NICHT liefern kann:
//   1. ProtoRepo/domain.proto                     (für protoc / gRPC)
//   2. EventJsonSerializerContext.g.cs             (für den STJ-Source-Generator, Marten-Storage)
//   3. CqrsWireJsonContext.g.cs                    (für den STJ-Source-Generator, Cross-Node-Wire)
//   4. domain_client/generated/vertraege.py        (Akteur-Verträge → Python-Basisklassen, docs/konzept-akteure.md §3)
//   5. domain_client/generated/funktionen.py       (Katalog-Funktionen → Python-Basisklassen, konzept-editor-pipelines §14.5)
//      + funktions_tabelle.py                   (Funktion → Basis, Modul, Klasse der Implementierung — der generische Worker, konzept-werkbank §12.3 E13)
//
// Früher öffnete dieser Prepass die ganze Solution über MSBuildWorkspace (geschachteltes
// MSBuild, fragil, plattformabhängig). Jetzt liest er nur die Metadaten der drei gebauten
// Domain-Assemblies über System.Reflection.MetadataLoadContext — reflexionsfrei zur Laufzeit,
// cross-platform, als schlankes MSBuild-Pre-Build-Target lauffähig.
//
// AUFRUF:  Cqrs.Codegen [<Assembly-Ordner>] [<Config>] [--wenn-geaendert] [--artefakte]
//   arg0 (optional): Ordner mit den DLLs der Codegen-Projekte (Abstractions.Projektstruktur.CodegenProjekte) SAMT ihrer
//                    Abhängigkeits-Closure. Default: der eigene Ausgabeordner des Prepass
//                    (AppContext.BaseDirectory) — dort liegt als Exe-Consumer die volle Closure.
//   arg1 (optional): Build-Config (Debug|Release) — rein informativ fürs Log.
//   --wenn-geaendert: nur erzeugen, wenn sich die gebauten Domänen-DLLs oder das Werkzeug geändert haben (Hash; der Build-Prepass
//                    und codegen.sh) — so braucht kein Build-Skript eine eigene Liste der Domänen-Quellen.
//   --artefakte:     nur die erzeugten Dateien auflisten (relativ zur Solution; für codegen.sh --check).
// ============================================================================

const string ProtoNamespace = "CqrsSolution";
// Welche Projekte die Domäne bilden und wohin die Artefakte gehen: eine Quelle (Abstractions.Projektstruktur, docs/07).
var wurzel = Abstractions.Projektstruktur.StandardWurzel;
var targetProjects = Abstractions.Projektstruktur.CodegenProjekte(wurzel).ToArray();
var flags = args.Where(a => a.StartsWith("--", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
var posArgs = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var pythonGeneriert = Path.Combine(Abstractions.Projektstruktur.PythonWorker(wurzel), Abstractions.Projektstruktur.PythonGeneriert);
var artefakte = new[]
{
    Path.Combine(Abstractions.Projektstruktur.ProtoProjekt, Abstractions.Projektstruktur.ProtoDatei),
    Path.Combine(Abstractions.Projektstruktur.SerialisierungsOrdner, "EventJsonSerializerContext.g.cs"),
    Path.Combine(Abstractions.Projektstruktur.SerialisierungsOrdner, "CqrsWireJsonContext.g.cs"),
    Path.Combine(pythonGeneriert, "vertraege.py"),
    Path.Combine(pythonGeneriert, "funktionen.py"),
    Path.Combine(pythonGeneriert, "funktions_tabelle.py"),
};
if (flags.Contains("--artefakte"))
{
    foreach (var a in artefakte) Console.WriteLine(a.Replace('\\', '/'));
    return 0;
}

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║        Cqrs.Codegen — vereinter Prepass (Reflection)      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

var solutionPath = FindSolutionFile();
if (solutionPath == null)
{
    Console.Error.WriteLine("❌ Keine Solution gefunden!");
    return 1;
}
var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";

// ── Argumente: Assembly-Ordner (mit voller Closure) + Config-Label ───────
var assemblyDir = posArgs.Length > 0 && !string.IsNullOrWhiteSpace(posArgs[0])
    ? Path.GetFullPath(posArgs[0])
    : AppContext.BaseDirectory;
var configLabel = posArgs.Length > 1 && !string.IsNullOrWhiteSpace(posArgs[1]) ? posArgs[1] : "-";

if (!Directory.Exists(assemblyDir))
{
    Console.Error.WriteLine($"❌ Assembly-Ordner fehlt: {assemblyDir}");
    return 1;
}

// ── Inkrementell: die Artefakte hängen allein an den gebauten Domänen-DLLs und am Werkzeug (deterministischer Build) ──
var hashDatei = Path.Combine(assemblyDir, "cqrs-codegen.hash");
var standHash = Stand(assemblyDir, targetProjects.Concat(new[] { "Abstractions", "Cqrs.Codegen", "Proto.SourceGeneration", "Core.SourceGeneration", "Abstractions.SourceGeneration" }));
if (flags.Contains("--wenn-geaendert") && File.Exists(hashDatei) && File.ReadAllText(hashDatei) == standHash
    && artefakte.All(a => File.Exists(Path.Combine(solutionDir, a))))
{
    Console.WriteLine($"Codegen aktuell ({standHash[..16]}…) — übersprungen.");
    return 0;
}

Console.WriteLine($"📁 Solution:  {solutionPath}");
Console.WriteLine($"📦 Assemblies: {assemblyDir}");
Console.WriteLine($"⚙  Config:    {configLabel}");
Console.WriteLine($"📋 Ziel-Projekte: {string.Join(", ", targetProjects)}");
Console.WriteLine();

// ── MetadataLoadContext: Resolver über den Assembly-Ordner (volle Closure) + Laufzeit-BCL ──
// Runtime-DLLs zuerst (kanonische BCL), danach die App-/NuGet-DLLs aus dem Assembly-Ordner.
var resolverPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
    resolverPaths[Path.GetFileNameWithoutExtension(dll)] = dll;

foreach (var dll in Directory.GetFiles(assemblyDir, "*.dll"))
    resolverPaths.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);

var resolver = new System.Reflection.PathAssemblyResolver(resolverPaths.Values);
using var mlc = new MetadataLoadContext(resolver);

// Die drei Domain-Assemblies: Quelle für die STJ-Kontexte (deckungsgleich zum
// domainAssemblies-Filter der Roslyn-Variante — NUR diese drei).
var assemblies = new List<Assembly>();
foreach (var project in targetProjects)
{
    var dllPath = Path.Combine(assemblyDir, project + ".dll");
    if (!File.Exists(dllPath))
    {
        Console.Error.WriteLine($"❌ Assembly fehlt: {dllPath}");
        return 1;
    }
    assemblies.Add(mlc.LoadFromAssemblyPath(dllPath));
    Console.WriteLine($"   ✓ {project}.dll");
}
Console.WriteLine();

// Proto-Scan-Menge: die Roslyn-Variante scannte die GESAMTE Referenz-Closure der drei
// Compilations (nicht nur die Domain-Assemblies) — dadurch landete z.B. das Framework-Event
// Abstractions.CommandFailed als Event im .proto. Ein Typ kann eine Abstractions-Message nur
// implementieren, wenn seine Assembly Abstractions referenziert (oder Abstractions selbst ist).
// Genau diese Assemblies werden zusätzlich gescannt — Framework-DLLs (Marten, Proto.Actor, BCL)
// referenzieren Abstractions nicht und bleiben so faithfully außen vor.
const string AbstractionsName = "Abstractions";
var protoScanAssemblies = new List<Assembly>(assemblies);
var alreadyLoaded = new HashSet<string>(targetProjects, StringComparer.OrdinalIgnoreCase);
foreach (var dll in Directory.GetFiles(assemblyDir, "*.dll"))
{
    var simpleName = Path.GetFileNameWithoutExtension(dll);
    if (alreadyLoaded.Contains(simpleName)) continue;
    try
    {
        var asm = mlc.LoadFromAssemblyPath(dll);
        var isAbstractions = string.Equals(simpleName, AbstractionsName, StringComparison.OrdinalIgnoreCase);
        var referencesAbstractions = asm.GetReferencedAssemblies()
            .Any(r => string.Equals(r.Name, AbstractionsName, StringComparison.OrdinalIgnoreCase));
        if (isAbstractions || referencesAbstractions)
        {
            protoScanAssemblies.Add(asm);
            alreadyLoaded.Add(simpleName);
        }
    }
    catch
    {
        // Nicht ladbare/rein native DLL — für den Proto-Scan irrelevant.
    }
}

// ────────────────────────────────────────────────────────────────────────
// 1. domain.proto  (unveränderte Proto-Logik, wiederverwendet aus Proto.SourceGeneration)
// ────────────────────────────────────────────────────────────────────────
Console.WriteLine("Analysiere Typen (Proto)...");
var analyzer = new ReflectionTypeAnalyzer(protoScanAssemblies);
var allGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IMessagePayload")
    .Concat(analyzer.AnalyzeTypesImplementing("Abstractions.IQuery"))
    .Concat(analyzer.AnalyzeTypesImplementing("Abstractions.IQueryResponse"))
    .Concat(analyzer.AnalyzeTypesImplementing("Abstractions.IPipelineTrigger"))
    .Concat(analyzer.AnalyzeTypesImplementing("Abstractions.IAuftrag"))   // Aufträge der Katalog-Funktionen (externe Ausführer)
    .ToList();

if (allGraphs.Count == 0)
{
    Console.WriteLine("⚠️  Keine Domain-Typen gefunden!");
    return 0;
}

var aggregator = new TypeAggregator();
aggregator.AggregateGraphs(allGraphs);

var objectTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Object");
var commandTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Command");
var eventTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Event");
var queryTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Query");
var queryResponseTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "QueryResponse");
var triggerTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Trigger");
var auftragTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Auftrag");

var protoContent = new FileGenerator().GenerateProtoFile(
    ProtoNamespace, objectTypes, commandTypes, eventTypes,
    queryTypes, queryResponseTypes, triggerTypes, auftragTypes);

var protoPath = Path.Combine(solutionDir, artefakte[0]);
if (!Directory.Exists(Path.GetDirectoryName(protoPath)!))
{
    Console.Error.WriteLine($"❌ ProtoRepo-Verzeichnis fehlt: {Path.GetDirectoryName(protoPath)}");
    return 1;
}
WriteIfChanged(protoPath, protoContent);
Console.WriteLine($"✅ {protoPath}");

// ────────────────────────────────────────────────────────────────────────
// 2 + 3. STJ-Kontext-Partials (Domain-Anteil) — [JsonSerializable]-Manifeste
// ────────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("Scanne Marker (STJ-Kontexte)...");
var (eventContext, wireContext, stats) = JsonContextEmitter.Emit(assemblies);

var serializationDir = Path.Combine(solutionDir, Abstractions.Projektstruktur.SerialisierungsOrdner);
if (!Directory.Exists(serializationDir))
{
    Console.Error.WriteLine($"❌ Serialization-Verzeichnis fehlt: {serializationDir}");
    return 1;
}
var eventCtxPath = Path.Combine(solutionDir, artefakte[1]);
var wireCtxPath = Path.Combine(solutionDir, artefakte[2]);
WriteIfChanged(eventCtxPath, eventContext);
WriteIfChanged(wireCtxPath, wireContext);
Console.WriteLine($"✅ {eventCtxPath}");
Console.WriteLine($"✅ {wireCtxPath}");

// ────────────────────────────────────────────────────────────────────────
// 4. Akteur-Verträge → Python (die Worker programmieren gegen die generierte Basis)
// ────────────────────────────────────────────────────────────────────────
var (vertraegePy, vertragAnzahl) = PythonVertragsEmitter.Emit(assemblies);
var vertraegePfad = Path.Combine(solutionDir, artefakte[3]);
if (Directory.Exists(Path.GetDirectoryName(vertraegePfad)!))
{
    WriteIfChanged(vertraegePfad, vertraegePy);
    Console.WriteLine($"✅ {vertraegePfad} ({vertragAnzahl} Verträge)");
}
else Console.WriteLine($"⚠️  Python-Generat-Ordner fehlt — Verträge nicht geschrieben: {Path.GetDirectoryName(vertraegePfad)}");

// ────────────────────────────────────────────────────────────────────────
// 5. Katalog-Funktionen → Python (ein Worker bietet sie an: <Funktion>Basis mit rufe(auftrag, x))
// ────────────────────────────────────────────────────────────────────────
var (funktionenPy, funktionAnzahl) = PythonFunktionsEmitter.Emit(assemblies);
var funktionenPfad = Path.Combine(solutionDir, artefakte[4]);
if (Directory.Exists(Path.GetDirectoryName(funktionenPfad)!))
{
    WriteIfChanged(funktionenPfad, funktionenPy);
    Console.WriteLine($"✅ {funktionenPfad} ({funktionAnzahl} Funktionen)");
    // … und die Tabelle des generischen Workers (Funktion → Basis, Modul, Klasse der Implementierung, konzept-werkbank §12.3 E13).
    var tabellePfad = Path.Combine(solutionDir, artefakte[5]);
    WriteIfChanged(tabellePfad, PythonFunktionsEmitter.Tabelle(assemblies));
    Console.WriteLine($"✅ {tabellePfad}");
}
else Console.WriteLine($"⚠️  Python-Generat-Ordner fehlt — Funktionen nicht geschrieben: {Path.GetDirectoryName(funktionenPfad)}");

Console.WriteLine();
Console.WriteLine($"📊 {stats}");
Console.WriteLine();
File.WriteAllText(hashDatei, standHash);
Console.WriteLine("Fertig. (Signale werden bewusst NICHT registriert — der WireSerializerGenerator");
Console.WriteLine(" serialisiert sie uniform (StreamId, Version) selbst, ohne STJ.)");
Console.WriteLine();
return 0;

// ============================================================================
// Nur schreiben, wenn sich der Inhalt ändert — hält die Datei-Timestamps stabil,
// damit MSBuilds Inputs/Outputs-Inkrementalität nachgelagerte Rebuilds nicht unnötig triggert.
static void WriteIfChanged(string path, string content)
{
    if (File.Exists(path) && File.ReadAllText(path) == content)
        return;
    File.WriteAllText(path, content);
}

// Hash über die DLLs, aus denen die Artefakte entstehen (Name + Inhalt, sortiert).
static string Stand(string verzeichnis, IEnumerable<string> namen)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    foreach (var n in namen.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
    {
        var pfad = Path.Combine(verzeichnis, n + ".dll");
        var kopf = System.Text.Encoding.UTF8.GetBytes(n + "\0");
        sha.TransformBlock(kopf, 0, kopf.Length, null, 0);
        if (File.Exists(pfad)) { var b = File.ReadAllBytes(pfad); sha.TransformBlock(b, 0, b.Length, null, 0); }
    }
    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    return Convert.ToHexString(sha.Hash!);
}

static string? FindSolutionFile()
{
    var searchDir = Directory.GetCurrentDirectory();
    for (int i = 0; i < 10 && searchDir != null; i++)
    {
        var slnFiles = Directory.GetFiles(searchDir, "*.sln");
        if (slnFiles.Length >= 1) return slnFiles[0];
        searchDir = Path.GetDirectoryName(searchDir);
    }
    return null;
}
