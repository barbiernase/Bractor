using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abstractions.SourceGeneration;
using Core.SourceGeneration;
using Proto.SourceGeneration;
using Cqrs.Codegen;
using Struktur = Abstractions.Projektstruktur;

// ============================================================================
// VEREINTER CODEGEN-PREPASS  (Reflection über die GEBAUTEN Domain-DLLs)
//
// Erzeugt je ANWENDUNG (Wurzel, docs/07) alle Artefakte, die ein NACHGELAGERTES Werkzeug braucht und die ein
// In-Compilation-Roslyn-Generator daher NICHT liefern kann — jeweils nur, wenn die Rolle in der Anwendung existiert:
//   1. <Wurzel>.Leitung/domain.proto               (Domänen-DTOs der Leitung, für protoc — nur mit Leitungs-Projekt)
//   2. EventJsonSerializerContext.g.cs             (für den STJ-Source-Generator, Marten-Storage — in die Komposition)
//   3. CqrsWireJsonContext.g.cs                    (für den STJ-Source-Generator, Cross-Node-Wire — in die Komposition)
//   4. domain_client/generated/vertraege.py        (Akteur-Verträge → Python-Basisklassen, docs/konzept-akteure.md §3)
//   5. domain_client/generated/funktionen.py       (Katalog-Funktionen → Python-Basisklassen, konzept-editor-pipelines §14.5)
//      + funktions_tabelle.py                   (Funktion → Basis, Modul, Klasse der Implementierung — der generische Worker, konzept-werkbank §12.3 E13)
//   6. domain_client/generated/nutzlast_tabelle.py (Nutzlast.typ → DTO-Klasse, Gegenstück zu GeneratedNutzlast, dieselbe Namensregel)
//   (4–6 nur mit Python-Worker-Projekt der Anwendung.)
//
// Früher öffnete dieser Prepass die ganze Solution über MSBuildWorkspace (geschachteltes
// MSBuild, fragil, plattformabhängig). Jetzt liest er nur die Metadaten der gebauten
// Domain-Assemblies über System.Reflection.MetadataLoadContext — als schlankes MSBuild-Pre-Build-Target
// lauffähig (Reflection im Werkzeug ist offen: docs/13 W10-1).
//
// AUFRUF:  Cqrs.Codegen [<Assembly-Ordner>] [<Config>] [--wurzel=<W> | --projekt=<Projekt> | --alle] [--wenn-geaendert] [--artefakte]
//   arg0 (optional): Ordner mit den DLLs der Codegen-Projekte (Abstractions.Projektstruktur.CodegenProjekte) SAMT ihrer
//                    Abhängigkeits-Closure. Default: der eigene Ausgabeordner des Prepass
//                    (AppContext.BaseDirectory) — dort liegt als Exe-Consumer die volle Closure.
//   arg1 (optional): Build-Config (Debug|Release) — rein informativ fürs Log.
//   --wurzel=<W>:    die Anwendung (Default: Projektstruktur.StandardWurzel).
//   --projekt=<P>:   die Anwendung, zu der das Projekt P gehört (der Build-Prepass übergibt seinen Projektnamen, CqrsCodegen.targets).
//   --alle:          jede Anwendung der Solution (eine Komposition <W>.Laufzeit je Anwendung; codegen.sh).
//   --wenn-geaendert: nur erzeugen, wenn sich die gebauten Domänen-DLLs oder das Werkzeug geändert haben (Hash je Wurzel; der
//                    Build-Prepass und codegen.sh) — so braucht kein Build-Skript eine eigene Liste der Domänen-Quellen.
//   --artefakte:     nur die erzeugten Dateien auflisten (relativ zur Solution; für codegen.sh --check).
// ============================================================================

var flags = args.Where(a => a.StartsWith("--", StringComparison.Ordinal) && !a.Contains('=')).ToHashSet(StringComparer.Ordinal);
var werte = args.Where(a => a.StartsWith("--", StringComparison.Ordinal) && a.Contains('='))
    .ToDictionary(a => a[..a.IndexOf('=')], a => a[(a.IndexOf('=') + 1)..], StringComparer.Ordinal);
var posArgs = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();

var solutionPath = FindSolutionFile();
if (solutionPath == null)
{
    Console.Error.WriteLine("❌ Keine Solution gefunden!");
    return 1;
}
var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";

