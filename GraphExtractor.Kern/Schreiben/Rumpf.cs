using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GraphExtractor;

/// <summary>
/// Wo ein Rumpf im Code steht (Board-Feld <c>_rumpf</c>): die Methode, deren Rumpf er ist (Datei relativ zur Solution + Spanne der Deklaration),
/// bzw. die Fluss-/Regel-Anweisung samt Index ihres Lambdas (<paramref name="Lambda"/>: der i-te Lambda, der direkt als Argument der Anweisung
/// steht — dieselbe Zählung wie die LLM-Slots).
/// </summary>
public sealed record RumpfOrt(string Datei, int Start, int Ende, int? Lambda = null);

/// <summary>
/// Rumpf-Edit (Werkbank P7.4, Zielbild §8.4): der Rumpf-Port zeigt den Code direkt aus der Datei, ✎ ersetzt GENAU diesen Bereich — den Rumpf einer
/// Methode (zwischen den Klammern; ein Ausdrucks-Rumpf wird ein Block) bzw. einen Lambda einer Fluss-/Regel-Anweisung. Alles andere bleibt Wort für
/// Wort (Anhang A.2). Sperre: der Bereich muss noch so aussehen wie das, was der Editor zeigte (sonst „neu laden“). Dieselbe Einsetzung nutzt die
/// LLM-Übernahme (Prompt-Zeile <see cref="PromptMarke"/>), damit geprüft wird, was später in der Datei steht.
/// </summary>
public static class Rumpf
{
    public const string PromptMarke = "// 🤖 Prompt:";

    /// <summary>Den Rumpf im Puffer ersetzen — wirft mit lesbarem Grund, wenn der Ort nicht (mehr) passt.</summary>
    public static void Ersetze(Dateipuffer puffer, RumpfOrt ort, string neu, string? alt)
    {
        var text = puffer.Lies(ort.Datei) ?? throw new InvalidOperationException($"Datei fehlt: {ort.Datei}");
        if (alt != null && Lies(text, ort) is { } jetzt && Norm(jetzt) != Norm(alt))
            throw new InvalidOperationException($"Der Rumpf in {ort.Datei} wurde inzwischen geändert — neu laden.");
        // Eine Prompt-Zeile im bearbeiteten Text bleibt (sie gehört zum 🤖-Verlauf dieses Rumpfs).
        var prompt = neu.Replace("\r\n", "\n").Split('\n').Select(z => z.Trim()).FirstOrDefault(z => z.StartsWith(PromptMarke, StringComparison.Ordinal))?[PromptMarke.Length..].Trim();
        puffer.Setze(ort.Datei, MitRumpf(text, ort, neu, prompt));
    }

