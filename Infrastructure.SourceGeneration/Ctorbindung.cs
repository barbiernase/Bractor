// In Projekt: Infrastructure.SourceGeneration
// Dateiname: Ctorbindung.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Was der Konstruktor einer Funktion bzw. ihrer Umsetzung nimmt (docs/06 §6.5) — die EINE Lesart für <c>FunktionsBausteinGenerator</c> und
    /// <c>TechnikGenerator</c>: Konfig-Records (Werte aus <c>Konfig:&lt;Record&gt;</c>), <c>ILogger&lt;T&gt;</c>, ihren Zustand und alles andere als
    /// Technik (ohne Marker: eine Schnittstelle oder Klasse der Anwendung, die der Generator bindet).
    /// Gebaut wird die Implementierung per generierter Fabrik (<c>sp =&gt; new Impl(sp.GetRequiredService&lt;P&gt;(), …)</c>) — ohne
    /// reflektierten Aktivator. Was nicht passt, meldet der Analyzer (CQRS090); hier wird es trotzdem aus dem Container geholt.
    /// </summary>
    internal static class Ctorbindung
    {
        /// <summary>
        /// Was ein Konstruktor-Parameter ist; <see cref="Zustand"/> = <c>IZustand&lt;T&gt;</c> (docs/04 §4.4), aus der Aktivierung, nie aus dem
        /// Container; <see cref="Technik"/> = jede andere Schnittstelle oder Klasse der Anwendung, die keine Funktion ist; <see cref="Fremd"/> = was die
        /// Umgebung nicht binden kann (eine Funktion, ein Typ außerhalb der Anwendung, ein Wert).
        /// </summary>
        public enum Art { Technik, Konfig, Logger, Zustand, Fremd }

        /// <summary>Der Konstruktor, den die Fabrik benutzt: der öffentliche mit den meisten Parametern (wie der DI-Aktivator).</summary>
        public static IMethodSymbol? Konstruktor(INamedTypeSymbol t) => t.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();

        public static Art ArtVon(ITypeSymbol p)
        {
            if (p is INamedTypeSymbol { IsGenericType: true } g && g.OriginalDefinition.ToDisplayString() == "Microsoft.Extensions.Logging.ILogger<TCategoryName>")
                return Art.Logger;
            if (p is INamedTypeSymbol { IsGenericType: true } z && (z.OriginalDefinition.ContainingNamespace?.ToDisplayString() + "." + z.OriginalDefinition.MetadataName)
                == Abstractions.Funktionsvertrag.Zustand)
                return Art.Zustand;
            if (p is INamedTypeSymbol { TypeKind: TypeKind.Class, IsRecord: true, IsAbstract: false } r && Vertragsassemblies.NutztVertrag(r.ContainingAssembly)) return Art.Konfig;
            if (p is INamedTypeSymbol { TypeKind: TypeKind.Interface or TypeKind.Class } t && Vertragsassemblies.NutztVertrag(t.ContainingAssembly)
                && !Funktionsform.Hat(t, Funktionsform.IFunktion) && !Funktionsform.Hat(t, Funktionsform.IQuellart))
                return Art.Technik;
            return Art.Fremd;
        }

        /// <summary>Ein Feld-Typ eines Konfig-Records, den die generierte Bindung lesen kann (<c>Abstractions.Funktionsvertrag.Feldtypen</c>, Enum, string[]).</summary>
        public static bool FeldBindbar(ITypeSymbol t) =>
            t.TypeKind == TypeKind.Enum
            || t is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }
            || Abstractions.Funktionsvertrag.IstFeldtyp((t.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.MetadataName);

        /// <summary>
        /// Die Argumente der Fabrik: je Parameter <c>sp.GetRequiredService&lt;T&gt;()</c> — ein Zustand (<c>IZustand&lt;T&gt;</c>) kommt aus der
        /// Aktivierung (<paramref name="zustand"/>: der Ausdruck in der Fabrik, der ihn liefert), nie aus dem Container.
        /// </summary>
        public static string Argumente(IMethodSymbol? ctor, SymbolDisplayFormat fq, string? zustand = null) => ctor == null ? ""
            : string.Join(", ", ctor.Parameters.Select(p => zustand != null && ArtVon(p.Type) == Art.Zustand
                ? $"({p.Type.ToDisplayString(fq)}){zustand}!"
                : $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{p.Type.ToDisplayString(fq)}>(sp)"));

        /// <summary>
        /// Die Form eines Zustands (<c>FunktionsZustandsform</c>): frisch, sichern, laden, zurücksetzen — über den Serializer-Kontext der Komposition
        /// (<c>EventJsonSerializerContext</c>, vom Codegen-Prepass um die Zustände ergänzt; fehlt der Typ dort, bricht der Build).
        /// </summary>
        public static string Zustandsform(INamedTypeSymbol z, SymbolDisplayFormat fq)
        {
            var typ = z.ToDisplayString(fq);
            var huelle = $"global::Infrastructure.Funktionen.Zustand<{typ}>";
            var info = $"global::Infrastructure.Serialization.EventJsonSerializerContext.Default.{z.Name}";
            return $"new global::Infrastructure.Funktionen.FunktionsZustandsform(typeof({typ}), () => new {huelle}(), "
                + $"z => global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes((({huelle})z).Wert, {info}), "
                + $"(z, d) => (({huelle})z).Wert = global::System.Text.Json.JsonSerializer.Deserialize(d, {info}) ?? new {typ}(), "
                + $"z => (({huelle})z).Wert = new {typ}())";
        }

        /// <summary>Was die Bindung prüfen muss (Technik und Konfig-Records des Konstruktors).</summary>
        public static IEnumerable<INamedTypeSymbol> Braucht(IMethodSymbol? ctor) => ctor == null
            ? Enumerable.Empty<INamedTypeSymbol>()
            : ctor.Parameters.Select(p => p.Type).OfType<INamedTypeSymbol>().Where(t => ArtVon(t) is Art.Technik or Art.Konfig);

        /// <summary>Die Klassen (oberste Ebene) der Vertrags-Assemblies (<see cref="Vertragsassemblies"/>) — Implementierungs-Kandidaten.</summary>
        public static List<INamedTypeSymbol> Klassen(Compilation c) =>
            Vertragsassemblies.Typen(c).Where(t => t.TypeKind == TypeKind.Class && t.ContainingType == null).ToList();

        /// <summary>Öffentlich, konkret, nicht generisch — nur so kann generierter Code sie bauen.</summary>
        public static bool Baubar(INamedTypeSymbol k) =>
            k.TypeKind == TypeKind.Class && !k.IsAbstract && k.TypeArguments.All(a => a.TypeKind != TypeKind.TypeParameter)
            && k.DeclaredAccessibility == Accessibility.Public
            && (k.ContainingType == null || k.ContainingType.DeclaredAccessibility == Accessibility.Public);
    }
}
