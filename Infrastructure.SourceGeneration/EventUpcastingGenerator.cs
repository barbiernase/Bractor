using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Generiert <c>GeneratedEventUpcasting</c> — den reflection-freien, TYPISIERTEN Upcasting-Dispatch
    /// (Invariante 4) für Schema-Evolution von Events. 1:1 UND 1:N (Split).
    ///
    /// ── Das Modell ──
    /// Der Entwickler schreibt pro Schritt genau EINE <c>IUpcast&lt;TAlt, …&gt;</c>-Klasse (hart typisiert,
    /// ohne String, ohne JSON). Der Generator leitet ALLES ab:
    /// - „frühere vs. aktuelle Version" aus der Topologie: Quelle einer Kante = frühere Version (nur lesbar);
    ///   IEvent, das keine Quelle ist = aktuelle Version (schreibbar).
    /// - die KETTE aus den Typkanten; bei Split (1:N) wird jedes Ausgabe-Event weiter durch SEINE Kette gefahren.
    /// - den Speicher-DISKRIMINATOR aus der DAG-Position ENTLANG DES ERSTEN ZIELS (die „Identitäts-Fortsetzung"):
    ///   die älteste Version trägt den Basisnamen (den die alten Bytes physisch tragen → keine Migration),
    ///   jede spätere <c>_v2</c>, <c>_v3</c>, …
    ///
    /// ── Emittiert ──
    /// - <c>Materialisiere(diskriminator, json) → IReadOnlyList&lt;IEvent&gt;</c> (COPY/Raw-Pfad).
    /// - <c>Aufwerten(object) → IReadOnlyList&lt;IEvent&gt;</c> (Seam-Eintritt nach Martens Deserialisierung).
    /// - <c>Diskriminator(IEvent) → string</c> (Schreibpfad) und <c>Registrierungen</c> (Marten-MapEventType).
    ///
    /// ── Guards (Compile-Zeit, statt stiller Laufzeit-Fehler) ──
    /// CQRS040 mehrdeutige Kante, CQRS041 Kette endet nicht an aktuellem Event, CQRS042 Zyklus,
    /// CQRS044 Merge (mehrere Vorgänger), CQRS045 Split-Sekundärziel ist selbst frühere Version
    /// (mit eigener Kette — in Stufe 2 nicht unterstützt: Sekundärziele müssen aktuelle Events sein).
    /// </summary>
    [Generator]
    public class EventUpcastingGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor MehrdeutigeKante = new DiagnosticDescriptor(
            "CQRS040", "Mehrdeutige Upcast-Kante",
            "Der frühere Event-Typ '{0}' hat mehr als eine ausgehende IUpcast — eine Version darf genau eine nächste Gestalt haben.",
            "Upcasting", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor KetteOffen = new DiagnosticDescriptor(
            "CQRS041", "Upcast-Kette endet nicht an aktuellem Event",
            "Das Ziel '{0}' einer IUpcast ist weder ein aktuelles IEvent noch selbst eine frühere Version mit eigener IUpcast — die Kette erreicht keine aktuelle Gestalt.",
            "Upcasting", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor Zyklus = new DiagnosticDescriptor(
            "CQRS042", "Zyklus in der Upcast-Kette",
            "Der frühere Event-Typ '{0}' ist über die IUpcast-Kanten von sich selbst erreichbar — Upcasting muss azyklisch sein.",
            "Upcasting", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor Merge = new DiagnosticDescriptor(
            "CQRS044", "Merge in der Upcast-Kette",
            "Auf das Ziel '{0}' zeigen mehrere IUpcast-Kanten (Merge/N:1) — nicht unterstützt.",
            "Upcasting", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor SekundaerKette = new DiagnosticDescriptor(
            "CQRS045", "Split-Sekundärziel mit eigener Kette",
            "Das Split-Sekundärziel '{0}' ist selbst eine frühere Version (mit eigener IUpcast). In Stufe 2 müssen Sekundärziele eines Splits aktuelle Events sein; nur das ERSTE Ziel darf die Kette fortsetzen.",
            "Upcasting", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor SplitNichtBereit = new DiagnosticDescriptor(
            "CQRS046", "Split-Upcasting (1:N) noch nicht produktionsreif",
            "Der Split-Upcaster von '{0}' ist erkannt und wird generator-/leseseitig materialisiert, aber die Consumer-Fabric (Projektions-Dedup-Schlüssel, Prozess-Korrelation, Pipeline-Emit um SubIndex) ist noch NICHT verdrahtet — ein Split würde an durablen Konsumenten (Projektion/Reaktion/Prozess) still falsch laufen. Bis die Fabric steht: nur 1:1-Upcaster (IUpcast<TAlt, TNeu>) verwenden. Ein echter Split wird zusammen mit der Fabric und einem Integrationstest geliefert.",
            "Upcasting", DiagnosticSeverity.Error, true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            var iEvent = compilation.GetTypeByMetadataName("Abstractions.IEvent");
            if (iEvent == null) return;
            var iTransient = compilation.GetTypeByMetadataName("Abstractions.ITransientEvent");
            var iSelf = compilation.GetTypeByMetadataName("Abstractions.IPipelineSelfMessage");
            var iUpcast2 = compilation.GetTypeByMetadataName("Abstractions.IUpcast`2");
            var iUpcast3 = compilation.GetTypeByMetadataName("Abstractions.IUpcast`3");
            var iUpcast4 = compilation.GetTypeByMetadataName("Abstractions.IUpcast`4");

            var all = new List<INamedTypeSymbol>();
            Collect(compilation.GlobalNamespace, all);
            var cmp = SymbolEqualityComparer.Default;

            // ── Aktuelle, persistierbare Events (identisch zu EventJsonGenerator) ──
            var persistable = new List<INamedTypeSymbol>();
            foreach (var t in all)
            {
                var ifaces = t.AllInterfaces;
                if (!ifaces.Contains(iEvent, cmp)) continue;
                if (iTransient != null && ifaces.Contains(iTransient, cmp)) continue;
                if (iSelf != null && ifaces.Contains(iSelf, cmp)) continue;
                persistable.Add(t);
            }

            // ── IUpcast-Kanten (1:1 UND 1:N) einsammeln: edges[Quelle] = (Ziele, Wandlerklasse) ──
            var edges = new Dictionary<INamedTypeSymbol, (List<INamedTypeSymbol> targets, INamedTypeSymbol wandler)>(cmp);
            foreach (var t in all)
            {
                foreach (var iface in t.AllInterfaces)
                {
                    var def = iface.OriginalDefinition;
                    int arity;
                    if (iUpcast2 != null && cmp.Equals(def, iUpcast2)) arity = 1;
                    else if (iUpcast3 != null && cmp.Equals(def, iUpcast3)) arity = 2;
                    else if (iUpcast4 != null && cmp.Equals(def, iUpcast4)) arity = 3;
                    else continue;

                    if (!(iface.TypeArguments[0] is INamedTypeSymbol src)) continue;
                    var targets = new List<INamedTypeSymbol>();
                    var ok = true;
                    for (int i = 1; i <= arity; i++)
                    {
                        if (iface.TypeArguments[i] is INamedTypeSymbol tg) targets.Add(tg);
                        else { ok = false; break; }
                    }
                    if (!ok) continue;

                    if (edges.ContainsKey(src))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(MehrdeutigeKante, Loc(t), src.Name));
                        continue;
                    }
                    edges[src] = (targets, t);
                }
            }

            var priors = new HashSet<INamedTypeSymbol>(edges.Keys, cmp);
            var currentEvents = persistable.Where(e => !priors.Contains(e)).ToList();

            // ── GUARD (CQRS046): Split (1:N) ist generator-/leseseitig fertig, aber die Consumer-Fabric
            //    (Projektions-Dedup, Prozess-Korrelation, Pipeline-Emit) ist noch nicht verdrahtet. Ein
            //    deklarierter Split bricht daher bewusst den BUILD — kein still halb-funktionierender Split.
            //    Der Codegen unten bleibt (probe-getestet, warm für später); dieser Fehler ist das Tor. ──
            foreach (var kv in edges)
                if (kv.Value.targets.Count > 1)
                    context.ReportDiagnostic(Diagnostic.Create(SplitNichtBereit, Loc(kv.Value.wandler), kv.Key.Name));

            // ── Validierungen: Merge, Kette-offen, Split-Sekundärziel ──
            var producedBy = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(cmp);
            foreach (var kv in edges)
            {
                var targets = kv.Value.targets;
                for (int i = 0; i < targets.Count; i++)
                {
                    var tg = targets[i];
                    if (producedBy.ContainsKey(tg))
                        context.ReportDiagnostic(Diagnostic.Create(Merge, Loc(kv.Value.wandler), tg.Name));
                    else
                        producedBy[tg] = kv.Key;

                    var istAktuell = currentEvents.Contains(tg, cmp);
                    var istPrior = priors.Contains(tg);
                    if (!istAktuell && !istPrior)
                        context.ReportDiagnostic(Diagnostic.Create(KetteOffen, Loc(kv.Value.wandler), tg.Name));
                    if (i > 0 && istPrior)
                        context.ReportDiagnostic(Diagnostic.Create(SekundaerKette, Loc(kv.Value.wandler), tg.Name));
                }
            }

            // ── Zyklus-Erkennung (allen Zielen folgen) ──
            foreach (var start in edges.Keys)
            {
                var seen = new HashSet<INamedTypeSymbol>(cmp);
                var stack = new Stack<INamedTypeSymbol>();
                stack.Push(start);
                var guard = 0;
                var cyc = false;
                while (stack.Count > 0 && guard++ < 8192)
                {
                    var cur = stack.Pop();
                    if (!edges.TryGetValue(cur, out var e2)) continue;
                    foreach (var tg in e2.targets)
                    {
                        if (cmp.Equals(tg, start)) { cyc = true; break; }
                        if (seen.Add(tg)) stack.Push(tg);
                    }
                    if (cyc) break;
                }
                if (cyc)
                    context.ReportDiagnostic(Diagnostic.Create(Zyklus, Loc(start), start.Name));
            }

            // ── Diskriminator-Lineage ENTLANG DES ERSTEN ZIELS ──
            var firstPrev = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(cmp); // erstesZiel -> Quelle
            foreach (var kv in edges)
            {
                var first = kv.Value.targets[0];
                if (!firstPrev.ContainsKey(first)) firstPrev[first] = kv.Key;
            }
            var disc = new Dictionary<INamedTypeSymbol, string>(cmp);
            currentEvents.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var e in currentEvents)
            {
                var chain = new List<INamedTypeSymbol> { e };
                var cur = e; var g = 0;
                while (firstPrev.TryGetValue(cur, out var p) && g++ < 4096) { chain.Insert(0, p); cur = p; }
                var baseName = Snake(e.Name);
                for (int i = 0; i < chain.Count; i++)
                    disc[chain[i]] = i == 0 ? baseName : baseName + "_v" + (i + 1);
            }

            // Methodennamen je frühere Version (für Rekursion vorab vergeben)
            var method = new Dictionary<INamedTypeSymbol, string>(cmp);
            {
                var idx = 0;
                foreach (var p in edges.Keys.OrderBy(x => x.Name, System.StringComparer.Ordinal))
                    method[p] = "AufwertenKette_" + (idx++) + "_" + Ident(p.Name);
            }

            var full = SymbolDisplayFormat.FullyQualifiedFormat;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using Abstractions;");
            sb.AppendLine("using Ctx = global::Infrastructure.Serialization.EventJsonSerializerContext;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Serialization;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Reflection-freier, typisierter Upcasting-Dispatch (Invariante 4). 1:1 und 1:N (Split).");
            sb.AppendLine("/// Generiert vom EventUpcastingGenerator. Diskriminator = DAG-Position entlang des ersten Ziels.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static class GeneratedEventUpcasting");
            sb.AppendLine("{");

            // ── Materialisiere: (Diskriminator, JSON) -> aktuelle Event(s) ──
            sb.AppendLine("    /// <summary>Liest die (alten) Bytes in den zugehörigen typisierten Record und fährt die Kette bis zur aktuellen Gestalt (Split → mehrere).</summary>");
            sb.AppendLine("    public static IReadOnlyList<IEvent> Materialisiere(string diskriminator, string json) => diskriminator switch");
            sb.AppendLine("    {");
            foreach (var p in edges.Keys.OrderBy(x => disc.TryGetValue(x, out var d) ? d : x.Name, System.StringComparer.Ordinal))
                sb.AppendLine($"        \"{disc[p]}\" => {method[p]}(JsonSerializer.Deserialize(json, Ctx.Default.{p.Name})!),");
            foreach (var e in currentEvents)
                sb.AppendLine($"        \"{disc[e]}\" => new IEvent[] {{ JsonSerializer.Deserialize(json, Ctx.Default.{e.Name})! }},");
            sb.AppendLine("        _ => throw new NotSupportedException($\"Unbekannter Event-Diskriminator beim Upcasting: {diskriminator}\")");
            sb.AppendLine("    };");
            sb.AppendLine();

            // ── Aufwerten: (von Marten deserialisiertes) Objekt -> aktuelle Event(s) ──
            sb.AppendLine("    /// <summary>Seam-Eintritt: wertet ein von Marten deserialisiertes Event-Objekt (frühere ODER aktuelle Version) auf die aktuelle(n) Gestalt(en) auf.</summary>");
            sb.AppendLine("    public static IReadOnlyList<IEvent> Aufwerten(object data) => data switch");
            sb.AppendLine("    {");
            foreach (var p in edges.Keys.OrderBy(x => x.Name, System.StringComparer.Ordinal))
                sb.AppendLine($"        {p.ToDisplayString(full)} v => {method[p]}(v),");
            sb.AppendLine("        IEvent e => new IEvent[] { e },   // aktuelle Gestalt: unverändert");
            sb.AppendLine("        _ => throw new NotSupportedException($\"Kein Upcasting-Pfad für {data?.GetType().FullName ?? \"null\"}\")");
            sb.AppendLine("    };");
            sb.AppendLine();

            // ── Diskriminator der AKTUELLEN Gestalt (Schreibpfad) ──
            sb.AppendLine("    /// <summary>Der Speicher-Diskriminator der AKTUELLEN Gestalt eines Events (Schreibpfad).</summary>");
            sb.AppendLine("    public static string Diskriminator(IEvent evt) => evt switch");
            sb.AppendLine("    {");
            foreach (var e in currentEvents)
                sb.AppendLine($"        {e.ToDisplayString(full)} => \"{disc[e]}\",");
            sb.AppendLine("        _ => throw new NotSupportedException($\"Kein Upcasting-Diskriminator für {evt.GetType().FullName}\")");
            sb.AppendLine("    };");
            sb.AppendLine();

            // ── Registrierungen: (CLR-Typ, Speicher-Diskriminator) für Martens MapEventType ──
            sb.AppendLine("    /// <summary>(CLR-Typ, Speicher-Diskriminator) für die Marten-Registrierung — inkl. früherer Versionen (nur lesbar).</summary>");
            sb.AppendLine("    public static readonly IReadOnlyList<(Type Typ, string Diskriminator)> Registrierungen = new (Type, string)[]");
            sb.AppendLine("    {");
            foreach (var kvp in disc.OrderBy(x => x.Value, System.StringComparer.Ordinal))
                sb.AppendLine($"        (typeof({kvp.Key.ToDisplayString(full)}), \"{kvp.Value}\"),");
            sb.AppendLine("    };");
            sb.AppendLine();

            // ── Ketten-Hilfsmethoden (rekursiv; Split flacht die Ausgaben) ──
            foreach (var p in edges.Keys.OrderBy(x => x.Name, System.StringComparer.Ordinal))
            {
                var (targets, wandler) = edges[p];
                sb.AppendLine($"    private static IReadOnlyList<IEvent> {method[p]}({p.ToDisplayString(full)} v)");
                sb.AppendLine("    {");
                if (targets.Count == 1)
                {
                    var t0 = targets[0];
                    if (priors.Contains(t0))
                        sb.AppendLine($"        return {method[t0]}(new {wandler.ToDisplayString(full)}().Wandle(v));");
                    else
                        sb.AppendLine($"        return new IEvent[] {{ new {wandler.ToDisplayString(full)}().Wandle(v) }};");
                }
                else
                {
                    sb.AppendLine($"        var t = new {wandler.ToDisplayString(full)}().Wandle(v);");
                    sb.AppendLine("        var liste = new List<IEvent>();");
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var tg = targets[i];
                        var item = "t.Item" + (i + 1);
                        if (i == 0 && priors.Contains(tg))
                            sb.AppendLine($"        liste.AddRange({method[tg]}({item}));");
                        else
                            sb.AppendLine($"        liste.Add({item});");
                    }
                    sb.AppendLine("        return liste;");
                }
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");

            context.AddSource("GeneratedEventUpcasting.g.cs", sb.ToString());
        }

        private static Location Loc(ISymbol s)
            => s.Locations.FirstOrDefault() ?? Location.None;

        private static void Collect(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var t in ns.GetTypeMembers())
                if (t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsStatic)
                    results.Add(t);
            foreach (var sub in ns.GetNamespaceMembers())
                Collect(sub, results);
        }

        private static string Ident(string name)
        {
            var s = Sanitize(name);
            var sb = new StringBuilder();
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        private static string Snake(string name)
        {
            var s = Sanitize(name);
            if (string.IsNullOrWhiteSpace(s)) return s;
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(s[0]));
            for (int i = 1; i < s.Length; i++)
            {
                var c = s[i];
                if (char.IsUpper(c)) { sb.Append('_'); sb.Append(char.ToLowerInvariant(c)); }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
                .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue")
                .Replace("ß", "ss");
        }
    }
}
