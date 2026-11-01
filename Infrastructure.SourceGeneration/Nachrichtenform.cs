// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration)
// Dateiname: Nachrichtenform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die eine Lesart der NACHRICHTEN ohne Marker (docs/03 §3.1, docs/04 §4.4) — nur aus Signaturen. Eine <b>Folge</b> ist ein Typ in der Signatur
    /// eines <c>Decide</c> (<c>IEnumerable&lt;OneOf&lt;…&gt;&gt;</c> bzw. <c>IEnumerable&lt;X&gt;</c>), der weder Event (auch kein Hinweis) noch Frist ist: ein
    /// schlichter Record. Sie geht nicht ins Log, sondern als offener Eingang an die Funktionen, die sie hören. Der Codegen (Wire-JSON-Kontext) liest
    /// dieselbe Regel über die gebauten Assemblies (<c>JsonContextEmitter</c>); fehlt ihm ein Typ, bricht der Build am generierten
    /// <c>Default.{Typ}</c>.
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

        /// <summary>Ein schlichter Record bzw. eine konkrete Klasse ohne Marker des Vertrags — weder Event noch Frist noch Command, Query oder Antwort.</summary>
        public static bool IstSchlicht(ITypeSymbol t) =>
            t is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false, IsGenericType: false } n
            && n.SpecialType == SpecialType.None
            && !n.AllInterfaces.Any(i => i.ContainingNamespace?.ToDisplayString() == "Abstractions"
                                         && (i.Name == "IEvent" || i.Name == "IFrist" || i.Name == "ICommand" || i.Name == "IQuery" || i.Name == "IQueryResponse"
                                             || i.Name == "IAusgabe"));

        /// <summary>Ist <paramref name="t"/> ein Decider (<c>IDecider&lt;T&gt;</c>)?</summary>
        public static bool IstDecider(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == A + "IDecider<TState>");

        /// <summary>Die Folgen eines Decide (die schlichten Typen seiner Signatur).</summary>
        public static IEnumerable<INamedTypeSymbol> FolgenVon(IMethodSymbol decide) =>
            Faelle(decide.ReturnType).Where(IstSchlicht).OfType<INamedTypeSymbol>();

        /// <summary>Die Decide-Methoden eines Deciders (öffentlich, erster Parameter ein Command).</summary>
        public static IEnumerable<IMethodSymbol> Decides(INamedTypeSymbol decider) =>
            decider.GetMembers("Decide").OfType<IMethodSymbol>()
                .Where(m => !m.IsStatic && m.DeclaredAccessibility == Accessibility.Public && m.Parameters.Length > 0
                            && m.Parameters[0].Type.AllInterfaces.Any(i => i.ToDisplayString() == A + "ICommand"));

        /// <summary>Ist <paramref name="t"/> eine Funktion (Klasse oder Schnittstelle, die <c>IFunktion</c> erweitert)?</summary>
        public static bool IstFunktion(INamedTypeSymbol t) =>
            (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Interface) && t.ToDisplayString() != A + "IFunktion"
            && t.AllInterfaces.Any(i => i.ToDisplayString() == A + "IFunktion");

        /// <summary>Die schlichten Typen der <c>Auf</c> einer Funktion: Eingänge (Folgen, Zwischenergebnisse) und Ausgaben (Zwischenergebnisse).</summary>
        public static IEnumerable<INamedTypeSymbol> VonFunktion(INamedTypeSymbol funktion) =>
            funktion.GetMembers("Auf").OfType<IMethodSymbol>()
                .Where(m => !m.IsStatic && m.Parameters.Length > 0)
                .SelectMany(m => new[] { m.Parameters[0].Type }.Concat(Faelle(m.ReturnType)))
                .Where(IstSchlicht).OfType<INamedTypeSymbol>();

        /// <summary>
        /// Alle Nachrichten ohne Marker der Anwendung — die Folgen der Decides und die schlichten Ein- und Ausgaben der Funktionen —, nach Namen
        /// sortiert, jede einmal. Sie reisen im Cluster (Wire-JSON) und über die Leitung (Nutzlast, {Typ}Dto-Message) zu Funktionen draußen.
        /// </summary>
        public static List<INamedTypeSymbol> Alle(Compilation c)
        {
            var aus = Folgen(c);
            foreach (var t in Vertragsassemblies.Typen(c).Where(IstFunktion))
                foreach (var n in VonFunktion(t))
                    if (!aus.Contains(n, SymbolEqualityComparer.Default)) aus.Add(n);
            aus.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
            return aus;
        }

        /// <summary>Alle Folgen der Anwendung (eigene und referenzierte Vertrags-Assemblies), nach Namen sortiert, jede einmal.</summary>
        public static List<INamedTypeSymbol> Folgen(Compilation c)
        {
            var aus = new List<INamedTypeSymbol>();
            foreach (var t in Vertragsassemblies.Typen(c).Where(IstDecider))
                foreach (var d in Decides(t))
                    foreach (var f in FolgenVon(d))
                        if (!aus.Contains(f, SymbolEqualityComparer.Default)) aus.Add(f);
            aus.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
            return aus;
        }
    }
}
