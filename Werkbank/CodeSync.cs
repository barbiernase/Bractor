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
/// Die SYNCHRONISATIONS-NAHT zwischen Board (Browser) und echter <c>.cs</c>-Datei. Der Browser schreibt
/// bewusst NUR eine Sache: die <c>// 🤖 Prompt:</c>-Kommentarzeile im Methoden-Rumpf; echten Code editiert
/// der Mensch im richtigen Editor. Die Datei ist die Wahrheit; der Anker wird aus dem Graphen abgeleitet
/// (Typ + Methode), NICHT als Marker in der Datei abgelegt — Roslyn findet die Methode über ihre Signatur.
///
/// Heute nur die Schreibseite (Decider/Applier) — genau das, was der Scaffolder als füllbare Rümpfe
/// (throw-Platzhalter) erzeugt. Leseseite-Rümpfe folgen, sobald der Scaffolder sie in Dateien schreibt.
/// </summary>
public static class CodeSync
{
    private const string PromptMarke = "// 🤖 Prompt:";

    // ════════════════════════════════════════════════════════════════════════════════════════
    //  ALLE Slot-Arten (LLM-Konsole): Anker = Datei + Klasse + Methode (+ Typ des 1. Parameters).
    //  Schreibt den KOMPLETTEN Rumpf (mit „// 🤖 Prompt:“-Zeile) — nur auf ausdrückliches Übernehmen.
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

