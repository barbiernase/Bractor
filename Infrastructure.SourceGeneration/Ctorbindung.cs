// In Projekt: Infrastructure.SourceGeneration
// Dateiname: Ctorbindung.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Was der Konstruktor einer Funktions- oder Dienst-Implementierung nimmt (Plan E11) — die EINE Lesart für <c>FunktionsGenerator</c> und
    /// <c>DienstGenerator</c>: Dienste (<c>IDienst</c>), Konfig-Records (Werte aus <c>Konfig:&lt;Record&gt;</c>) und <c>ILogger&lt;T&gt;</c>.
    /// Gebaut wird die Implementierung per generierter Fabrik (<c>sp =&gt; new Impl(sp.GetRequiredService&lt;P&gt;(), …)</c>) — ohne
    /// reflektierten Aktivator. Was nicht passt, meldet der Analyzer (CQRS090); hier wird es trotzdem aus dem Container geholt.
    /// </summary>
    internal static class Ctorbindung
    {
        public enum Art { Dienst, Konfig, Logger, Fremd }

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
            if (p is INamedTypeSymbol { TypeKind: TypeKind.Class, IsRecord: true, IsAbstract: false } r && !IstRahmen(r)) return Art.Konfig;
            return Art.Fremd;
        }

        /// <summary>Typen aus System/Microsoft/Abstractions sind nie Konfig-Records der Domäne.</summary>
        private static bool IstRahmen(INamedTypeSymbol t)
        {
            var ns = t.ContainingNamespace?.ToDisplayString() ?? "";
            return ns == "System" || ns.StartsWith("System.") || ns == "Microsoft" || ns.StartsWith("Microsoft.") || ns == "Abstractions" || ns.StartsWith("Abstractions.");
        }

        /// <summary>Ein Feld-Typ eines Konfig-Records, den die generierte Bindung lesen kann (<c>Abstractions.Dienstvertrag.Feldtypen</c>, Enum, string[]).</summary>
        public static bool FeldBindbar(ITypeSymbol t) =>
            t.TypeKind == TypeKind.Enum
            || t is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }
            || Abstractions.Dienstvertrag.IstFeldtyp((t.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.MetadataName);

        /// <summary>Die Argumente der Fabrik: je Parameter <c>sp.GetRequiredService&lt;T&gt;()</c>.</summary>
        public static string Argumente(IMethodSymbol? ctor, SymbolDisplayFormat fq) => ctor == null ? ""
            : string.Join(", ", ctor.Parameters.Select(p => $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{p.Type.ToDisplayString(fq)}>(sp)"));

        /// <summary>Was die Bindung prüfen muss (Dienste und Konfig-Records des Konstruktors).</summary>
        public static IEnumerable<INamedTypeSymbol> Braucht(IMethodSymbol? ctor, INamedTypeSymbol? iDienst) => ctor == null
            ? Enumerable.Empty<INamedTypeSymbol>()
            : ctor.Parameters.Select(p => p.Type).OfType<INamedTypeSymbol>().Where(t => ArtVon(t, iDienst) is Art.Dienst or Art.Konfig);

        public static void SammleKlassen(INamespaceSymbol ns, List<INamedTypeSymbol> aus)
        {
            foreach (var t in ns.GetTypeMembers()) if (t.TypeKind == TypeKind.Class) aus.Add(t);
            foreach (var sub in ns.GetNamespaceMembers())
            {
                var n = sub.Name;
                if (ns.IsGlobalNamespace && (n == "System" || n == "Microsoft" || n == "Marten" || n == "Proto" || n == "Npgsql" || n == "JasperFx"
                        || n == "Google" || n == "Grpc" || n == "StackExchange" || n == "Weasel" || n == "Abstractions" || n == "OpenCvSharp")) continue;
                SammleKlassen(sub, aus);
            }
        }

        public static void SammleInterfaces(INamespaceSymbol ns, List<INamedTypeSymbol> aus)
        {
            foreach (var t in ns.GetTypeMembers()) SammleGeschachtelt(t, aus);
            foreach (var sub in ns.GetNamespaceMembers()) SammleInterfaces(sub, aus);
        }

        private static void SammleGeschachtelt(INamedTypeSymbol t, List<INamedTypeSymbol> aus)
        {
            if (t.TypeKind == TypeKind.Interface && t.ContainingNamespace?.ToDisplayString() != "Abstractions") aus.Add(t);
            foreach (var n in t.GetTypeMembers()) SammleGeschachtelt(n, aus);
        }

        /// <summary>Öffentlich, konkret, nicht generisch — nur so kann generierter Code sie bauen.</summary>
        public static bool Baubar(INamedTypeSymbol k) =>
            k.TypeKind == TypeKind.Class && !k.IsAbstract && !k.IsGenericType && k.DeclaredAccessibility == Accessibility.Public
            && (k.ContainingType == null || k.ContainingType.DeclaredAccessibility == Accessibility.Public);
    }
}
