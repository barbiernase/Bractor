using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Werkbank;

/// <summary>Anker eines füllbaren Rumpfs — aus dem Modell (das der Extractor aus dem Code liest), nicht aus einer Konvention.</summary>
/// <param name="Kind">"decider" | "applier" (nur für Meldungen).</param>
/// <param name="Namespace">Aggregat-Namespace (nur für Meldungen).</param>
/// <param name="Disc">Diskriminator: Typ des ersten Parameters (Command bzw. Event) — identifiziert die Methode.</param>
/// <param name="Datei">Die echte Quelldatei relativ zur Solution (aus dem Modell); null = noch nicht geschrieben.</param>

/// <summary>Anker für ALLE Slot-Arten (LLM-Konsole): Datei relativ zur Solution, Klasse, Methode, Typ des 1. Parameters (null = parameterlos).</summary>
public sealed record MethodenAnker(string Datei, string Klasse, string Methode, string? ParameterTyp);

/// <summary>
/// Anker eines Fluss-Lambdas (docs/konzept-editor-pipelines.md §14.4): Datei, Pipeline-Klasse, die wievielte Anweisung des
/// Definiere-Lambdas und der wievielte Lambda darin (Code-Reihenfolge; verschachtelte zählen nicht) — wie der Extractor sie zählt.
/// </summary>
public sealed record LambdaAnker(string Datei, string Klasse, int Anweisung, int Lambda);

/// <summary>
/// Die LLM-Slots (<c>GraphExtractor --kontexte</c>) im Code finden: Anker (Datei + Klasse + Methode bzw. Anweisung + Lambda) → aktueller Rumpf,
/// Hash (Sperre gegen Zwischenänderungen) und <see cref="GraphExtractor.RumpfOrt"/>. Geschrieben wird hier NICHTS — eine 🤖-Übernahme ist eine
/// Rumpf-Geste (<see cref="GraphExtractor.Rumpf"/>, Verlauf und ↶ der Werkbank), derselbe Weg wie ✎ (docs/13 W11-1). Dazu der Build des
/// Laufzeit-Projekts nach einer Übernahme.
/// </summary>
public static class CodeSync
{
    private const string PromptMarke = "// 🤖 Prompt:";

    // ════════════════════════════════════════════════════════════════════════════════════════
    //  ALLE Slot-Arten (LLM-Konsole): Anker = Datei + Klasse + Methode (+ Typ des 1. Parameters).
    // ════════════════════════════════════════════════════════════════════════════════════════

    private sealed record MethodenTreffer(bool Ok, string Pfad, string Text, MethodDeclarationSyntax? M, string Grund);

