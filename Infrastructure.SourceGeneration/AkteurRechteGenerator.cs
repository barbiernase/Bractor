// In Projekt: Infrastructure.SourceGeneration
// Dateiname: AkteurRechteGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Akteure (<c>docs/konzept-akteure.md</c>): erzeugt <c>GeneratedAkteurRechte</c> — je <c>IAkteur</c>-Record seine
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
    /// <para><b>Vertrag</b> (<c>IAkteurVertrag&lt;A&gt;</c>, docs/konzept-akteure.md §9): je <c>Auf(TEvent)</c> die erlaubten Ausgaben.
    /// Hat ein Akteur einen Vertrag, gilt <b>Befugt = IDarf ∪ Ausgaben(Vertrag)</b> und <b>Hört = Eingänge(Vertrag) ∪ (2)</b> — exakt
    /// das, worauf er reagiert, statt der groben Regel (1). Dazu der Vertrags-Hash (<c>Abstractions.Akteurvertrag</c>), gegen den
    /// der Handshake prüft.</para>
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
            var iVertrag = c.GetTypeByMetadataName("Abstractions.IAkteurVertrag`1");
            var vertraege = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);   // Akteur → Vertrag
            if (iVertrag != null)
            {
                var schnitt = new List<INamedTypeSymbol>();
                CollectInterfaces(c.GlobalNamespace, schnitt);
                foreach (var v in schnitt.OrderBy(x => x.ToDisplayString(), System.StringComparer.Ordinal))
                    foreach (var i in v.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iVertrag)))
                        if (i.TypeArguments[0] is INamedTypeSymbol akt && !vertraege.ContainsKey(akt))
                            vertraege[akt] = v;   // mehrere je Akteur meldet CQRS061; hier zählt der erste
            }

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
            // Akteur = ein Record/eine Klasse mit IAkteur (Domänen-Experte). Ein Dienst ist nie Akteur — er gehört einem
            //   (IAkteurDienst<A>, docs/konzept-akteure.md §8); Interfaces zählen daher nicht.
            var akteure = alle.Where(t => Implementiert(t, iAkteur))
                .OrderBy(t => t.Name, System.StringComparer.Ordinal).ToList();
            string ArtVon(INamedTypeSymbol t) =>
                t.AllInterfaces.Any(i => i.ToDisplayString() == "Abstractions.IMensch") ? "Mensch"
                : t.AllInterfaces.Any(i => i.ToDisplayString() == "Abstractions.IMaschine") ? "Maschine"
                : t.AllInterfaces.Any(i => i.ToDisplayString() == "Abstractions.IKi") ? "Ki" : "";
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

                // Vertrag: je Auf(Event) die Ausgaben (konkrete Commands; die Form prüft CQRS061/062).
                vertraege.TryGetValue(a, out var vertrag);
                var reaktionen = new List<(INamedTypeSymbol Ein, List<INamedTypeSymbol> Aus, bool Strom)>();
                if (vertrag != null)
                    foreach (var m in vertrag.GetMembers(Abstractions.Akteurvertrag.Auf).OfType<IMethodSymbol>())
                        if (m.Parameters.Length == 1 && m.Parameters[0].Type is INamedTypeSymbol ein)
                        {
                            var aus = VertragsAusgaben(m.ReturnType, out var strom).Where(t => Implementiert(t, iCommand)).ToList();
                            reaktionen.Add((ein, aus, strom));
                        }
                foreach (var t in reaktionen.SelectMany(r => r.Aus))
                    if (!cmds.Contains(t, SymbolEqualityComparer.Default)) cmds.Add(t);   // Befugt = IDarf ∪ Ausgaben(Vertrag)

                var hoert = new SortedSet<string>(System.StringComparer.Ordinal);
                if (vertrag != null)
                    foreach (var r in reaktionen) hoert.Add(r.Ein.ToDisplayString(fq));   // exakt: worauf er reagiert
                else
                    foreach (var cmd in cmds)
                        if (cmdZuAgg.TryGetValue(cmd, out var agg) && aggEvents.TryGetValue(agg, out var evs)) hoert.UnionWith(evs);
                foreach (var q in queries)
                    if (queryZuProj.TryGetValue(q, out var p) && projEvents.TryGetValue(p, out var evs)) hoert.UnionWith(evs);

                sb.AppendLine($"        [\"{a.Name}\"] = new AkteurRechte(\"{a.Name}\", typeof({a.ToDisplayString(fq)}),");
                sb.AppendLine($"            Commands: {Menge(cmds.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Queries: {Menge(queries.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Trigger: {Menge(trigger.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            TransientEvents: {Menge(transient.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Hoert: {Menge(hoert)},");
                if (vertrag == null)
                {
                    sb.AppendLine($"            Art: \"{ArtVon(a)}\"),");
                    continue;
                }
                var kanon = Abstractions.Akteurvertrag.Kanon(a.Name, reaktionen.Select(r =>
                    new Abstractions.Akteurvertrag.Reaktion(r.Ein.Name, r.Aus.Select(x => x.Name).ToList(), r.Strom)));
                sb.AppendLine($"            Art: \"{ArtVon(a)}\")");
                sb.AppendLine("        {");
                sb.AppendLine($"            VertragTyp = typeof({vertrag.ToDisplayString(fq)}),");
                sb.AppendLine($"            VertragHash = \"{Abstractions.Akteurvertrag.Hash(kanon)}\",");
                sb.AppendLine("            Vertrag = new Dictionary<Type, IReadOnlySet<Type>>");
                sb.AppendLine("            {");
                foreach (var r in reaktionen.OrderBy(r => r.Ein.Name, System.StringComparer.Ordinal))
                    sb.AppendLine($"                [typeof({r.Ein.ToDisplayString(fq)})] = {Menge(r.Aus.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine("            },");
                sb.AppendLine($"            Stroeme = {Menge(reaktionen.Where(r => r.Strom).Select(r => r.Ein.ToDisplayString(fq)))},");
                sb.AppendLine("        },");
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

        /// <summary>Ausgaben einer Reaktion: void → keine; T/OneOf&lt;…&gt; → eine; (Async)Enumerable davon → Strom.</summary>
        private static List<INamedTypeSymbol> VertragsAusgaben(ITypeSymbol rueckgabe, out bool strom)
        {
            strom = false;
            var el = rueckgabe;
            if (el.SpecialType == SpecialType.System_Void) return new List<INamedTypeSymbol>();
            if (el is INamedTypeSymbol en && en.TypeArguments.Length == 1
                && (en.Name == "IEnumerable" || en.Name == "IAsyncEnumerable"))
            {
                strom = true;
                el = en.TypeArguments[0];
            }
            if (el is INamedTypeSymbol oneOf && oneOf.Name == "OneOf")
                return oneOf.TypeArguments.OfType<INamedTypeSymbol>().ToList();
            return el is INamedTypeSymbol n ? new List<INamedTypeSymbol> { n } : new List<INamedTypeSymbol>();
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
                if (type.TypeKind == TypeKind.Interface && type.ContainingNamespace.ToDisplayString() != "Abstractions")
                    results.Add(type);
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