// Welche Anwendungen: eine Quelle (Abstractions.Projektstruktur, docs/07) — Wurzel aus dem Projektnamen bzw. je Komposition eine.
string[] wurzeln = flags.Contains("--alle") ? Kompositionen(solutionDir)
    : werte.TryGetValue("--projekt", out var projekt) ? new[] { Struktur.Zerlege(projekt).Wurzel }
    : new[] { werte.TryGetValue("--wurzel", out var w) ? w : Struktur.StandardWurzel };

if (flags.Contains("--artefakte"))
{
    foreach (var a in wurzeln.SelectMany(w => Artefakte(solutionDir, w))) Console.WriteLine(a.Pfad.Replace('\\', '/'));
    return 0;
}

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║        Cqrs.Codegen — vereinter Prepass (Reflection)      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

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

foreach (var wurzel in wurzeln)
{
    var rc = Erzeuge(wurzel);
    if (rc != 0) return rc;
}
return 0;

// ============================================================================
// Eine Anwendung: ihre Codegen-Projekte lesen und ihre Artefakte schreiben.
int Erzeuge(string wurzel)
{
    // Nur die Rollen-Projekte, die es in dieser Anwendung gibt (eine Test-Anwendung hat z. B. nur die Schreibseite).
    // Ein Projekt ist, was eine .csproj hat — ein liegengebliebener bin-Ordner eines gelöschten Projekts (Checkout ohne git clean -x) ist keins.
    var targetProjects = Struktur.CodegenProjekte(wurzel).Where(p => File.Exists(Path.Combine(solutionDir, p, p + ".csproj"))).ToArray();
    var artefakte = Artefakte(solutionDir, wurzel);
    string Pfad(Art art) => Path.Combine(solutionDir, artefakte.Single(a => a.Art == art).Pfad);
    bool Hat(Art art) => artefakte.Any(a => a.Art == art);

    if (targetProjects.Length == 0)
    {
        Console.Error.WriteLine($"❌ Anwendung '{wurzel}' hat kein Codegen-Projekt ({string.Join(", ", Struktur.CodegenProjekte(wurzel))}).");
        return 1;
    }

    // ── Inkrementell: die Artefakte hängen allein an den gebauten Domänen-DLLs und am Werkzeug (deterministischer Build) ──
    var hashDatei = Path.Combine(assemblyDir, $"cqrs-codegen.{wurzel}.hash");
    var standHash = Stand(assemblyDir, targetProjects.Concat(new[] { "Abstractions", "Cqrs.Codegen", "Proto.SourceGeneration", "Core.SourceGeneration", "Abstractions.SourceGeneration" }));
    if (flags.Contains("--wenn-geaendert") && File.Exists(hashDatei) && File.ReadAllText(hashDatei) == standHash
        && artefakte.All(a => File.Exists(Path.Combine(solutionDir, a.Pfad))))
    {
        Console.WriteLine($"Codegen {wurzel} aktuell ({standHash[..16]}…) — übersprungen.");
        return 0;
    }

    Console.WriteLine($"📁 Solution:  {solutionPath}");
    Console.WriteLine($"📦 Assemblies: {assemblyDir}");
    Console.WriteLine($"⚙  Config:    {configLabel}");
    Console.WriteLine($"🌱 Anwendung: {wurzel}");
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

    // Die Domain-Assemblies der Anwendung: Quelle für die STJ-Kontexte und die Python-Generate.
    var assemblies = new List<Assembly>();
    foreach (var project in targetProjects)
    {
        var dllPath = Path.Combine(assemblyDir, project + ".dll");
        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"❌ Assembly fehlt: {dllPath} (Cqrs.Codegen muss {project} referenzieren)");
            return 1;
        }
        assemblies.Add(mlc.LoadFromAssemblyPath(dllPath));
        Console.WriteLine($"   ✓ {project}.dll");
    }
    Console.WriteLine();

    // Proto-Scan-Menge: die Domain-Assemblies UND ihre Referenz-Hülle, soweit sie Abstractions-Nachrichten tragen kann (Abstractions
    // selbst oder ein Nutzer davon) — so landet z. B. das Framework-Event Abstractions.CommandFailed im .proto. Nur die HÜLLE der
    // Anwendung: die DLLs einer anderen Anwendung im selben Werkzeug-Ordner (eine Test-Anwendung) bleiben draußen.
    const string AbstractionsName = "Abstractions";
    var protoScanAssemblies = new List<Assembly>(assemblies);
    var gesehen = new HashSet<string>(targetProjects, StringComparer.OrdinalIgnoreCase);
    var offen = new Queue<Assembly>(assemblies);
    while (offen.Count > 0)
    {
        foreach (var r in offen.Dequeue().GetReferencedAssemblies())
        {
            if (r.Name == null || !gesehen.Add(r.Name) || !resolverPaths.TryGetValue(r.Name, out var pfad)
                || Path.GetDirectoryName(pfad) == runtimeDir) continue;
            try
            {
                var asm = mlc.LoadFromAssemblyPath(pfad);
                var isAbstractions = string.Equals(r.Name, AbstractionsName, StringComparison.OrdinalIgnoreCase);
                if (!isAbstractions && !asm.GetReferencedAssemblies().Any(x => string.Equals(x.Name, AbstractionsName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                protoScanAssemblies.Add(asm);
                offen.Enqueue(asm);
            }
            catch
            {
                // Nicht ladbare/rein native DLL — für den Proto-Scan irrelevant.
            }
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

    var aggregator = new TypeAggregator();
    aggregator.AggregateGraphs(allGraphs);

    var objectTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Object");
    var commandTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Command");
    var eventTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Event");
    var queryTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Query");
    var queryResponseTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "QueryResponse");
    var triggerTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Trigger");
    var auftragTypes = aggregator.GetTypesSortedByDepth(domainTypeFilter: "Auftrag");

    if (Hat(Art.Proto))
    {
        // Paket und C#-Namespace aus dem Leitungs-Projekt (docs/07): Namespace = Projekt, Paket = Projekt in Kleinbuchstaben (Proto-Konvention).
        var leitung = Struktur.Projekt(wurzel, Struktur.Rolle.Leitung);
        var protoContent = new FileGenerator().GenerateProtoFile(
            leitung.ToLowerInvariant(), objectTypes, commandTypes, eventTypes,
            queryTypes, queryResponseTypes, triggerTypes, auftragTypes, csharpNamespace: leitung);
        WriteIfChanged(Pfad(Art.Proto), protoContent);
        Console.WriteLine($"✅ {Pfad(Art.Proto)}");
    }
    else Console.WriteLine($"·  kein Leitungs-Projekt ({Struktur.Projekt(wurzel, Struktur.Rolle.Leitung)}) — kein domain.proto");

    // ────────────────────────────────────────────────────────────────────────
    // 2 + 3. STJ-Kontext-Partials (Domain-Anteil) — [JsonSerializable]-Manifeste in die Komposition
    // ────────────────────────────────────────────────────────────────────────
    Console.WriteLine();
    Console.WriteLine("Scanne Marker (STJ-Kontexte)...");
    var (eventContext, wireContext, stats) = JsonContextEmitter.Emit(assemblies);

    var serializationDir = Path.Combine(solutionDir, Struktur.SerialisierungsOrdner(wurzel));
    if (!Directory.Exists(serializationDir))
    {
        Console.Error.WriteLine($"❌ Serialization-Verzeichnis der Komposition fehlt: {serializationDir}");
        return 1;
    }
    WriteIfChanged(Pfad(Art.EventJson), eventContext);
    WriteIfChanged(Pfad(Art.WireJson), wireContext);
    Console.WriteLine($"✅ {Pfad(Art.EventJson)}");
    Console.WriteLine($"✅ {Pfad(Art.WireJson)}");

    if (Hat(Art.Vertraege))
    {
        // ────────────────────────────────────────────────────────────────────
        // 4. Akteur-Verträge → Python (die Worker programmieren gegen die generierte Basis)
        // ────────────────────────────────────────────────────────────────────
        var (vertraegePy, vertragAnzahl) = PythonVertragsEmitter.Emit(assemblies);
        WriteIfChanged(Pfad(Art.Vertraege), vertraegePy);
        Console.WriteLine($"✅ {Pfad(Art.Vertraege)} ({vertragAnzahl} Verträge)");

        // ────────────────────────────────────────────────────────────────────
        // 5. Katalog-Funktionen → Python (ein Worker bietet sie an: <Funktion>Basis mit rufe(auftrag, x)) + die Tabelle des
        //    generischen Workers (Funktion → Basis, Modul, Klasse der Implementierung, konzept-werkbank §12.3 E13)
        // ────────────────────────────────────────────────────────────────────
        var (funktionenPy, funktionAnzahl) = PythonFunktionsEmitter.Emit(assemblies);
        WriteIfChanged(Pfad(Art.Funktionen), funktionenPy);
        Console.WriteLine($"✅ {Pfad(Art.Funktionen)} ({funktionAnzahl} Funktionen)");
        WriteIfChanged(Pfad(Art.FunktionsTabelle), PythonFunktionsEmitter.Tabelle(assemblies));
        Console.WriteLine($"✅ {Pfad(Art.FunktionsTabelle)}");

        // ────────────────────────────────────────────────────────────────────
        // 6. Nutzlast-Tabelle → Python (typ → DTO-Klasse; dieselben Kategorien und dieselbe Namensregel wie GeneratedNutzlast in C#)
        // ────────────────────────────────────────────────────────────────────
        var (nutzlastPy, nutzlastAnzahl) = PythonNutzlastEmitter.Emit(commandTypes, eventTypes, queryTypes, queryResponseTypes, triggerTypes, auftragTypes);
        WriteIfChanged(Pfad(Art.NutzlastTabelle), nutzlastPy);
        Console.WriteLine($"✅ {Pfad(Art.NutzlastTabelle)} ({nutzlastAnzahl} Nutzlast-Typen)");
    }
    else Console.WriteLine($"·  kein Python-Worker ({Struktur.PythonWorker(wurzel)}) — keine Python-Generate");

    Console.WriteLine();
    Console.WriteLine($"📊 {stats}");
    Console.WriteLine();
    File.WriteAllText(hashDatei, standHash);
    Console.WriteLine("Fertig. (Signale werden bewusst NICHT registriert — der WireSerializerGenerator");
    Console.WriteLine(" serialisiert sie uniform (StreamId, Version) selbst, ohne STJ.)");
    Console.WriteLine();
    return 0;
}

// Die Artefakte einer Anwendung (relativ zur Solution) — je Rolle nur, wenn ihr Projekt existiert (docs/07 §7.2).
static List<(Art Art, string Pfad)> Artefakte(string solutionDir, string wurzel)
{
    var liste = new List<(Art, string)>();
    var leitung = Struktur.Projekt(wurzel, Struktur.Rolle.Leitung);
    if (Directory.Exists(Path.Combine(solutionDir, leitung))) liste.Add((Art.Proto, Path.Combine(leitung, Struktur.ProtoDatei)));
    liste.Add((Art.EventJson, Path.Combine(Struktur.SerialisierungsOrdner(wurzel), "EventJsonSerializerContext.g.cs")));
    liste.Add((Art.WireJson, Path.Combine(Struktur.SerialisierungsOrdner(wurzel), "CqrsWireJsonContext.g.cs")));
    if (Directory.Exists(Path.Combine(solutionDir, Struktur.PythonWorker(wurzel))))
    {
        var py = Path.Combine(Struktur.PythonWorker(wurzel), Struktur.PythonGeneriert);
        liste.Add((Art.Vertraege, Path.Combine(py, "vertraege.py")));
        liste.Add((Art.Funktionen, Path.Combine(py, "funktionen.py")));
        liste.Add((Art.FunktionsTabelle, Path.Combine(py, "funktions_tabelle.py")));
        liste.Add((Art.NutzlastTabelle, Path.Combine(py, "nutzlast_tabelle.py")));
    }
    return liste;
}

// Die Anwendungen der Solution: je Komposition <W>.Laufzeit/<W>.Laufzeit.csproj eine Wurzel W (Rolle Komposition, docs/07).
static string[] Kompositionen(string solutionDir) => Directory.GetDirectories(solutionDir)
    .Select(Path.GetFileName)
    .Where(n => n != null && Struktur.Zerlege(n).Rolle == Struktur.Rolle.Komposition && File.Exists(Path.Combine(solutionDir, n, n + ".csproj")))
    .Select(n => Struktur.Zerlege(n!).Wurzel)
    .OrderBy(w => w, StringComparer.Ordinal)
    .ToArray();

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

/// <summary>Die Artefakt-Arten des Codegens.</summary>
enum Art { Proto, EventJson, WireJson, Vertraege, Funktionen, FunktionsTabelle, NutzlastTabelle }
