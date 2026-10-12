using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimHost.Werkbank;

/// <summary>
/// Ein Compiler-/Analyzer-Befund, zugeordnet zum Code-Fakt (Typ + Member) und darüber zur Karte (<see cref="Karte"/>: stabile Karten-Id,
/// <see cref="Karten"/>), an der der Editor ihn zeigt.
/// </summary>
public sealed record Diagnose(string Datei, int Zeile, int Spalte, string Code, string Text, string Schwere, string? Typ, string? Member, string? Karte = null);

/// <summary>
/// Liest die MSBuild-Befunde (<c>datei(zeile,spalte): error CODE: text [projekt]</c>) und ordnet jeden dem umschließenden Typ und Member
/// zu — über den Syntaxbaum der Datei (Code-Fakt), nie über Namen. Befunde ohne Quell-Ort (die Gesamtbild-Regeln der Laufzeit-Compilation,
/// <c>CSC : error CQRS084: 'Typ' …</c>) tragen den Typ vorn im Text. Die Karte dazu bestimmt <see cref="Karten"/> (stabile Karten-Ids).
/// </summary>
public static partial class Diagnosen
{
    [GeneratedRegex(@"^\s*(?<datei>[^\(\s][^\(]*)\((?<z>\d+),(?<s>\d+)\):\s*(?<schwere>error|warning)\s+(?<code>[A-Z]+\d+):\s*(?<text>.*?)(\s+\[[^\]]+\])?\s*$")]
    private static partial Regex Zeile();

    /// <summary>Ein Befund ohne Quell-Ort (<c>CSC : warning CQRS085: 'Typ' …</c>) — die Gesamtbild-Regeln der Laufzeit-Compilation nennen den Typ vorn.</summary>
    [GeneratedRegex(@"^\s*[^:\(]+?\s*:\s*(?<schwere>error|warning)\s+(?<code>CQRS\d+):\s*(?<text>'(?<typ>[^']+)'.*?)(\s+\[[^\]]+\])?\s*$")]
    private static partial Regex OhneOrt();

    public static IReadOnlyList<Diagnose> Lies(IEnumerable<string> ausgabe, string slnRoot, bool mitWarnungen = false)
    {
        var aus = new List<Diagnose>();
        var gesehen = new HashSet<string>();
        foreach (var z in ausgabe)
        {
            var m = Zeile().Match(z);
            if (!m.Success)
            {
                // Gesamtbild-Regeln (Werkbank P7.4): der Typ steht vorn im Text, die Karte folgt aus ihm.
                var o = OhneOrt().Match(z);
                if (o.Success && (o.Groups["schwere"].Value == "error" || mitWarnungen) && gesehen.Add($"|0|{o.Groups["code"].Value}|{o.Groups["text"].Value}"))
                    aus.Add(new Diagnose("", 0, 0, o.Groups["code"].Value, o.Groups["text"].Value, o.Groups["schwere"].Value, o.Groups["typ"].Value, null));
                continue;
            }
            var schwere = m.Groups["schwere"].Value;
            // Warnungen nur auf Wunsch: die der eigenen Regeln holt die Werkbank vollständig aus den Analyzern (Arbeitsbereich.RegelBefundeAsync) —
            //   ein unverändertes Projekt druckt beim inkrementellen Bauen keine Warnungen mehr.
            if (schwere == "warning" && !mitWarnungen) continue;
            var datei = m.Groups["datei"].Value.Trim();
            var zeile = int.Parse(m.Groups["z"].Value);
            var schluessel = $"{datei}|{zeile}|{m.Groups["code"].Value}|{m.Groups["text"].Value}";
            if (!gesehen.Add(schluessel)) continue;
            var (typ, member) = Ort(datei, zeile);
            var rel = datei.StartsWith(slnRoot, StringComparison.Ordinal) ? Path.GetRelativePath(slnRoot, datei).Replace('\\', '/') : datei;
            aus.Add(new Diagnose(rel, zeile, int.Parse(m.Groups["s"].Value), m.Groups["code"].Value, m.Groups["text"].Value, schwere, typ, member));
        }
        return aus;
    }

    /// <summary>Der innerste Typ (und Member) an einer Zeile — aus dem Syntaxbaum.</summary>
    public static (string? Typ, string? Member) Ort(string datei, int zeile)
    {
        if (!File.Exists(datei) || !datei.EndsWith(".cs", StringComparison.Ordinal)) return (null, null);
        var baum = CSharpSyntaxTree.ParseText(File.ReadAllText(datei));
        var text = baum.GetText();
        if (zeile < 1 || zeile > text.Lines.Count) return (null, null);
        var pos = text.Lines[zeile - 1].Start;
        var knoten = baum.GetRoot().FindToken(pos).Parent;
        var typ = knoten?.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        var member = knoten?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault(x => x is MethodDeclarationSyntax or PropertyDeclarationSyntax);
        var name = member switch
        {
            MethodDeclarationSyntax md => md.Identifier.Text + "(" + string.Join(", ", md.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "")) + ")",
            PropertyDeclarationSyntax pd => pd.Identifier.Text,
            _ => null,
        };
        // Verschachtelte Decider/Applier: der Aggregat-Typ ist der äußere.
        var aussen = typ?.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().LastOrDefault();
        return (aussen?.Identifier.Text ?? typ?.Identifier.Text, name);
    }
}
