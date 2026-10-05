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
    /// Akteur-Dienst: ein Interface, das <c>Abstractions.IAkteurDienst&lt;TAkteur&gt;</c> trägt (z. B. der Classifier-Dienst
    /// des Klassifizierers). Ein Pipeline-Handle nimmt ihn als Parameter wie eine Fähigkeit — dann entscheidet der Handle IM
    /// AUFTRAG von TAkteur (docs/konzept-akteure.md §8). Der Dienst selbst ist kein Akteur.
    /// </summary>
    public static bool IstAkteurDienst(ITypeSymbol typ, Compilation comp) => AkteurVonDienst(typ, comp) != null;

    /// <summary>Der Akteur, dem dieser Dienst gehört (<c>IAkteurDienst&lt;TAkteur&gt;</c> → TAkteur), sonst null.</summary>
    public static INamedTypeSymbol? AkteurVonDienst(ITypeSymbol typ, Compilation comp)
    {
        if (typ is not INamedTypeSymbol { TypeKind: TypeKind.Interface } i) return null;
        var d = comp.GetTypeByMetadataName("Abstractions.IAkteurDienst`1");
        if (d == null) return null;
        return i.AllInterfaces.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, d))
            ?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
    }

    /// <summary>Die Fähigkeits-Parameter ab <paramref name="ab"/>, voll qualifiziert.</summary>
    public static IEnumerable<string> Argumente(IMethodSymbol m, int ab) =>
        m.Parameters.Skip(ab).Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

    /// <summary>„, faehigkeiten.Hole&lt;A&gt;(), faehigkeiten.Hole&lt;B&gt;()" (leer ohne Fähigkeiten).</summary>
    public static string ArgumentListe(IEnumerable<string> typen) =>
        string.Concat(typen.Select(t => $", faehigkeiten.Hole<{t}>()"));
}