    /// <summary>
    /// Den Rumpf ersetzen: <paramref name="rumpf"/> = Anweisungen OHNE äußere Klammern (dedentet); davor die Prompt-Zeile.
    /// Ein Ausdrucks-Rumpf („=&gt; …;“) wird zum Block. Sperre: <paramref name="baseHash"/> muss zum Dateistand passen.
    /// Liefert den alten Rumpf-Inhalt zurück (für Rückgängig).
    /// </summary>
    public static (bool Ok, string Grund, string? AlterInhalt, string? NeuerHash, int Zeile) SetzeRumpf(
        MethodenAnker a, string rumpf, string? prompt, string? baseHash, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        if (!t.Ok) return (false, t.Grund, null, null, 0);
        var alt = RumpfInhalt(t);
        if (baseHash != null && Hash(alt) != baseHash) return (false, "Die Datei wurde seit dem Füllen geändert (Hash passt nicht) — neu laden.", null, null, 0);

        var neuText = NeuerText(t, rumpf, prompt);
        File.WriteAllText(t.Pfad, neuText);
        var neu = Finde(a, slnRoot);
        return (true, "", alt, neu.Ok ? Hash(RumpfInhalt(neu)) : null, t.M!.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    /// <summary>
    /// Der Dateitext MIT dem Kandidaten-Rumpf, ohne zu schreiben — für die Prüfung vor dem Übernehmen (dieselbe Einsetzung wie
    /// <see cref="SetzeRumpf"/>, damit geprüft wird, was später in der Datei steht).
    /// </summary>
    public static (bool Ok, string Grund, string? Pfad, string? NeuText) MitRumpf(MethodenAnker a, string rumpf, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        return t.Ok ? (true, "", t.Pfad, NeuerText(t, rumpf, null)) : (false, t.Grund, null, null);
    }

    /// <summary>Dieselbe Einsetzung wie der Rumpf-Edit der Werkbank (<see cref="GraphExtractor.Rumpf"/>) — eine Regel für ✎ und 🤖.</summary>
    private static string NeuerText(MethodenTreffer t, string rumpf, string? prompt) => GraphExtractor.Rumpf.MitMethodenRumpf(t.Text, t.M!, rumpf, prompt);

    /// <summary>Rückgängig: den gesicherten alten Rumpf-Inhalt wörtlich zurückschreiben (nur wenn seitdem unverändert).</summary>
    public static (bool Ok, string Grund) SetzeInhaltZurueck(MethodenAnker a, string alterInhalt, string erwarteterHash, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        if (!t.Ok) return (false, t.Grund);
        if (Hash(RumpfInhalt(t)) != erwarteterHash) return (false, "Die Datei wurde nach dem Übernehmen weiter geändert — Rückgängig verweigert.");
        var nl = t.Text.Contains("\r\n") ? "\r\n" : "\n";
        var b = t.M!.Body!;   // nach dem Übernehmen ist es immer ein Block
        string neuText;
        if (alterInhalt.StartsWith("=> "))
        {
            // war Ausdrucks-Rumpf: Block wieder durch „=> …;“ ersetzen
            var vorher = t.Text[..b.Span.Start].TrimEnd();
            neuText = vorher + " " + alterInhalt + t.Text[b.Span.End..];
        }
        else
            neuText = t.Text[..b.OpenBraceToken.Span.End] + alterInhalt.Replace("\n", nl) + t.Text[b.CloseBraceToken.Span.Start..];
        File.WriteAllText(t.Pfad, neuText);
        return (true, "");
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
        var psi = new ProcessStartInfo("dotnet", $"build \"{proj}\" -v q --nologo")
        {
            WorkingDirectory = slnRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        var fehler = (o.Result + "\n" + e.Result).Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Contains(": error")).Select(l => l.Trim()).Distinct().Take(50).ToList();
        return new { ok = p.ExitCode == 0, projekt = Rel(proj, slnRoot), fehler };
    }

    // ── Bauen: die Generatoren + Proto-Prepass laufen bei dotnet build automatisch mit (siehe Domain.Leitung.csproj). ──
    public static object Baue(string slnRoot)
    {
        var psi = new ProcessStartInfo("dotnet", "build -v q --nologo")
        {
            WorkingDirectory = slnRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var aus = p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd();
        p.WaitForExit();
        var fehler = aus.Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Contains(": error"))
            .Select(l => l.Trim()).Distinct().Take(50).ToList();
        return new { ok = p.ExitCode == 0, fehler };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    //  FLUSS-LAMBDAS: Anker = Klasse + Anweisung + Lambda-Index. Der „Rumpf“ ist der ganze Lambda-Ausdruck.
    // ════════════════════════════════════════════════════════════════════════════════════════

    private sealed record LambdaTreffer(bool Ok, string Pfad, string Text, LambdaExpressionSyntax? L, string Grund);

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
            ? new(true, pfad, text, lambdas[a.Lambda], "")
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

    /// <summary>Den Lambda-Ausdruck ersetzen (wörtlich). Sperre wie bei Methoden: <paramref name="baseHash"/> muss zum Dateistand passen.</summary>
    public static (bool Ok, string Grund, string? AlterInhalt, string? NeuerHash, int Zeile) SetzeLambda(
        LambdaAnker a, string ausdruck, string? baseHash, string slnRoot)
    {
        var t = FindeLambda(a, slnRoot);
        if (!t.Ok) return (false, t.Grund, null, null, 0);
        var alt = t.L!.ToString();
        if (baseHash != null && Hash(alt.Replace("\r\n", "\n")) != baseHash)
            return (false, "Die Datei wurde seit dem Füllen geändert (Hash passt nicht) — neu laden.", null, null, 0);
        File.WriteAllText(t.Pfad, t.Text[..t.L.Span.Start] + ausdruck.Trim() + t.Text[t.L.Span.End..]);
        var neu = FindeLambda(a, slnRoot);
        return (true, "", alt, neu.Ok ? Hash(neu.L!.ToString().Replace("\r\n", "\n")) : null, t.L.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    /// <summary>Der Dateitext MIT dem Kandidaten-Lambda, ohne zu schreiben — für die Prüfung vor dem Übernehmen (dieselbe Einsetzung wie <see cref="SetzeLambda"/>).</summary>
    public static (bool Ok, string Grund, string? Pfad, string? NeuText) MitLambda(LambdaAnker a, string ausdruck, string slnRoot)
    {
        var t = FindeLambda(a, slnRoot);
        return t.Ok ? (true, "", t.Pfad, t.Text[..t.L!.Span.Start] + ausdruck.Trim() + t.Text[t.L.Span.End..]) : (false, t.Grund, null, null);
    }

    /// <summary>Rückgängig: den alten Lambda wörtlich zurück (nur wenn seitdem unverändert).</summary>
    public static (bool Ok, string Grund) SetzeLambdaZurueck(LambdaAnker a, string alterInhalt, string erwarteterHash, string slnRoot)
    {
        var t = FindeLambda(a, slnRoot);
        if (!t.Ok) return (false, t.Grund);
        if (Hash(t.L!.ToString().Replace("\r\n", "\n")) != erwarteterHash) return (false, "Die Datei wurde nach dem Übernehmen weiter geändert — Rückgängig verweigert.");
        File.WriteAllText(t.Pfad, t.Text[..t.L.Span.Start] + alterInhalt + t.Text[t.L.Span.End..]);
        return (true, "");
    }

    private static string? LiesPrompt(string inner)
    {
        var l = inner.Split('\n').FirstOrDefault(x => x.TrimStart().StartsWith(PromptMarke));
        return l?.TrimStart()[PromptMarke.Length..].Trim();
    }

    private static int OffeneSpalte(MethodDeclarationSyntax m) =>
        m.Body!.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Character;

    private static string Einzug(string zeile) => zeile[..(zeile.Length - zeile.TrimStart().Length)];
    private static string EinZeile(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();
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
