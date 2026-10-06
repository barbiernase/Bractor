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
    /// <para><b>Vertrag</b> (<c>IAkteurVertrag&lt;A&gt;</c>, docs/konzept-akteure.md §3): je <c>Auf(TEvent)</c> die erlaubten Ausgaben.
    /// Hat ein Akteur einen Vertrag, gilt <b>Befugt = IDarf ∪ Ausgaben(Vertrag)</b> und <b>Hört = Eingänge(Vertrag) ∪ (2)</b> — exakt
    /// das, worauf er reagiert, statt der groben Regel (1). Dazu der Vertrags-Hash (<c>Abstractions.Akteurvertrag</c>), gegen den
    /// der Handshake prüft.</para>
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
            var iClient = c.GetTypeByMetadataName("Abstractions.IClientVertrag");
            var schnitt = new List<INamedTypeSymbol>();
            CollectInterfaces(c.GlobalNamespace, schnitt);
            schnitt = schnitt.OrderBy(x => x.ToDisplayString(), System.StringComparer.Ordinal).ToList();
            bool IstClient(INamedTypeSymbol t) => iClient != null && t.AllInterfaces.Contains(iClient, SymbolEqualityComparer.Default);
            // Akteur → seine Vertrags-TEILE (Interfaces mit IAkteurVertrag<A>, ohne Client-Verträge, die Teile nur erben). Der ganze
            //   Vertrag ist die Vereinigung ihrer Auf-Methoden; je Event höchstens eine (CQRS061).
            var teile = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            if (iVertrag != null)
                foreach (var v in schnitt.Where(x => !IstClient(x)))
                    foreach (var i in v.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iVertrag)))
                        if (i.TypeArguments[0] is INamedTypeSymbol akt)
                        {
                            if (!teile.TryGetValue(akt, out var l)) teile[akt] = l = new List<INamedTypeSymbol>();
                            if (!l.Contains(v, SymbolEqualityComparer.Default)) l.Add(v);
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
            //   (IAkteurDienst<A>, docs/konzept-akteure.md §2); Interfaces zählen daher nicht.
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

                // Vertrag: je Auf(Event) die Ausgaben (konkrete Commands; die Form prüft CQRS061/062) — über ALLE Teile des Akteurs.
                teile.TryGetValue(a, out var aTeile);
                var zusagen = Zusagen(aTeile ?? new List<INamedTypeSymbol>(), iCommand);
                // Der Vertrags-Typ (Handshake per Akteur-Name, Übergang): der Teil, der alle Zusagen sieht (eigene + geerbte), sonst der erste.
                var vertrag = aTeile == null ? null
                    : aTeile.FirstOrDefault(t => Zusagen(new List<INamedTypeSymbol> { t }.Concat(t.AllInterfaces.Where(x => aTeile.Contains(x, SymbolEqualityComparer.Default))).ToList(), iCommand).Count == zusagen.Count)
                      ?? aTeile[0];
                foreach (var t in zusagen.SelectMany(r => r.Aus))
                    if (!cmds.Contains(t, SymbolEqualityComparer.Default)) cmds.Add(t);   // Befugt = IDarf ∪ Ausgaben(Vertrag)

                var hoert = new SortedSet<string>(System.StringComparer.Ordinal);
                if (vertrag != null)
                    foreach (var r in zusagen) hoert.Add(r.Ein.ToDisplayString(fq));   // exakt: worauf er reagiert
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
                var kanon = Abstractions.Akteurvertrag.Kanon(a.Name, zusagen.Select(r =>
                    new Abstractions.Akteurvertrag.Zusage(r.Ein.Name, r.Aus.Select(x => x.Name).ToList(), r.Strom)));
                sb.AppendLine($"            Art: \"{ArtVon(a)}\")");
                sb.AppendLine("        {");
                sb.AppendLine($"            VertragTyp = typeof({vertrag.ToDisplayString(fq)}),");
                sb.AppendLine($"            VertragHash = \"{Abstractions.Akteurvertrag.Hash(kanon)}\",");
                sb.AppendLine("            Vertrag = new Dictionary<Type, IReadOnlySet<Type>>");
                sb.AppendLine("            {");
                foreach (var r in zusagen.OrderBy(r => r.Ein.Name, System.StringComparer.Ordinal))
                    sb.AppendLine($"                [typeof({r.Ein.ToDisplayString(fq)})] = {Menge(r.Aus.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine("            },");
                sb.AppendLine($"            Stroeme = {Menge(zusagen.Where(r => r.Strom).Select(r => r.Ein.ToDisplayString(fq)))},");
                sb.AppendLine("        },");
            }
            sb.AppendLine("    };");
            sb.AppendLine("}");
            context.AddSource("GeneratedAkteurRechte.g.cs", sb.ToString());

            // ── Clients (docs/konzept-akteure.md §4) ──
            var clients = schnitt.Where(IstClient).ToList();
            var akteurNamen = new HashSet<string>(akteure.Select(x => x.Name), System.StringComparer.Ordinal);
            foreach (var g in clients.GroupBy(x => Abstractions.Akteurvertrag.ClientName(x.Name)).Where(g => g.Count() > 1 || akteurNamen.Contains(g.Key)))
                context.ReportDiagnostic(Diagnostic.Create(NameKollision, g.First().Locations.FirstOrDefault() ?? Location.None,
                    g.Key, string.Join(", ", g.Select(x => x.ToDisplayString(fq)).Concat(akteure.Where(x => x.Name == g.Key).Select(x => x.ToDisplayString(fq))))));
            context.AddSource("GeneratedClientVertraege.g.cs", ClientTabelle(clients, akteure, teile, iDarf, iCommand, iEvent, iTransient, fq));
        }

        /// <summary>
        /// <c>GeneratedClientVertraege</c>: je <c>IClientVertrag</c> die getragenen Zusagen je Akteur, Sendet/Fragt/Kenntnis, die
        /// verkörperten Akteure und der Client-Hash (<c>Abstractions.Akteurvertrag.ClientKanon</c>).
        /// </summary>
        private static string ClientTabelle(List<INamedTypeSymbol> clients, List<INamedTypeSymbol> akteure,
            Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> teile, INamedTypeSymbol iDarf, INamedTypeSymbol? iCommand,
            INamedTypeSymbol? iEvent, INamedTypeSymbol? iTransient, SymbolDisplayFormat fq)
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
            var teilZuAkteur = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var kv in teile) foreach (var t in kv.Value) teilZuAkteur[t] = kv.Key;
            foreach (var cl in clients.GroupBy(x => Abstractions.Akteurvertrag.ClientName(x.Name)).Select(g => g.First()))
            {
                var name = Abstractions.Akteurvertrag.ClientName(cl.Name);
                // getragene Teile (auch über andere Client-Verträge geerbt) → je Akteur die Zusagen
                var getragen = cl.AllInterfaces.Where(i => teilZuAkteur.ContainsKey(i)).ToList();
                var jeAkteur = getragen.GroupBy(t => teilZuAkteur[t], SymbolEqualityComparer.Default)
                    .Select(g => (Akteur: (INamedTypeSymbol)g.Key!, Zusagen: Zusagen(g.ToList(), iCommand)))
                    .OrderBy(x => x.Akteur.Name, System.StringComparer.Ordinal).ToList();
                var basen = new[] { cl }.Concat(cl.AllInterfaces.Where(i => IstClientTyp(i))).ToList();
                bool IstClientTyp(INamedTypeSymbol i) => clients.Contains(i, SymbolEqualityComparer.Default);
                List<INamedTypeSymbol> Generisch(string meta) => basen.SelectMany(b => b.Interfaces)
                    .Where(i => i.OriginalDefinition.ToDisplayString().StartsWith(meta, System.StringComparison.Ordinal) && i.TypeArguments.Length == 1)
                    .Select(i => i.TypeArguments[0]).OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
                var sendet = Generisch("Abstractions.ISendet<");
                var fragt = Generisch("Abstractions.IFragt<");
                var kenntnis = basen.SelectMany(b => b.GetMembers(Abstractions.Akteurvertrag.Auf).OfType<IMethodSymbol>())
                    .Where(m => m.Parameters.Length == 1).Select(m => m.Parameters[0].Type).OfType<INamedTypeSymbol>()
                    .Where(e => Implementiert(e, iEvent) || Implementiert(e, iTransient)).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
                // verkörpert = Akteure der getragenen Teile ∪ je Sendet/Fragt die IDarf-Halter — aber nur, wenn keiner der Teil-Akteure den
                //   Typ schon darf (der KlassifikationsWorker fragt HoleAktivesModell als Klassifizierer, nicht zusätzlich als Modellfreigeber).
                var basis = new HashSet<string>(jeAkteur.Select(x => x.Akteur.Name), System.StringComparer.Ordinal);
                var verk = new SortedSet<string>(basis, System.StringComparer.Ordinal);
                foreach (var t in sendet.Concat(fragt))
                {
                    var halter = akteure.Where(a => a.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)
                                                                          && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], t))).Select(a => a.Name).ToList();
                    if (!halter.Any(basis.Contains)) verk.UnionWith(halter);
                }
                var kanon = Abstractions.Akteurvertrag.ClientKanon(name,
                    jeAkteur.Select(x => new KeyValuePair<string, IEnumerable<Abstractions.Akteurvertrag.Zusage>>(x.Akteur.Name,
                        x.Zusagen.Select(r => new Abstractions.Akteurvertrag.Zusage(r.Ein.Name, r.Aus.Select(y => y.Name).ToList(), r.Strom)))),
                    sendet.Select(t => t.Name), fragt.Select(t => t.Name), kenntnis.Select(t => t.Name));

                sb.AppendLine($"        [\"{name}\"] = new ClientVertrag(\"{name}\", typeof({cl.ToDisplayString(fq)}), \"{Abstractions.Akteurvertrag.Hash(kanon)}\",");
                sb.AppendLine("            Traegt: new Dictionary<string, IReadOnlyDictionary<Type, IReadOnlySet<Type>>>(StringComparer.Ordinal)");
                sb.AppendLine("            {");
                foreach (var (akt, rs) in jeAkteur)
                {
                    sb.AppendLine($"                [\"{akt.Name}\"] = new Dictionary<Type, IReadOnlySet<Type>>");
                    sb.AppendLine("                {");
                    foreach (var r in rs.OrderBy(r => r.Ein.Name, System.StringComparer.Ordinal))
                        sb.AppendLine($"                    [typeof({r.Ein.ToDisplayString(fq)})] = {Menge(r.Aus.Select(t => t.ToDisplayString(fq)))},");
                    sb.AppendLine("                },");
                }
                sb.AppendLine("            },");
                sb.AppendLine($"            Stroeme: {Menge(jeAkteur.SelectMany(x => x.Zusagen).Where(r => r.Strom).Select(r => r.Ein.ToDisplayString(fq)))},");
                sb.AppendLine($"            Sendet: {Menge(sendet.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Fragt: {Menge(fragt.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Kenntnis: {Menge(kenntnis.Select(t => t.ToDisplayString(fq)))},");
                sb.AppendLine($"            Verkoerpert: new[] {{ {string.Join(", ", verk.Select(v => $"\"{v}\""))} }}),");
            }
            sb.AppendLine("    };");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Die Zusagen aus den Auf-Methoden der Teile (eigene Members, je Methode einmal), Ausgaben = konkrete Commands.</summary>
        private static List<(INamedTypeSymbol Ein, List<INamedTypeSymbol> Aus, bool Strom)> Zusagen(List<INamedTypeSymbol> teile, INamedTypeSymbol? iCommand)
        {
            var liste = new List<(INamedTypeSymbol Ein, List<INamedTypeSymbol> Aus, bool Strom)>();
            var gesehen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var t in teile)
                foreach (var m in t.GetMembers(Abstractions.Akteurvertrag.Auf).OfType<IMethodSymbol>())
                    if (m.Parameters.Length == 1 && m.Parameters[0].Type is INamedTypeSymbol ein && gesehen.Add(ein))
                    {
                        var aus = VertragsAusgaben(m.ReturnType, out var strom).Where(x => Implementiert(x, iCommand)).ToList();
                        liste.Add((ein, aus, strom));
                    }
            return liste;
        }

        private static string Menge(IEnumerable<string> typen)
        {
            var liste = typen.OrderBy(x => x, System.StringComparer.Ordinal).ToList();
            return liste.Count == 0
                ? "new HashSet<Type>()"
                : "new HashSet<Type> { " + string.Join(", ", liste.Select(t => $"typeof({t})")) + " }";
        }

        /// <summary>Ausgaben einer Zusage: void → keine; T/OneOf&lt;…&gt; → eine; (Async)Enumerable davon → Strom.</summary>
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
