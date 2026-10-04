// In Projekt: Infrastructure.SourceGeneration
// Dateiname: AkteurRechteGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Akteure (<c>docs/konzept-akteure.md</c>): erzeugt <c>GeneratedAkteurRechte</c> — je <c>IAkteur</c>-Typ seine
    /// Befugnisse, reflexionsfrei als Tabelle.
    ///
    /// <para><b>Deklariert</b> (Basisliste): jedes <c>IDarf&lt;T&gt;</c>, nach Art sortiert (Command, Query, Trigger,
    /// Transient-Event).</para>
    /// <para><b>Abgeleitet</b> (aus dem Graphen, kein zweites Wort): die Hör-Menge =
    /// (1) alle Events, die das Aggregat eines erlaubten Commands erzeugt (Decide-OneOf aller Decide dieses Aggregats,
    ///     inkl. Ablehnungen) — „du hörst das Ding, auf das du einwirkst";
    /// (2) alle Events, die die Projektion hinter einer erlaubten Query behandelt (Query → Reader-Handle →
    ///     <c>IReader&lt;TProjektion&gt;</c> → Projektions-Handle mit <c>IAggregateEnvelope</c>) — „du hörst, was du
    ///     sowieso lesen darfst".</para>
    /// </summary>
    [Generator]
    public class AkteurRechteGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor NameKollision = new DiagnosticDescriptor(
            id: "CQRS059",
            title: "Zwei Akteure mit gleichem Namen",
            messageFormat: "Der Akteur-Name '{0}' ist mehrdeutig ({1}) — die Composition Root ordnet Tokens über den "
                + "einfachen Typnamen zu. Benenne einen der Akteure um.",
            category: "CQRS.Akteur",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var c = context.Compilation;
            var iAkteur = c.GetTypeByMetadataName("Abstractions.IAkteur");
            var iDarf = c.GetTypeByMetadataName("Abstractions.IDarf`1");
            if (iAkteur == null || iDarf == null)
                return;
            var iCommand = c.GetTypeByMetadataName("Abstractions.ICommand");
            var iQuery = c.GetTypeByMetadataName("Abstractions.IQuery");
            var iTrigger = c.GetTypeByMetadataName("Abstractions.IPipelineTrigger");
            var iEvent = c.GetTypeByMetadataName("Abstractions.IEvent");
            var iTransient = c.GetTypeByMetadataName("Abstractions.ITransientEvent");
            var iDecider = c.GetTypeByMetadataName("Abstractions.IDecider`1");
            var iReader = c.GetTypeByMetadataName("Abstractions.IReader`1");
            var iAggEnvelope = c.GetTypeByMetadataName("Abstractions.IAggregateEnvelope");
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var alle = new List<INamedTypeSymbol>();
            CollectTypes(c.GlobalNamespace, alle);

            // ── Graph: Command → Aggregat, Aggregat → Events (aus den Decide-Signaturen) ──
            var cmdZuAgg = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var aggEvents = new Dictionary<INamedTypeSymbol, SortedSet<string>>(SymbolEqualityComparer.Default);
            // ── Graph: Query → Projektion, Projektion → behandelte Events ──
            var queryZuProj = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var projEvents = new Dictionary<INamedTypeSymbol, SortedSet<string>>(SymbolEqualityComparer.Default);

            foreach (var t in alle)
            {
                var dec = iDecider == null ? null : t.AllInterfaces.FirstOrDefault(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDecider));
                if (dec?.TypeArguments.FirstOrDefault() is INamedTypeSymbol state)
                {
                    if (!aggEvents.TryGetValue(state, out var evs))
                        aggEvents[state] = evs = new SortedSet<string>(System.StringComparer.Ordinal);
                    foreach (var m in t.GetMembers(Abstractions.Aggregatvertrag.Decide).OfType<IMethodSymbol>())
                    {
                        if (m.Parameters.Length < 1 || m.Parameters[0].Type is not INamedTypeSymbol cmd) continue;
                        if (!Implementiert(cmd, iCommand)) continue;
                        cmdZuAgg[cmd] = state;
                        foreach (var e in EventsAus(m.ReturnType, iEvent, iTransient))
                            evs.Add(e.ToDisplayString(fq));
                    }
                }

                var rdr = iReader == null ? null : t.AllInterfaces.FirstOrDefault(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iReader));
                if (rdr?.TypeArguments.FirstOrDefault() is INamedTypeSymbol proj)
                {
                    foreach (var m in t.GetMembers("Handle").OfType<IMethodSymbol>())
                        if (m.Parameters.Length >= 1 && m.Parameters[0].Type is INamedTypeSymbol q && Implementiert(q, iQuery))
                            queryZuProj[q] = proj;
                    if (!projEvents.ContainsKey(proj))
                        projEvents[proj] = ProjektionsEvents(proj, iAggEnvelope, iEvent, iTransient, fq);
                }
            }

            // ── Akteure ──
            // Akteur = ein Typ mit IAkteur — ein Record (Mensch/Fremdsystem) ODER ein Dienst-Vertrag (Interface, z. B. die KI).
            //   Die Implementierungsklasse eines Akteur-Dienstes ist kein eigener Akteur (der Vertrag ist es).
            var vertraege = new List<INamedTypeSymbol>();
            CollectInterfaces(c.GlobalNamespace, vertraege);
            bool IstAkteurVertrag(INamedTypeSymbol i) => !SymbolEqualityComparer.Default.Equals(i, iAkteur) && Implementiert(i, iAkteur);
            var akteure = alle.Where(t => Implementiert(t, iAkteur) && !t.AllInterfaces.Any(IstAkteurVertrag))
                .Concat(vertraege.Where(IstAkteurVertrag))
                .OrderBy(t => t.Name, System.StringComparer.Ordinal).ToList();
            foreach (var g in akteure.GroupBy(a => a.Name).Where(g => g.Count() > 1))
                context.ReportDiagnostic(Diagnostic.Create(NameKollision, g.First().Locations.FirstOrDefault() ?? Location.None,
                    g.Key, string.Join(", ", g.Select(a => a.ToDisplayString(fq)))));

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Akteure: deklarierte IDarf<T> + aus dem Graphen abgeleitete Hör-Mengen (docs/konzept-akteure.md).");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Akteure;");
            sb.AppendLine();
            sb.AppendLine("public static class GeneratedAkteurRechte");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Akteur-Name (einfacher Typname) → Befugnisse.</summary>");
            sb.AppendLine("    public static IReadOnlyDictionary<string, AkteurRechte> Alle { get; } = new Dictionary<string, AkteurRechte>(StringComparer.Ordinal)");
            sb.AppendLine("    {");
            foreach (var a in akteure.GroupBy(x => x.Name).Select(g => g.First()))
            {
                var darf = a.AllInterfaces
                    .Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf))
                    .Select(i => i.TypeArguments[0]).OfType<INamedTypeSymbol>()
                    .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
                var cmds = darf.Where(t => Implementiert(t, iCommand)).ToList();
                var queries = darf.Where(t => Implementiert(t, iQuery)).ToList();
                var trigger = darf.Where(t => Implementiert(t, iTrigger)).ToList();
                var transient = darf.Where(t => Implementiert(t, iTransient)).ToList();

                var hoert = new SortedSet<string>(System.StringComparer.Ordinal);
                foreach (var cmd in cmds)
                    if (cmdZuAgg.TryGetValue(cmd, out var agg) && aggEvents.TryGetValue(agg, out var evs)) hoert.UnionWith(evs);
                foreach (var q in queries)
                    if (queryZuProj.TryGetValue(q, out var p) && projEvents.TryGetValue(p, out var evs)) hoert.UnionWith(evs);

                sb.AppendLine($"        [\"{a.Name}\"] = new AkteurRechte(\"{a.Name}\", typeof({a.ToDisplayString(fq)}),");
                sb.AppendLine($"            Commands: {Menge(cmds.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Queries: {Menge(queries.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Trigger: {Menge(trigger.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            TransientEvents: {Menge(transient.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Hoert: {Menge(hoert)}),");
            }
            sb.AppendLine("    };");
            sb.AppendLine("}");
            context.AddSource("GeneratedAkteurRechte.g.cs", sb.ToString());
        }

        private static string Menge(IEnumerable<string> typen)
        {
            var liste = typen.OrderBy(x => x, System.StringComparer.Ordinal).ToList();
            return liste.Count == 0
                ? "new HashSet<Type>()"
                : "new HashSet<Type> { " + string.Join(", ", liste.Select(t => $"typeof({t})")) + " }";
        }

        /// <summary>Events, die eine Projektion behandelt: erster Parameter jedes <c>Handle(evt, IAggregateEnvelope, …)</c>.</summary>
        private static SortedSet<string> ProjektionsEvents(INamedTypeSymbol proj, INamedTypeSymbol? iAggEnvelope,
            INamedTypeSymbol? iEvent, INamedTypeSymbol? iTransient, SymbolDisplayFormat fq)
        {
            var evs = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (var m in proj.GetMembers("Handle").OfType<IMethodSymbol>())
            {
                if (m.Parameters.Length < 2 || m.Parameters[0].Type is not INamedTypeSymbol e) continue;
                if (iAggEnvelope != null && !SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, iAggEnvelope)) continue;
                if (Implementiert(e, iEvent) || Implementiert(e, iTransient)) evs.Add(e.ToDisplayString(fq));
            }
            return evs;
        }

        /// <summary>Alle Event-Typen (persistiert + Ablehnung) aus <c>IEnumerable&lt;OneOf&lt;…&gt;&gt;</c> bzw. <c>IEnumerable&lt;E&gt;</c>.</summary>
        private static IEnumerable<INamedTypeSymbol> EventsAus(ITypeSymbol rueckgabe, INamedTypeSymbol? iEvent, INamedTypeSymbol? iTransient)
        {
            if (rueckgabe is not INamedTypeSymbol en || en.TypeArguments.Length != 1) yield break;
            if (en.TypeArguments[0] is not INamedTypeSymbol el) yield break;
            var kandidaten = el.TypeArguments.Length > 0 ? el.TypeArguments.ToList() : new List<ITypeSymbol> { el };
            foreach (var k in kandidaten.OfType<INamedTypeSymbol>())
                if (Implementiert(k, iEvent) || Implementiert(k, iTransient))
                    yield return k;
        }

        private static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? iface) =>
            iface != null && (SymbolEqualityComparer.Default.Equals(t, iface)
                || t.AllInterfaces.Contains(iface, SymbolEqualityComparer.Default));

        private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var type in ns.GetTypeMembers())
                CollectNested(type, results);
            foreach (var sub in ns.GetNamespaceMembers())
                CollectTypes(sub, results);
        }

        private static void CollectInterfaces(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var type in ns.GetTypeMembers())
                if (type.TypeKind == TypeKind.Interface) results.Add(type);
            foreach (var sub in ns.GetNamespaceMembers())
                CollectInterfaces(sub, results);
        }

        private static void CollectNested(INamedTypeSymbol type, List<INamedTypeSymbol> results)
        {
            if (type.TypeKind == TypeKind.Class && !type.IsAbstract)
                results.Add(type);
            foreach (var nested in type.GetTypeMembers())
                CollectNested(nested, results);
        }
    }
}
