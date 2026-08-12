using Microsoft.CodeAnalysis;

namespace GraphExtractor;

/// <summary>Geteilte Roslyn-Helfer für die Symbol-Analyse über mehrere Compilations.</summary>
public static class Sym
{
    /// <summary>Voll qualifizierter Name ohne <c>global::</c>.</summary>
    public static string Fq(this ISymbol s) =>
        s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    /// <summary>
    /// Implementiert <paramref name="type"/> das Interface <paramref name="iface"/>? String-Vergleich,
    /// weil Symbole aus VERSCHIEDENEN Compilations nie referenzgleich sind (gleiches Muster wie die
    /// Multi-Compilation-Generatoren).
    /// </summary>
    public static bool Implements(ITypeSymbol type, INamedTypeSymbol? iface)
    {
        if (iface == null || type is not INamedTypeSymbol named) return false;
        var target = iface.Fq();
        return named.AllInterfaces.Any(i => i.Fq() == target || i.OriginalDefinition.Fq() == target);
    }

    /// <summary>Alle konkreten (nicht-abstrakten, nicht-Interface) benannten Typen über alle Compilations.</summary>
    public static List<INamedTypeSymbol> AllConcreteTypes(IEnumerable<Compilation> comps)
    {
        var seen = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var comp in comps)
            Collect(comp.GlobalNamespace, seen);
        return seen.Values.ToList();
    }

    private static void Collect(INamespaceSymbol ns, Dictionary<string, INamedTypeSymbol> acc)
    {
        foreach (var t in ns.GetTypeMembers())
            CollectNested(t, acc);
        foreach (var sub in ns.GetNamespaceMembers())
            Collect(sub, acc);
    }

    private static void CollectNested(INamedTypeSymbol t, Dictionary<string, INamedTypeSymbol> acc)
    {
        if (t.TypeKind == TypeKind.Class && !t.IsAbstract)
            acc.TryAdd(t.Fq(), t);
        foreach (var nested in t.GetTypeMembers())
            CollectNested(nested, acc);
    }
}
