// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration)
// Dateiname: Nachrichtenform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die eine Lesart der COMMANDS OHNE ZIEL (docs/03 §3.1, docs/04 §4.4): ein konkreter Typ mit <c>ICommand</c>, aber ohne <c>ICommand&lt;T&gt;</c>.
    /// Er fließt zwischen den Knoten eines Graphen und aus einem <c>Decide</c> an den Kopf eines Graphen (dann im selben Commit als offener Eingang),
    /// nie ins Log. Er reist im Cluster (Wire-JSON) und über die Leitung (Nutzlast, {Typ}Dto-Message). Der Codegen liest dieselbe Regel über die
    /// gebauten Assemblies (<c>Nachrichtenlesart</c>); fehlt ihm ein Typ, bricht der Build am generierten <c>Default.{Typ}</c>.
    /// </summary>
    internal static class Nachrichtenform
    {
        private const string A = "Abstractions.";

        /// <summary>Die Fälle einer Rückgabe: Task/IEnumerable/Nullable abgewickelt, bei OneOf seine Typargumente.</summary>
        public static IEnumerable<ITypeSymbol> Faelle(ITypeSymbol t)
        {
            while (t is INamedTypeSymbol { TypeArguments.Length: 1 } h
                   && (h.Name == "Task" || h.Name == "ValueTask" || h.Name == "IEnumerable" || h.Name == "IAsyncEnumerable"))
                t = h.TypeArguments[0];
            if (t is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } n) t = n.TypeArguments[0];
            if (t is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions") return o.TypeArguments;
            if (t.SpecialType == SpecialType.System_Void || t is INamedTypeSymbol { Name: "Task", TypeArguments.Length: 0 }) return Enumerable.Empty<ITypeSymbol>();
            return new[] { t };
        }

        /// <summary>Ein Command ohne Ziel: konkret, nicht generisch, mit <c>ICommand</c>, ohne <c>IAggregatCommand</c> (also ohne <c>ICommand&lt;T&gt;</c>).</summary>
        public static bool IstOhneZiel(ITypeSymbol t) =>
            t is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false, IsGenericType: false } n
            && n.AllInterfaces.Any(i => i.ToDisplayString() == A + "ICommand")
            && !n.AllInterfaces.Any(i => i.ToDisplayString() == A + "IAggregatCommand");

        /// <summary>Ist <paramref name="t"/> ein Decider (<c>IDecider&lt;T&gt;</c>)?</summary>
        public static bool IstDecider(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == A + "IDecider<TState>");

        /// <summary>Die Commands ohne Ziel eines Decide (aus seiner Signatur).</summary>
        public static IEnumerable<INamedTypeSymbol> CommandsVon(IMethodSymbol decide) =>
            Faelle(decide.ReturnType).Where(IstOhneZiel).OfType<INamedTypeSymbol>();

        /// <summary>Die Decide-Methoden eines Deciders (öffentlich, erster Parameter ein Command an ein Aggregat).</summary>
        public static IEnumerable<IMethodSymbol> Decides(INamedTypeSymbol decider) =>
            decider.GetMembers("Decide").OfType<IMethodSymbol>()
                .Where(m => !m.IsStatic && m.DeclaredAccessibility == Accessibility.Public && m.Parameters.Length > 0
                            && m.Parameters[0].Type.AllInterfaces.Any(i => i.ToDisplayString() == A + "IAggregatCommand"));

        /// <summary>
        /// Alle Commands ohne Ziel der Anwendung (eigene und referenzierte Vertrags-Assemblies), nach Namen sortiert, jeder einmal. Sie reisen im
        /// Cluster (Wire-JSON) und über die Leitung (Nutzlast, {Typ}Dto-Message) zu Funktionen draußen.
        /// </summary>
        public static List<INamedTypeSymbol> Alle(Compilation c)
        {
            var aus = Vertragsassemblies.Typen(c).Where(IstOhneZiel).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToList();
            aus.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
            return aus;
        }
    }
}
