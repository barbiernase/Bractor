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
    /// <para><b>Funktionen</b>: die Funktionen draußen, die er rechnen darf (<c>IDarf&lt;IFunktion-Typ&gt;</c>) — ein Client, der ihn verkörpert,
    /// darf sie tragen.</para>
    /// <para><b>Clients</b> (<c>GeneratedClientVertraege</c>, docs/konzept-akteure.md §4): je <c>IClientVertrag</c> was er trägt, sendet, fragt
    /// und hört, die verkörperten Akteure und der Client-Hash (<c>Abstractions.Clientvertragsregeln</c>), gegen den der Handshake prüft.</para>
    /// </summary>
    [Generator]
    public class AkteurRechteGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor NameKollision = new DiagnosticDescriptor(
            id: "CQRS059",
            title: "Zwei Akteure/Clients mit gleichem Namen",
            messageFormat: "Der Name '{0}' ist mehrdeutig ({1}) — Composition Root und Handshake ordnen Akteure und Clients über den "
                + "einfachen Namen zu (Client: Interface ohne führendes I). Benenne einen um.",
            category: "CQRS.Akteur",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!Laufzeitprojekt.Ist(context.Compilation)) return;   // nur in der Komposition (docs/07)
            var c = context.Compilation;
            var iAkteur = c.GetTypeByMetadataName("Abstractions.IAkteur");
            var iDarf = c.GetTypeByMetadataName("Abstractions.IDarf`1");
            if (iAkteur == null || iDarf == null)
                return;
            var iCommand = c.GetTypeByMetadataName("Abstractions.ICommand");
            var iQuery = c.GetTypeByMetadataName("Abstractions.IQuery");
            // Eine Quell-Nachricht (von einem Treiber oder vom Client) ist eine eigene Befugnis-Art.
            var iQuelle = c.GetTypeByMetadataName("Abstractions.IQuellNachricht");
            var iEvent = c.GetTypeByMetadataName("Abstractions.IEvent");
            var iTransient = c.GetTypeByMetadataName("Abstractions.ITransientEvent");
            var iFunktion = c.GetTypeByMetadataName("Abstractions.IFunktion");
            var iDecider = c.GetTypeByMetadataName("Abstractions.IDecider`1");
            var iReader = c.GetTypeByMetadataName("Abstractions.IReader`1");
            var iAggEnvelope = c.GetTypeByMetadataName("Abstractions.IAggregateEnvelope");
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var alle = new List<INamedTypeSymbol>();
            CollectTypes(c.GlobalNamespace, alle);
            var iClient = c.GetTypeByMetadataName("Abstractions.IClientVertrag");
            var schnitt = new List<INamedTypeSymbol>();
            CollectInterfaces(c.GlobalNamespace, schnitt);
            schnitt = schnitt.OrderBy(x => x.ToDisplayString(), System.StringComparer.Ordinal).ToList();
            bool IstClient(INamedTypeSymbol t) => iClient != null && t.AllInterfaces.Contains(iClient, SymbolEqualityComparer.Default);

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
            // Akteur = ein Record/eine Klasse mit IAkteur (Domänen-Experte); Interfaces zählen nicht.
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
                var quellen = darf.Where(t => Implementiert(t, iQuelle)).ToList();
                var transient = darf.Where(t => Implementiert(t, iTransient)).ToList();
                // Funktionen draußen, die er rechnen (= deren Ausgaben er hineingeben) darf.
                var funktionen = darf.Where(t => t.TypeKind == TypeKind.Interface && Implementiert(t, iFunktion)).ToList();

                var hoert = new SortedSet<string>(System.StringComparer.Ordinal);
                foreach (var cmd in cmds)
                    if (cmdZuAgg.TryGetValue(cmd, out var agg) && aggEvents.TryGetValue(agg, out var evs)) hoert.UnionWith(evs);
                foreach (var q in queries)
                    if (queryZuProj.TryGetValue(q, out var p) && projEvents.TryGetValue(p, out var evs)) hoert.UnionWith(evs);

                sb.AppendLine($"        [\"{a.Name}\"] = new AkteurRechte(\"{a.Name}\", typeof({a.ToDisplayString(fq)}),");
                sb.AppendLine($"            Commands: {Menge(cmds.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Queries: {Menge(queries.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Quellen: {Menge(quellen.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            TransientEvents: {Menge(transient.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Hoert: {Menge(hoert)},");
                sb.AppendLine($"            Art: \"{ArtVon(a)}\") {{ Funktionen = {Menge(funktionen.Select(t => t.ToDisplayString(fq)))} }},");
            }
            sb.AppendLine("    };");
            sb.AppendLine("}");
            context.AddSource("GeneratedAkteurRechte.g.cs", sb.ToString());

            // ── Clients (docs/konzept-akteure.md §4) ──
            var clients = schnitt.Where(IstClient).ToList();
            var akteurNamen = new HashSet<string>(akteure.Select(x => x.Name), System.StringComparer.Ordinal);
            foreach (var g in clients.GroupBy(x => Abstractions.Clientvertragsregeln.ClientName(x.Name)).Where(g => g.Count() > 1 || akteurNamen.Contains(g.Key)))
                context.ReportDiagnostic(Diagnostic.Create(NameKollision, g.First().Locations.FirstOrDefault() ?? Location.None,
                    g.Key, string.Join(", ", g.Select(x => x.ToDisplayString(fq)).Concat(akteure.Where(x => x.Name == g.Key).Select(x => x.ToDisplayString(fq))))));
            context.AddSource("GeneratedClientVertraege.g.cs", ClientTabelle(c, clients, akteure, iDarf, fq));
        }

        /// <summary>
        /// <c>GeneratedClientVertraege</c>: je <c>IClientVertrag</c> die getragenen Funktionen, Sendet/Fragt/Hört, die verkörperten Akteure und der
        /// Client-Hash (<c>Abstractions.Clientvertragsregeln.ClientKanon</c>).
        /// </summary>
        private static string ClientTabelle(Compilation c, List<INamedTypeSymbol> clients, List<INamedTypeSymbol> akteure,
            INamedTypeSymbol iDarf, SymbolDisplayFormat fq)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Client-Verträge: was über die Leitung eines Clients geht (docs/konzept-akteure.md §4).");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Akteure;");
            sb.AppendLine();
            sb.AppendLine("public static class GeneratedClientVertraege");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Client-Name (Interface ohne führendes I) → Vertrag.</summary>");
            sb.AppendLine("    public static IReadOnlyDictionary<string, ClientVertrag> Alle { get; } = new Dictionary<string, ClientVertrag>(StringComparer.Ordinal)");
            sb.AppendLine("    {");
            foreach (var cl in clients.GroupBy(x => Abstractions.Clientvertragsregeln.ClientName(x.Name)).Select(g => g.First()))
            {
                // EINE Lesart mit dem Client-Generator (Clientvertragslesart): Name, Trägt/Sendet/Fragt/Hört, Hash.
                var v = Clientvertragslesart.Lies(cl, c);
                var traegt = v.Traegt.Select(x => x.Funktion).ToList();
                // verkörpert: EINE Regel (Abstractions.Clientvertragsregeln.Verkoerpert) — eindeutige IDarf-Halter, mehrdeutige nur bei Bedarf.
                var verk = Abstractions.Clientvertragsregeln.Verkoerpert(traegt.Concat(v.Sendet).Concat(v.Fragt).Select(t =>
                    akteure.Where(a => a.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)
                                                              && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], t))).Select(a => a.Name)));

                sb.AppendLine($"        [\"{v.Name}\"] = new ClientVertrag(\"{v.Name}\", typeof({cl.ToDisplayString(fq)}), \"{v.Hash}\",");
                sb.AppendLine($"            Traegt: {Menge(traegt.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Sendet: {Menge(v.Sendet.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Fragt: {Menge(v.Fragt.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Hoert: {Menge(v.Hoert.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Verkoerpert: new[] {{ {string.Join(", ", verk.Select(x => $"\"{x}\""))} }}),");
            }
            sb.AppendLine("    };");
            sb.AppendLine("}");
            return sb.ToString();
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

        private static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? iface) => Clientvertragslesart.Implementiert(t, iface);

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
