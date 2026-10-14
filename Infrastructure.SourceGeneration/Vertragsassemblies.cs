// In Projekt: Infrastructure.SourceGeneration (per Link auch in Client.SourceGeneration)
// Dateiname: Vertragsassemblies.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// WO Generatoren nach Typen suchen — als Code-Fakt statt Namensliste: die eigene Assembly und jede referenzierte Assembly, die den
    /// Vertrag (<c>Abstractions</c>) referenziert. Fremd-Bibliotheken (Marten, Proto.Actor, OpenCvSharp, BCL …) kennen den Vertrag nicht
    /// und werden nie durchlaufen; der Vertrag selbst referenziert sich nicht und bleibt ebenfalls außen vor.
    /// </summary>
    internal static class Vertragsassemblies
    {
        public const string Vertrag = "Abstractions";

        /// <summary>Kennt die Assembly den Vertrag (referenziert sie <c>Abstractions</c>)?</summary>
        public static bool NutztVertrag(IAssemblySymbol? a) =>
            a != null && a.Modules.Any(m => m.ReferencedAssemblies.Any(r => r.Name == Vertrag));

        /// <summary>Die eigene Assembly und die referenzierten Vertrags-Nutzer (Domäne, Framework), sortiert.</summary>
        public static IEnumerable<IAssemblySymbol> Von(Compilation c) =>
            new[] { c.Assembly }.Concat(c.SourceModule.ReferencedAssemblySymbols.Where(NutztVertrag).OrderBy(a => a.Name, System.StringComparer.Ordinal));

        /// <summary>Wie <see cref="Von"/>, dazu der Vertrag selbst (seine Framework-Nachrichten, z. B. <c>CommandFailed</c>).</summary>
        public static IEnumerable<IAssemblySymbol> MitVertrag(Compilation c)
        {
            var eigene = Von(c).ToList();
            var vertrag = c.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(a => a.Name == Vertrag);
            return vertrag == null || eigene.Contains(vertrag, SymbolEqualityComparer.Default) ? eigene : eigene.Append(vertrag);
        }

        /// <summary>Alle Typen dieser Assemblies (mit geschachtelten), je Assembly nur die eigenen.</summary>
        public static List<INamedTypeSymbol> Typen(Compilation c)
        {
            var aus = new List<INamedTypeSymbol>();
            foreach (var a in Von(c)) Sammle(a.GlobalNamespace, aus);
            return aus;
        }

        private static void Sammle(INamespaceSymbol ns, List<INamedTypeSymbol> aus)
        {
            foreach (var t in ns.GetTypeMembers()) Geschachtelt(t, aus);
            foreach (var sub in ns.GetNamespaceMembers()) Sammle(sub, aus);
        }

        private static void Geschachtelt(INamedTypeSymbol t, List<INamedTypeSymbol> aus)
        {
            aus.Add(t);
            foreach (var n in t.GetTypeMembers()) Geschachtelt(n, aus);
        }
    }
}
