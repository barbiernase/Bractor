using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Domain.SourceGeneration;

/// <summary>Gemeinsame Symbol-Helfer der Modell-Analyzer.</summary>
internal static class Typen
{
    /// <summary>Die Fälle einer Ausgabe-Signatur: <c>OneOf&lt;A, B&gt;</c> → A, B; <c>Task&lt;…&gt;</c>/<c>IEnumerable&lt;…&gt;</c> abgeschält; sonst der Typ selbst.</summary>
    public static IEnumerable<ITypeSymbol> Faelle(ITypeSymbol t)
    {
        while (t is INamedTypeSymbol { TypeArguments.Length: 1 } h
               && (h.Name == "Task" || h.Name == "ValueTask" || h.Name == "IEnumerable" || h.Name == "IAsyncEnumerable"))
            t = h.TypeArguments[0];
        if (t is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions") return o.TypeArguments;
        if (t.SpecialType == SpecialType.System_Void || t is INamedTypeSymbol { Name: "Task", TypeArguments.Length: 0 }) return Enumerable.Empty<ITypeSymbol>();
        return new[] { t };
    }

    /// <summary>Alle Typen der eigenen Assembly und jeder referenzierten, die den Vertrag (Abstractions) nutzt — ohne BCL-Metadaten.</summary>
    public static IEnumerable<INamedTypeSymbol> Vertragsnutzer(Compilation c)
    {
        var asms = new List<IAssemblySymbol> { c.Assembly };
        asms.AddRange(c.SourceModule.ReferencedAssemblySymbols.Where(a =>
            a.Name != "Abstractions" && a.Modules.Any(m => m.ReferencedAssemblySymbols.Any(r => r.Name == "Abstractions"))));
        foreach (var a in asms)
            foreach (var t in Alle(a.GlobalNamespace))
                yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> Alle(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in Alle(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }
}
