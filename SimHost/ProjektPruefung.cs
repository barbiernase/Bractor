using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Projections.SourceGeneration;

namespace SimHost;

// ════════════════════════════════════════════════════════════════════════════
//  Prüfung eines Kandidaten-Rumpfs gegen das ECHTE Projekt, in dem die Methode steht — vor dem Schreiben.
//
//  Für Store-Funktionen gibt es kein Modell-Kompilat (das Editor-Modell trägt keine Store-Rümpfe). Deshalb: den Rumpf im
//  Speicher in die Datei einsetzen (dieselbe Einsetzung wie beim Übernehmen), das Projekt aus seinen Quellen + seinen
//  Compile-Referenzen (wie im echten Build: project.assets.json) kompilieren und die Store-Regeln (CQRS066/067) darüber laufen lassen. Es zählen nur NEUE Befunde
//  gegenüber dem unveränderten Projekt — was dort schon fehlt (z. B. Generat, das in-memory nicht entsteht), ist kein Befund.
//  Nichts wird geschrieben.
// ════════════════════════════════════════════════════════════════════════════

public sealed class ProjektPruefung
{
    private readonly string _sln;
    private readonly object _sperre = new();
    private readonly Dictionary<string, HashSet<string>> _basis = new();   // Projekt-Stand → Befunde des unveränderten Projekts

    public ProjektPruefung(string slnRoot) => _sln = slnRoot;

    public List<string> Pruefe(MethodenAnker anker, string rumpf)
    {
        var (ok, grund, pfad, neuText) = CodeSync.MitRumpf(anker, rumpf, _sln);
        if (!ok) return new List<string> { grund };
        var projekt = ProjektVon(pfad!);
        if (projekt == null) return new List<string> { $"Kein Projekt zu {anker.Datei} gefunden." };
        var refs = Referenzen(projekt);
        if (refs == null) return new List<string> { $"{Path.GetFileNameWithoutExtension(projekt)} ist noch nicht gebaut — erst bauen, dann prüfen." };

        var quellen = Quellen(projekt);
        var stand = string.Join("|", quellen.Select(q => q + "@" + File.GetLastWriteTimeUtc(q).Ticks)) + "|" + refs.Count;
        HashSet<string>? basis;
        lock (_sperre) _basis.TryGetValue(stand, out basis);
        if (basis == null)
        {
            basis = Befunde(projekt, quellen, refs, null, null).ToHashSet();
            lock (_sperre) _basis[stand] = basis;
        }
        return Befunde(projekt, quellen, refs, pfad, neuText).Where(b => !basis.Contains(b)).Distinct().Take(20).ToList();
    }

    private static List<string> Befunde(string projekt, List<string> quellen, List<MetadataReference> refs, string? ersetzt, string? neuText)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var baeume = quellen.Select(q => CSharpSyntaxTree.ParseText(
            q == ersetzt ? neuText! : File.ReadAllText(q), parse, path: q)).ToList();
        var comp = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(projekt), baeume, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var analyzer = ImmutableArrayVon(new StoreZugriffAnalyzer());
        var diags = comp.WithAnalyzers(analyzer).GetAllDiagnosticsAsync().GetAwaiter().GetResult();
        return diags.Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}").ToList();
    }

    private static System.Collections.Immutable.ImmutableArray<DiagnosticAnalyzer> ImmutableArrayVon(params DiagnosticAnalyzer[] a)
        => System.Collections.Immutable.ImmutableArray.Create(a);

    /// <summary>Das nächste .csproj über der Datei.</summary>
    private string? ProjektVon(string datei)
    {
        for (var d = Path.GetDirectoryName(datei); d != null && d.StartsWith(_sln, StringComparison.Ordinal); d = Path.GetDirectoryName(d))
            if (Directory.GetFiles(d, "*.csproj").FirstOrDefault() is { } p) return p;
        return null;
    }

    /// <summary>Alle Quellen des Projekts (ohne bin/obj) plus die globalen usings seines letzten Builds (ImplicitUsings).</summary>
    private static List<string> Quellen(string projekt)
    {
        var dir = Path.GetDirectoryName(projekt)!;
        bool Ausgabe(string f) => f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                  || f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
        var quellen = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Where(f => !Ausgabe(f)).ToList();
        var obj = Path.Combine(dir, "obj");
        if (Directory.Exists(obj))
            quellen.AddRange(Directory.GetFiles(obj, "*.GlobalUsings.g.cs", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(1));
        return quellen.OrderBy(q => q, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Die Compile-Referenzen wie im echten Build: NuGet-Pakete aus <c>obj/project.assets.json</c> (eine Bibliothek kopiert sie
    /// nicht nach bin), Projekt-Referenzen aus der Build-Ausgabe, dazu das Laufzeit-Framework. null = nie gebaut/restored.
    /// </summary>
    private static List<MetadataReference>? Referenzen(string projekt)
    {
        var dir = Path.GetDirectoryName(projekt)!;
        var name = Path.GetFileNameWithoutExtension(projekt);
        var assets = Path.Combine(dir, "obj", "project.assets.json");
        var bin = Path.Combine(dir, "bin");
        var eigene = Directory.Exists(bin)
            ? Directory.GetFiles(bin, name + ".dll", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        if (eigene == null || !File.Exists(assets)) return null;

        var dateien = new List<string>();
        using (var doc = JsonDocument.Parse(File.ReadAllText(assets)))
        {
            var root = doc.RootElement;
            var ordner = root.GetProperty("packageFolders").EnumerateObject().Select(p => p.Name).ToList();
            var bibliotheken = root.GetProperty("libraries");
            foreach (var lib in root.GetProperty("targets").EnumerateObject().First().Value.EnumerateObject())
            {
                if (lib.Value.TryGetProperty("type", out var typ) && typ.GetString() == "project")
                {
                    var pname = lib.Name.Split('/')[0];
                    var ausgabe = Path.Combine(Path.GetDirectoryName(eigene)!, pname + ".dll");
                    if (File.Exists(ausgabe)) dateien.Add(ausgabe);
                    continue;
                }
                if (!lib.Value.TryGetProperty("compile", out var compile)
                    || !bibliotheken.TryGetProperty(lib.Name, out var info) || !info.TryGetProperty("path", out var pfad)) continue;
                foreach (var c in compile.EnumerateObject().Where(c => c.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    if (ordner.Select(o => Path.Combine(o, pfad.GetString()!, c.Name)).FirstOrDefault(File.Exists) is { } f) dateien.Add(f);
            }
        }
        var laufzeit = Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll");
        return dateien.Concat(laufzeit)
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(g => g.First())   // Pakete/Projekte vor Laufzeit
            .Where(IstAssembly)
            .Select(f => (MetadataReference)MetadataReference.CreateFromFile(f)).ToList();
    }

    private static bool IstAssembly(string f)
    {
        try { System.Reflection.AssemblyName.GetAssemblyName(f); return true; }
        catch { return false; }   // native DLLs im Laufzeit-Verzeichnis
    }
}