    /// <summary>Der aktuelle Rumpf am Ort (dedentet, ohne Prompt-Zeile) — null, wenn der Ort nicht passt.</summary>
    public static string? Lies(string text, RumpfOrt ort)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        if (ort.Lambda is { } i) return LambdaAm(root, ort, i)?.ToString().Replace("\r\n", "\n");
        if (MethodeAm(root, ort) is not { } m) return null;
        var inner = m.Body is { } b ? text[b.OpenBraceToken.Span.End..b.CloseBraceToken.Span.Start] : m.ExpressionBody?.Expression.ToString() ?? "";
        return Dedent(string.Join("\n", inner.Replace("\r\n", "\n").Split('\n').Where(z => !z.TrimStart().StartsWith(PromptMarke, StringComparison.Ordinal)))).Trim();
    }

    /// <summary>Der Dateitext mit dem neuen Rumpf am Ort (ohne zu schreiben); <paramref name="prompt"/> = optionale Prompt-Zeile (LLM).</summary>
    public static string MitRumpf(string text, RumpfOrt ort, string neu, string? prompt)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        if (ort.Lambda is { } i)
        {
            var l = LambdaAm(root, ort, i) ?? throw new InvalidOperationException($"{ort.Datei}: an dieser Stelle steht kein Lambda {i} (mehr) — neu laden.");
            return text[..l.Span.Start] + neu.Trim() + text[l.Span.End..];
        }
        var m = MethodeAm(root, ort) ?? throw new InvalidOperationException($"{ort.Datei}: an dieser Stelle steht keine Methode (mehr) — neu laden.");
        return MitMethodenRumpf(text, m, neu, prompt);
    }

    /// <summary>Den Rumpf einer Methode ersetzen: Inhalt eingerückt unter der Methode, optional mit Prompt-Zeile; „=&gt; …;“ wird ein Block.</summary>
    public static string MitMethodenRumpf(string text, MethodDeclarationSyntax m, string neu, string? prompt)
    {
        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        var spalte = m.GetLocation().GetLineSpan().StartLinePosition.Character;
        var einzug = new string(' ', spalte + 4);
        // Eine mitgelieferte Prompt-Zeile (z. B. aus dem aktuellen Rumpf einer Anpassung) fliegt raus — es gibt genau EINE.
        var zeilen = Dedent(neu.Replace("\r\n", "\n")).Split('\n').Where(z => !z.TrimStart().StartsWith(PromptMarke, StringComparison.Ordinal)).ToList();
        if (!string.IsNullOrWhiteSpace(prompt)) zeilen.Insert(0, PromptMarke + " " + prompt.Replace("\r", " ").Replace("\n", " ").Trim());
        var inhalt = nl + string.Join(nl, zeilen.Select(z => z.Trim().Length == 0 ? "" : einzug + z)) + nl + new string(' ', spalte);
        if (m.Body is { } b) return text[..b.OpenBraceToken.Span.End] + inhalt + text[b.CloseBraceToken.Span.Start..];
        var start = m.ExpressionBody!.Span.Start;
        var ende = m.SemicolonToken.Span.End;
        return text[..start].TrimEnd(' ') + nl + new string(' ', spalte) + "{" + inhalt + "}" + text[ende..];
    }

    private static MethodDeclarationSyntax? MethodeAm(SyntaxNode root, RumpfOrt ort) =>
        Am(root, ort) is { } n ? n.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Span.Start == ort.Start && m.Span.End == ort.Ende)
            : null;

    /// <summary>Der i-te Lambda, der direkt als Argument der Anweisung steht (nicht in einem anderen Lambda).</summary>
    private static LambdaExpressionSyntax? LambdaAm(SyntaxNode root, RumpfOrt ort, int i)
    {
        var st = Am(root, ort)?.AncestorsAndSelf().FirstOrDefault(n => n.Span.Start == ort.Start && n.Span.End == ort.Ende);
        if (st == null) return null;
        var lambdas = st.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>()
            .Where(l => l.Parent is ArgumentSyntax && !l.Ancestors().TakeWhile(x => x != st).OfType<LambdaExpressionSyntax>().Any()).ToList();
        return i >= 0 && i < lambdas.Count ? lambdas[i] : null;
    }

    private static SyntaxNode? Am(SyntaxNode root, RumpfOrt ort) =>
        ort.Start >= 0 && ort.Ende <= root.FullSpan.End && ort.Ende > ort.Start ? root.FindNode(TextSpan.FromBounds(ort.Start, ort.Ende)) : null;

    /// <summary>Vergleichsform: ohne Prompt-Zeile, Leerraum zusammengefasst.</summary>
    private static string Norm(string s) => Regex.Replace(string.Join("\n", s.Replace("\r\n", "\n").Split('\n')
        .Where(z => !z.TrimStart().StartsWith(PromptMarke, StringComparison.Ordinal))), @"\s+", " ").Trim();

    private static string Dedent(string s)
    {
        var zeilen = s.Split('\n');
        var min = zeilen.Where(z => z.Trim().Length > 0).Select(z => z.Length - z.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select(z => z.Length >= min ? z[min..] : z)).Trim('\n');
    }
}