    private static MethodenTreffer Finde(MethodenAnker a, string slnRoot)
    {
        var pfad = Path.GetFullPath(Path.Combine(slnRoot, a.Datei));
        if (!pfad.StartsWith(Path.GetFullPath(slnRoot), StringComparison.Ordinal)) return new(false, pfad, "", null, "Pfad liegt außerhalb der Solution.");
        if (!File.Exists(pfad)) return new(false, pfad, "", null, $"Datei fehlt: {a.Datei}");
        var text = File.ReadAllText(pfad);
        var kandidaten = CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == a.Methode
                        && m.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text == a.Klasse
                        && (a.ParameterTyp == null
                            ? m.ParameterList.Parameters.Count == 0
                            : m.ParameterList.Parameters.Count > 0 && Basisname(m.ParameterList.Parameters[0].Type?.ToString()) == a.ParameterTyp))
            .ToList();
        if (kandidaten.Count != 1)
            return new(false, pfad, text, null, kandidaten.Count == 0 ? $"{a.Klasse}.{a.Methode}({a.ParameterTyp}) nicht gefunden in {a.Datei}." : $"{a.Klasse}.{a.Methode}({a.ParameterTyp}) ist mehrdeutig in {a.Datei}.");
        var treffer = kandidaten[0];
        if (treffer.Body is null && treffer.ExpressionBody is null) return new(false, pfad, text, null, "Methode ohne Rumpf.");
        return new(true, pfad, text, treffer, "");
    }

    private static string Basisname(string? typ) => (typ ?? "").Split('<')[0].Split('.').Last().TrimEnd('?').Trim();

    /// <summary>Der Rumpf als Text: bei Block der Inhalt zwischen den Klammern, bei Ausdruck „=&gt; …;“.</summary>
    private static string RumpfInhalt(MethodenTreffer t) => t.M!.Body is { } b
        ? t.Text[b.OpenBraceToken.Span.End..b.CloseBraceToken.Span.Start].Replace("\r\n", "\n")
        : "=> " + t.M.ExpressionBody!.Expression + ";";

    /// <summary>Aktueller Rumpf (dedentet, OHNE Prompt-Zeile) + Prompt + Hash — für Anzeige und optimistische Sperre.</summary>
    public static object LeseRumpf(MethodenAnker a, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        if (!t.Ok) return new { ok = false, grund = t.Grund };
        var inner = RumpfInhalt(t);
        var ohnePrompt = string.Join("\n", inner.Split('\n').Where(l => !l.TrimStart().StartsWith(PromptMarke)));
        return new { ok = true, pfad = Rel(t.Pfad, slnRoot), zeile = t.M!.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            body = Dedent(ohnePrompt), prompt = LiesPrompt(inner), hash = Hash(inner) };
    }

    public static string? RumpfHash(MethodenAnker a, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        return t.Ok ? Hash(RumpfInhalt(t)) : null;
    }

    /// <summary>Der Ort der Methode in der Board-Form (<c>_ort</c>) — darüber setzt und schreibt die Rumpf-Geste (✎ wie 🤖).</summary>
    public static (GraphExtractor.RumpfOrt? Ort, string Grund, int Zeile) Ort(MethodenAnker a, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        return t.Ok ? (new GraphExtractor.RumpfOrt(Rel(t.Pfad, slnRoot), t.M!.Span.Start, t.M.Span.End), "", t.M.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
            : (null, t.Grund, 0);
    }


    /// <summary>Nur das Projekt bauen, zu dem die Datei gehört (nächste .csproj aufwärts) — schneller als die ganze Solution.</summary>
    public static object BaueProjektVon(string relDatei, string slnRoot)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(slnRoot, relDatei)));
        string? proj = null;
        while (dir != null && dir.StartsWith(slnRoot, StringComparison.Ordinal) && proj == null)
        {
            proj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
            dir = Path.GetDirectoryName(dir);
        }
        if (proj == null) return new { ok = false, projekt = (string?)null, fehler = new List<string> { "Kein .csproj über der Datei gefunden." } };
        return BaueProjekt(Rel(proj, slnRoot), slnRoot);
    }

    /// <summary>Ein Projekt (relativ zur Solution) bauen — die Roslyn-Generatoren und MSBuild-Prepasses laufen mit.</summary>
    public static object BaueProjekt(string relProjekt, string slnRoot)
    {
        var proj = Path.GetFullPath(Path.Combine(slnRoot, relProjekt));
        if (!File.Exists(proj)) return new { ok = false, projekt = (string?)relProjekt, fehler = new List<string> { $"Projekt fehlt: {relProjekt}" } };
        // Derselbe Prozess-Weg wie ⟳ (wartet auf das Prozessende, nicht auf die von MSBuild-Knoten geerbte Pipe).
        var r = Werkbank.Server.Prozesse.LaufeAsync("dotnet", ["build", proj, "-v", "q", "--nologo"], slnRoot).GetAwaiter().GetResult();
        var fehler = r.Zeilen.Where(l => l.Contains(": error")).Select(l => l.Trim()).Distinct().Take(50).ToList();
        return new { ok = r.Ok, projekt = Rel(proj, slnRoot), fehler };
    }

    // ── Bauen: die Generatoren + Proto-Prepass laufen bei dotnet build automatisch mit (siehe Domain.Leitung.csproj). ──

    // ════════════════════════════════════════════════════════════════════════════════════════
    //  FLUSS-LAMBDAS: Anker = Klasse + Anweisung + Lambda-Index. Der „Rumpf“ ist der ganze Lambda-Ausdruck.
    // ════════════════════════════════════════════════════════════════════════════════════════

    private sealed record LambdaTreffer(bool Ok, string Pfad, string Text, LambdaExpressionSyntax? L, string Grund, Microsoft.CodeAnalysis.SyntaxNode? Anweisung = null);

    private static LambdaTreffer FindeLambda(LambdaAnker a, string slnRoot)
    {
        var pfad = Path.GetFullPath(Path.Combine(slnRoot, a.Datei));
        if (!pfad.StartsWith(Path.GetFullPath(slnRoot), StringComparison.Ordinal)) return new(false, pfad, "", null, "Pfad liegt außerhalb der Solution.");
        if (!File.Exists(pfad)) return new(false, pfad, "", null, $"Datei fehlt: {a.Datei}");
        var text = File.ReadAllText(pfad);
        var klasse = CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == a.Klasse);
        var definiere = klasse?.Members.OfType<PropertyDeclarationSyntax>()
            .SelectMany(p => p.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Select(i => i.ArgumentList.Arguments.FirstOrDefault()?.Expression as LambdaExpressionSyntax)
            .FirstOrDefault(l => l != null);
        if (definiere == null) return new(false, pfad, text, null, $"{a.Klasse}: kein Definiere-Lambda gefunden.");
        // Block = je Anweisung eine Regel/ein Knoten; Ausdrucks-Lambda (eine einzige Regel) = Anweisung 0.
        var anweisungen = definiere.Body is BlockSyntax block ? block.Statements.Cast<Microsoft.CodeAnalysis.SyntaxNode>().ToList() : [definiere.Body];
        if (a.Anweisung < 0 || a.Anweisung >= anweisungen.Count) return new(false, pfad, text, null, $"{a.Klasse}: Anweisung {a.Anweisung} fehlt.");
        var st = anweisungen[a.Anweisung];
        var lambdas = st.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>()
            .Where(l => l.Parent is ArgumentSyntax && !l.Ancestors().TakeWhile(x => x != st).OfType<LambdaExpressionSyntax>().Any()).ToList();
        return a.Lambda >= 0 && a.Lambda < lambdas.Count
            ? new(true, pfad, text, lambdas[a.Lambda], "", st)
            : new(false, pfad, text, null, $"{a.Klasse}: Anweisung {a.Anweisung} hat keinen Lambda {a.Lambda}.");
    }

    public static object LeseLambda(LambdaAnker a, string slnRoot)
    {
        var t = FindeLambda(a, slnRoot);
        if (!t.Ok) return new { ok = false, grund = t.Grund };
        var inhalt = t.L!.ToString().Replace("\r\n", "\n");
        return new { ok = true, pfad = Rel(t.Pfad, slnRoot), zeile = t.L.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            body = Dedent(inhalt), prompt = (string?)null, hash = Hash(inhalt) };
    }

    public static string? LambdaHash(LambdaAnker a, string slnRoot)
    {
        var t = FindeLambda(a, slnRoot);
        return t.Ok ? Hash(t.L!.ToString().Replace("\r\n", "\n")) : null;
    }

    /// <summary>Der Ort des Lambdas in der Board-Form (Spanne der Anweisung + Lambda-Index, wie <c>_ort</c> eines Fluss-Knotens).</summary>
    public static (GraphExtractor.RumpfOrt? Ort, string Grund, int Zeile) Ort(LambdaAnker a, string slnRoot)
    {
        var t = FindeLambda(a, slnRoot);
        return t.Ok ? (new GraphExtractor.RumpfOrt(Rel(t.Pfad, slnRoot), t.Anweisung!.Span.Start, t.Anweisung.Span.End, a.Lambda), "",
                t.L!.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
            : (null, t.Grund, 0);
    }


    private static string? LiesPrompt(string inner)
    {
        var l = inner.Split('\n').FirstOrDefault(x => x.TrimStart().StartsWith(PromptMarke));
        return l?.TrimStart()[PromptMarke.Length..].Trim();
    }


    private static string Rel(string pfad, string root) => Path.GetRelativePath(root, pfad).Replace('\\', '/');

    // Gemeinsamen Mindest-Einzug abziehen → hübsche Vorschau im Board.
    private static string Dedent(string s)
    {
        var zeilen = s.Split('\n');
        var min = zeilen.Where(z => z.Trim().Length > 0).Select(z => z.Length - z.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select(z => z.Length >= min ? z[min..] : z)).Trim('\n');
    }

    // Hash über den INHALT (dedentet, getrimmt) → stabil gegen reine Whitespace-Unterschiede.
    private static string Hash(string inner)
    {
        var norm = Dedent(inner).Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(norm)))[..12];
    }
}
