using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Domain.SourceGeneration;

/// <summary>
/// Gemeinsamer Symbol-Fakt der Dispatch-Generatoren: eine FÄHIGKEIT ist ein Interface, das den Marker
/// <c>Abstractions.IWriteStore</c> oder <c>Abstractions.IReadStore</c> trägt. Ein Handle nimmt 0…n davon
/// als Parameter hinter dem Framework-Kontext; der generierte Dispatch löst sie über
/// <c>IFaehigkeiten.Hole&lt;T&gt;()</c> auf — keine Reflection, die Typen stehen im Generat.
/// </summary>
internal static class FaehigkeitsTypen
{
    public static bool IstFaehigkeit(ITypeSymbol typ, Compilation comp)
    {
        if (typ is not INamedTypeSymbol { TypeKind: TypeKind.Interface } i) return false;
        var w = comp.GetTypeByMetadataName("Abstractions.IWriteStore");
        var r = comp.GetTypeByMetadataName("Abstractions.IReadStore");
        return i.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, w) || SymbolEqualityComparer.Default.Equals(x, r));
    }

    /// <summary>
    /// Akteur-Dienst: ein Interface, das <c>Abstractions.IAkteur</c> trägt (z. B. die KI). Ein Pipeline-Handle nimmt ihn als
    /// Parameter wie eine Fähigkeit — dann entscheidet der Handle IM AUFTRAG dieses Akteurs (docs/konzept-akteure.md).
    /// </summary>
    public static bool IstAkteurDienst(ITypeSymbol typ, Compilation comp)
    {
        if (typ is not INamedTypeSymbol { TypeKind: TypeKind.Interface } i) return false;
        var a = comp.GetTypeByMetadataName("Abstractions.IAkteur");
        return a != null && i.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, a));
    }

    /// <summary>Die Fähigkeits-Parameter ab <paramref name="ab"/>, voll qualifiziert.</summary>
    public static IEnumerable<string> Argumente(IMethodSymbol m, int ab) =>
        m.Parameters.Skip(ab).Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

    /// <summary>„, faehigkeiten.Hole&lt;A&gt;(), faehigkeiten.Hole&lt;B&gt;()" (leer ohne Fähigkeiten).</summary>
    public static string ArgumentListe(IEnumerable<string> typen) =>
        string.Concat(typen.Select(t => $", faehigkeiten.Hole<{t}>()"));
}
