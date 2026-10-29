// In Projekt: Infrastructure.SourceGeneration
// Dateiname: Ctorbindung.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Was der Konstruktor einer Funktions- oder Dienst-Implementierung nimmt (docs/06 §6.5) — die EINE Lesart für <c>FunktionsGenerator</c> und
    /// <c>DienstGenerator</c>: Dienste (<c>IDienst</c>), Konfig-Records (Werte aus <c>Konfig:&lt;Record&gt;</c>) und <c>ILogger&lt;T&gt;</c>.
    /// Gebaut wird die Implementierung per generierter Fabrik (<c>sp =&gt; new Impl(sp.GetRequiredService&lt;P&gt;(), …)</c>) — ohne
    /// reflektierten Aktivator. Was nicht passt, meldet der Analyzer (CQRS090); hier wird es trotzdem aus dem Container geholt.
    /// </summary>
    internal static class Ctorbindung
    {
        /// <summary>Was ein Konstruktor-Parameter ist; <see cref="Zustand"/> nur in einer Funktions-Klasse (<c>IZustand&lt;T&gt;</c>, docs/04 §4.4).</summary>
        public enum Art { Dienst, Konfig, Logger, Zustand, Fremd }

        /// <summary>Der Konstruktor, den die Fabrik benutzt: der öffentliche mit den meisten Parametern (wie der DI-Aktivator).</summary>
        public static IMethodSymbol? Konstruktor(INamedTypeSymbol t) => t.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();

        public static Art ArtVon(ITypeSymbol p, INamedTypeSymbol? iDienst)
        {
            if (iDienst != null && p is INamedTypeSymbol { TypeKind: TypeKind.Interface } i
                && i.AllInterfaces.Contains(iDienst, SymbolEqualityComparer.Default)) return Art.Dienst;
            if (p is INamedTypeSymbol { IsGenericType: true } g && g.OriginalDefinition.ToDisplayString() == "Microsoft.Extensions.Logging.ILogger<TCategoryName>")
                return Art.Logger;
            if (p is INamedTypeSymbol { IsGenericType: true } z && (z.OriginalDefinition.ContainingNamespace?.ToDisplayString() + "." + z.OriginalDefinition.MetadataName)
                == Abstractions.Funktionsvertrag.Zustand)
                return Art.Zustand;
            if (p is INamedTypeSymbol { TypeKind: TypeKind.Class, IsRecord: true, IsAbstract: false } r && Vertragsassemblies.NutztVertrag(r.ContainingAssembly)) return Art.Konfig;
            return Art.Fremd;
        }

        /// <summary>Ein Feld-Typ eines Konfig-Records, den die generierte Bindung lesen kann (<c>Abstractions.Dienstvertrag.Feldtypen</c>, Enum, string[]).</summary>
        public static bool FeldBindbar(ITypeSymbol t) =>
            t.TypeKind == TypeKind.Enum
            || t is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }
            || Abstractions.Dienstvertrag.IstFeldtyp((t.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.MetadataName);

        /// <summary>
        /// Die Argumente der Fabrik: je Parameter <c>sp.GetRequiredService&lt;T&gt;()</c> — ein Zustand (<c>IZustand&lt;T&gt;</c>) kommt aus der
        /// Aktivierung (<paramref name="zustand"/>, der Name der Variablen in der Fabrik), nie aus dem Container.
        /// </summary>
        public static string Argumente(IMethodSymbol? ctor, SymbolDisplayFormat fq, string? zustand = null) => ctor == null ? ""
            : string.Join(", ", ctor.Parameters.Select(p => zustand != null && ArtVon(p.Type, null) == Art.Zustand
                ? $"({p.Type.ToDisplayString(fq)}){zustand}!"
                : $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{p.Type.ToDisplayString(fq)}>(sp)"));

        /// <summary>Was die Bindung prüfen muss (Dienste und Konfig-Records des Konstruktors).</summary>
        public static IEnumerable<INamedTypeSymbol> Braucht(IMethodSymbol? ctor, INamedTypeSymbol? iDienst) => ctor == null
            ? Enumerable.Empty<INamedTypeSymbol>()
            : ctor.Parameters.Select(p => p.Type).OfType<INamedTypeSymbol>().Where(t => ArtVon(t, iDienst) is Art.Dienst or Art.Konfig);

        /// <summary>Die Klassen (oberste Ebene) der Vertrags-Assemblies (<see cref="Vertragsassemblies"/>) — Implementierungs-Kandidaten.</summary>
        public static List<INamedTypeSymbol> Klassen(Compilation c) =>
            Vertragsassemblies.Typen(c).Where(t => t.TypeKind == TypeKind.Class && t.ContainingType == null).ToList();

        /// <summary>Die Schnittstellen (auch geschachtelte) der Vertrags-Assemblies — Funktionen und Dienste der Anwendung.</summary>
        public static List<INamedTypeSymbol> Interfaces(Compilation c) =>
            Vertragsassemblies.Typen(c).Where(t => t.TypeKind == TypeKind.Interface).ToList();

        /// <summary>Öffentlich, konkret, nicht generisch — nur so kann generierter Code sie bauen.</summary>
        public static bool Baubar(INamedTypeSymbol k) =>
            k.TypeKind == TypeKind.Class && !k.IsAbstract && !k.IsGenericType && k.DeclaredAccessibility == Accessibility.Public
            && (k.ContainingType == null || k.ContainingType.DeclaredAccessibility == Accessibility.Public);
    }
}
