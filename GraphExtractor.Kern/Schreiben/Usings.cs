using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>
/// Fehlende <c>using</c>-Direktiven in eine bestehende Datei EINSORTIEREN statt sie voranzustellen (Werkbank P7.5-Abnahme): <c>System…</c> zuerst,
/// sonst ordinal — vor die erste vorhandene Direktive, die danach kommt, bzw. hinter die letzte. Ohne vorhandene Direktiven stehen die neuen
/// sortiert am Anfang, gefolgt von einer Leerzeile. Was schon da ist (auch innerhalb eines Namespace), bleibt; Alias-, <c>static</c>- und
/// <c>global</c>-Direktiven werden nicht umsortiert.
/// </summary>
public static class Usings
{
    /// <summary>Ergänzt die Namespaces <paramref name="namespaces"/> (z. B. <c>Domain.Datensatz</c>), die der Datei fehlen.</summary>
    public static string Ergaenze(string text, IEnumerable<string> namespaces)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        var da = root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>()
            .Where(Schlicht).Select(u => u.Name!.ToString()).ToHashSet(StringComparer.Ordinal);
        var neu = namespaces.Select(n => n.Trim()).Where(n => n.Length > 0 && !da.Contains(n)).Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, Reihenfolge.Instanz).ToList();
        if (neu.Count == 0) return text;
        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        var oben = root.Usings.Where(Schlicht).ToList();
        if (oben.Count == 0) return string.Concat(neu.Select(n => $"using {n};{nl}")) + nl + text;

        // Von hinten nach vorn einfügen, damit die Positionen der vorderen Direktiven stimmen.
        var einfuegen = new List<(int Pos, string Text)>();
        foreach (var n in neu)
        {
            var danach = oben.FirstOrDefault(u => Reihenfolge.Instanz.Compare(n, u.Name!.ToString()) < 0);
            var pos = danach != null ? Zeilenanfang(text, danach.SpanStart) : Zeilenende(text, oben[^1].Span.End);
            einfuegen.Add(pos < 0 ? (text.Length, $"{nl}using {n};") : (pos, $"using {n};{nl}"));
        }
        foreach (var g in einfuegen.GroupBy(e => e.Pos).OrderByDescending(g => g.Key))
            text = text[..g.Key] + string.Concat(g.Select(e => e.Text)) + text[g.Key..];
        return text;
    }

    /// <summary>Ergänzt die <c>using</c>-Direktiven einer generierten Datei (<paramref name="genRoot"/>), die der bestehenden fehlen.</summary>
    public static string ErgaenzeAus(string text, Microsoft.CodeAnalysis.SyntaxNode genRoot) =>
        Ergaenze(text, genRoot.DescendantNodes(n => n is CompilationUnitSyntax).OfType<UsingDirectiveSyntax>().Where(Schlicht).Select(u => u.Name!.ToString()));

    private static bool Schlicht(UsingDirectiveSyntax u) =>
        u.Alias == null && u.StaticKeyword.RawKind == 0 && u.GlobalKeyword.RawKind == 0 && u.Name != null;

    private static int Zeilenanfang(string text, int pos)
    {
        var i = text.LastIndexOf('\n', Math.Max(0, pos - 1));
        return i < 0 ? 0 : i + 1;
    }
    private static int Zeilenende(string text, int pos)
    {
        var i = text.IndexOf('\n', pos);
        return i < 0 ? -1 : i + 1;   // -1: die letzte Direktive steht in der letzten Zeile
    }

    /// <summary>System zuerst (wie <c>dotnet_sort_system_directives_first</c>), sonst ordinal.</summary>
    private sealed class Reihenfolge : IComparer<string>
    {
        public static readonly Reihenfolge Instanz = new();
        private static bool IstSystem(string n) => n == "System" || n.StartsWith("System.", StringComparison.Ordinal);
        public int Compare(string? a, string? b)
        {
            if (IstSystem(a!) != IstSystem(b!)) return IstSystem(a!) ? -1 : 1;
            return string.CompareOrdinal(a, b);
        }
    }
}
