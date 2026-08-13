using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abstractions;
using Cqrs.Testing;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SimHost;

// ── Ergebnis-DTOs für das Board ──
public record CompileFehler(string Schweregrad, string Code, string Meldung, string? Datei);
public record CompileErgebnis(bool Ok, List<CompileFehler> Fehler, int Aggregate, int Sagas);
public record RunFrame(string Command, string Ursprung, List<string> Events, List<string> Ablehnungen, bool Unrouted);
public record RunState(string Aggregate, string Id, Dictionary<string, object?> Felder);
public record RunErgebnis(bool Ok, List<CompileFehler> Fehler, List<RunFrame> Frames, List<RunState> States);

/// <summary>
/// Das getippte Domänen-Modell LIVE ausführen, ohne Neustart: der <see cref="Scaffolder"/> erzeugt C#,
/// Roslyn übersetzt es IN-MEMORY mit den ECHTEN Domain-Generatoren (dieselben State/Handler/Factory wie
/// im Cluster), das Ergebnis läuft über den store-freien Kern (<see cref="SagaLaufwerk"/>). Kein Proto,
/// keine Platte, kein Actor — „Modell = ausführbare Daten" (eine Wahrheit, ein zweites Backend).
///
/// EHRLICHE GRENZEN: Nur der interne Kern-Pfad (kein Marten/Wire/Proto). Kompilate werden nach Modell-
/// Hash gecacht; alte Assemblies bleiben im Prozess (kein Unload) — für ein Editor-Werkzeug vertretbar.
/// </summary>
public sealed class ModellRuntime
{
    private sealed class Kompilat
    {
        public required CompileErgebnis Ergebnis { get; init; }
        public Assembly? Assembly { get; init; }
        public SagaLaufwerk? Lauf { get; set; }
    }

    private static readonly JsonSerializerOptions CmdJson = new() { PropertyNameCaseInsensitive = true };
    private readonly ConcurrentDictionary<string, Kompilat> _cache = new();

    /// <summary>Nur übersetzen und die Diagnosen melden — der Self-Repair-/Guardrail-Signalgeber.</summary>
    public CompileErgebnis Kompiliere(EditorModell modell) => Hole(modell).Ergebnis;

    /// <summary>Übersetzen (gecacht) und einen Command mit Werten ausführen; liefert Trace + Endzustände.</summary>
    public RunErgebnis Fahre(EditorModell modell, string commandName, JsonElement werte, bool reset)
    {
        var k = Hole(modell);
        if (!k.Ergebnis.Ok || k.Assembly is null)
            return new RunErgebnis(false, k.Ergebnis.Fehler, [], []);

        if (reset || k.Lauf is null) k.Lauf = BaueLauf(k.Assembly);

        var cmdType = k.Assembly.GetTypes().FirstOrDefault(t => typeof(ICommand).IsAssignableFrom(t) && t.Name == commandName);
        if (cmdType is null)
            return new RunErgebnis(false, [new("error", "RUN-CMD", $"Command '{commandName}' nicht im Kompilat gefunden.", null)], [], []);

        var cmd = (ICommand)JsonSerializer.Deserialize(werte.GetRawText(), cmdType, CmdJson)!;
        var trace = k.Lauf!.Fahre(cmd);

        var frames = trace.Schritte.Select(s => new RunFrame(
            s.Command.GetType().Name,
            s.Ursprung.ToString(),
            s.Ausgang.Where(e => e.Persistent).Select(e => e.Typ).ToList(),
            s.Ausgang.Where(e => !e.Persistent).Select(e => e.Typ).ToList(),
            s.Unrouted)).ToList();

        var states = k.Lauf.AlleZustände()
            .Select(z => new RunState(z.Typ, z.Id.ToString(), new Dictionary<string, object?>(z.Felder)))
            .ToList();

        return new RunErgebnis(true, [], frames, states);
    }

    // ── In-Memory-Übersetzung ──
    private Kompilat Hole(EditorModell modell) => _cache.GetOrAdd(Hash(modell.AlsJson()), _ => Übersetze(modell));

    private static Kompilat Übersetze(EditorModell modell)
    {
        var trees = Scaffolder.Generiere(modell)
            .Select(d => CSharpSyntaxTree.ParseText(d.Inhalt, path: d.Pfad)).ToList();
        // Implizite globale usings (wie ImplicitUsings=enable im echten Build).
        trees.Add(CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;\n" +
            "global using System.Threading;\nglobal using System.Threading.Tasks;\n", path: "GlobalUsings.g.cs"));

        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var refs = tpa.Where(p => p.EndsWith(".dll")).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

        var comp = CSharpCompilation.Create("EditorLive_" + Guid.NewGuid().ToString("N"),
            trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // Die ECHTEN Domain-Generatoren (State/Handler/Factory/Signal/Prozess…) laufen lassen.
        var genAsm = typeof(Domain.SourceGeneration.Generator).Assembly;
        var gens = genAsm.GetTypes().Where(t => !t.IsAbstract &&
                (typeof(IIncrementalGenerator).IsAssignableFrom(t) || typeof(ISourceGenerator).IsAssignableFrom(t)))
            .Select(t => Activator.CreateInstance(t)!)
            .Select(o => o is IIncrementalGenerator ig ? ig.AsSourceGenerator() : (ISourceGenerator)o)
            .ToArray();

        CSharpGeneratorDriver.Create(gens).RunGeneratorsAndUpdateCompilation(comp, out var outComp, out var genDiag);

        using var ms = new MemoryStream();
        var emit = outComp.Emit(ms);

        var fehler = emit.Diagnostics.Concat(genDiag)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => new CompileFehler("error", d.Id, d.GetMessage(), d.Location.SourceTree?.FilePath))
            .DistinctBy(f => (f.Code, f.Meldung, f.Datei))
            .ToList();

        if (!emit.Success)
            return new Kompilat { Ergebnis = new CompileErgebnis(false, fehler, modell.Aggregate.Count, modell.Sagas.Count) };

        ms.Position = 0;
        var asm = Assembly.Load(ms.ToArray());
        return new Kompilat
        {
            Assembly = asm,
            Ergebnis = new CompileErgebnis(true, fehler, modell.Aggregate.Count, modell.Sagas.Count),
        };
    }

    private static SagaLaufwerk BaueLauf(Assembly asm)
    {
        var factoryType = asm.GetType("Infrastructure.AggregateHandlerFactory")
            ?? throw new InvalidOperationException("AggregateHandlerFactory nicht generiert (kein Aggregat?).");
        var factory = (IAggregateHandlerFactory)Activator.CreateInstance(factoryType)!;

        // Prozess-Regeln direkt aus den kompilierten IProzessDefinition-Typen (wie SagaSzenario.Mit).
        var prozesse = asm.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IProzessDefinition).IsAssignableFrom(t))
            .Select(t => (IProzessDefinition)Activator.CreateInstance(t)!)
            .Select(p => (p.GetType().Name, p.Regeln))
            .ToList();

        return new SagaLaufwerk(factory, prozesse);
    }

    private static string Hash(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }
}
