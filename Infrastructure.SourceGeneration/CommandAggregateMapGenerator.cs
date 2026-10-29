// In Projekt: Infrastructure.SourceGeneration
// Dateiname: CommandAggregateMapGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Das Command→Aggregat-Routing aus dem TYP des Commands (docs/10 §10.4, R3): ein Command ist <c>ICommand&lt;TAggregat&gt;</c>, sein Ziel steht
    /// im Typ-Argument — Routing über Typen (Inv. 3), ohne Konvention. Die Decide-Signaturen liefern die Kanten Command → Events
    /// (<c>CommandToEvents</c>) und müssen im Ziel-Aggregat stehen: ein <c>Decide(TCommand)</c> in einem anderen Aggregat bricht den Build
    /// (CQRS010). Ein Command ohne typisiertes Ziel meldet CQRS013 (<c>ModellAnalyzer</c>), einer ohne Decide CQRS086.
    ///
    /// Darüber routen Dispatcher, Leitung und Emitter (<c>Anwendung.Routing.CommandToAggregate</c>).
    /// </summary>
    [Generator]
    public class CommandAggregateMapGenerator : ISourceGenerator
    {
        private const string IDeciderMetadataName = "Abstractions.IDecider`1";
        private const string ICommandFullName = "Abstractions.ICommand";
        private const string ICommandZielName = Abstractions.Idvertrag.CommandMetadatenName;
        private const string IEventFullName = "Abstractions.IEvent";
        private const string ITransientEventFullName = "Abstractions.ITransientEvent";

        // R3: ein Command gehört genau dem Aggregat in seinem Typ — ein Decide anderswo bräche das Routing still (der Command käme nie dort an).
        private static readonly DiagnosticDescriptor CommandKollision = new DiagnosticDescriptor(
            id: "CQRS010",
            title: "Command außerhalb seines Ziel-Aggregats entschieden",
            messageFormat: "Command '{0}' gehört laut ICommand<{1}> zu {1}, wird aber in {2} entschieden — ein Command gehört genau EINEM "
                + "Aggregat, dem in seinem Typ; das Decide gehört dorthin.",
            category: "Cqrs",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // ★ P1c (TG-3): zwei verschiedene Aggregate, die auf DIESELBE Identität auflösen (gleicher einfacher
        //   Typname in verschiedenen Namespaces, ohne unterscheidendes [AggregatName]), würden denselben
        //   ClusterKind/aggregate_type belegen → zur Laufzeit falsch geroutet. Fail-fast am Build statt still.
        private static readonly DiagnosticDescriptor AggregatKollision = new DiagnosticDescriptor(
            id: "CQRS011",
            title: "Zwei Aggregate mit gleicher Identität",
            messageFormat: "Die Aggregat-Identität '{0}' ist mehrdeutig — sie wird von mehreren State-Typen belegt ({1}). "
                + "Zwei Aggregate mit gleichem Namen kollidieren im ClusterKind/aggregate_type. Gib mindestens einem ein "
                + "explizites [AggregatName(\"...\")], um sie zu unterscheiden.",
            category: "Cqrs",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!Laufzeitprojekt.Ist(context.Compilation)) return;   // nur in der Komposition (docs/07)
            var compilation = context.Compilation;
            var iDecider = compilation.GetTypeByMetadataName(IDeciderMetadataName);
            var iCommand = compilation.GetTypeByMetadataName(ICommandFullName);
            var iCommandZiel = compilation.GetTypeByMetadataName(ICommandZielName);
            if (iDecider == null || iCommand == null || iCommandZiel == null)
                return;
            var iEvent = compilation.GetTypeByMetadataName(IEventFullName);
            var iTransient = compilation.GetTypeByMetadataName(ITransientEventFullName);

            // Command-Symbol → sein Ziel-Aggregat (Identität), aus ICommand<TAggregat>.
            var map = new Dictionary<INamedTypeSymbol, SortedSet<string>>(SymbolEqualityComparer.Default);
            // Command-Symbol → produzierte PERSISTIERTE Event-Typen (FQN), aus den Decide-OneOf-Rückgabetypen.
            // Grundlage des Azyklizitäts-Boot-Guards (präzise Command→Event-Kanten statt aggregat-grob).
            var events = new Dictionary<INamedTypeSymbol, SortedSet<string>>(SymbolEqualityComparer.Default);
            // ★ P1c (TG-3): aufgelöste Aggregat-Identität → die State-Typen, die sie belegen (Kollisionserkennung).
            var identitätZuStates = new Dictionary<string, HashSet<INamedTypeSymbol>>(System.StringComparer.Ordinal);
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            // Das Ziel jedes Commands aus seinem Typ: ICommand<TAggregat> → TAggregat.
            foreach (var type in allTypes)
                if (Ziel(type, iCommandZiel) is { } ziel)
                    map[type] = new SortedSet<string>(System.StringComparer.Ordinal) { AggregatIdentität(ziel) };

            foreach (var type in allTypes)
            {
                // Das Interface IDecider<TState> unter den implementierten Interfaces finden → TState.
                var deciderIface = type.AllInterfaces.FirstOrDefault(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDecider));
                if (deciderIface == null || deciderIface.TypeArguments.Length != 1)
                    continue;

                // ★ P1c (TG-3): Identität aus [AggregatName] (falls vorhanden), sonst der einfache Typname
                //   (Default → keine Migration). Kollisionen erkennen wir über die belegenden State-Typen.
                if (deciderIface.TypeArguments[0] is not INamedTypeSymbol stateSymbol)
                    continue;
                var stateName = AggregatIdentität(stateSymbol);

                if (!identitätZuStates.TryGetValue(stateName, out var states))
                    identitätZuStates[stateName] = states = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                states.Add(stateSymbol);

                // Alle Decide(TCommand)-Methoden dieses Deciders.
                foreach (var method in type.GetMembers(Abstractions.Aggregatvertrag.Decide).OfType<IMethodSymbol>())
                {
                    if (method.Parameters.Length < 1)
                        continue;
                    if (method.Parameters[0].Type is not INamedTypeSymbol cmd)
                        continue;
                    if (!cmd.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default))
                        continue;

                    // Das Decide muss im Ziel-Aggregat stehen (CQRS010); ohne typisiertes Ziel meldet CQRS013 im Domänen-Projekt.
                    var ziel = Ziel(cmd, iCommandZiel);
                    if (ziel is null)
                        continue;
                    if (!SymbolEqualityComparer.Default.Equals(ziel, stateSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(CommandKollision,
                            method.Locations.FirstOrDefault() ?? Location.None, cmd.Name, ziel.Name, stateSymbol.Name));
                        continue;
                    }

                    if (!events.TryGetValue(cmd, out var evSet))
                        events[cmd] = evSet = new SortedSet<string>(System.StringComparer.Ordinal);
                    foreach (var ev in ProduzierteEvents(method.ReturnType, iEvent, iTransient, fq))
                        evSet.Add(ev);
                }
            }

            // ★ P1c (TG-3): zwei verschiedene State-Typen mit derselben aufgelösten Identität → Build-Fehler.
            foreach (var kv in identitätZuStates.Where(k => k.Value.Count > 1))
                context.ReportDiagnostic(Diagnostic.Create(
                    AggregatKollision,
                    kv.Value.First().Locations.FirstOrDefault() ?? Location.None,
                    kv.Key, string.Join(", ", kv.Value.Select(s => s.ToDisplayString(fq)))));

            context.AddSource("GeneratedCommandRouting.g.cs", Emit(map, events, fq));
        }

        /// <summary>Das Ziel-Aggregat eines Commands aus <c>ICommand&lt;TAggregat&gt;</c>, oder null (kein Command mit typisiertem Ziel).</summary>
        private static INamedTypeSymbol? Ziel(INamedTypeSymbol cmd, INamedTypeSymbol iCommandZiel)
        {
            var ziele = cmd.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iCommandZiel)).ToList();
            return ziele.Count == 1 ? ziele[0].TypeArguments[0] as INamedTypeSymbol : null;
        }

        /// <summary>
        /// ★ P1c (TG-3): die Aggregat-Identität — <c>[AggregatName("…")]</c> falls gesetzt, sonst der einfache
        /// Typname des State (Default, keine Migration).
        /// </summary>
        internal static string AggregatIdentität(INamedTypeSymbol state)
        {
            var attr = state.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "Abstractions.AggregatNameAttribute");
            if (attr != null && attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string s && !string.IsNullOrWhiteSpace(s))
                return s;
            return state.Name;
        }

        /// <summary>
        /// Extrahiert aus einem Decide-Rückgabetyp <c>IEnumerable&lt;OneOf&lt;E1,E2,…&gt;&gt;</c> (oder
        /// <c>IEnumerable&lt;E&gt;</c>) die PERSISTIERTEN Event-Typen (implementieren IEvent, aber NICHT
        /// ITransientEvent — Ablehnungen werden nicht persistiert und können keine Regel triggern).
        /// </summary>
        private static IEnumerable<string> ProduzierteEvents(
            ITypeSymbol returnType, INamedTypeSymbol iEvent, INamedTypeSymbol iTransient, SymbolDisplayFormat fq)
        {
            if (iEvent == null) yield break;
            if (returnType is not INamedTypeSymbol enumerable || enumerable.TypeArguments.Length != 1) yield break;
            if (enumerable.TypeArguments[0] is not INamedTypeSymbol element) yield break;

            // Element ist entweder OneOf<…> (dann die Typargumente) oder direkt ein Event (bare IEnumerable<E>).
            var kandidaten = element.TypeArguments.Length > 0
                ? element.TypeArguments
                : ImmutableArrayOf(element);

            foreach (var ta in kandidaten)
            {
                if (ta is not INamedTypeSymbol et) continue;
                var istEvent = et.AllInterfaces.Contains(iEvent, SymbolEqualityComparer.Default);
                var istTransient = iTransient != null && et.AllInterfaces.Contains(iTransient, SymbolEqualityComparer.Default);
                if (istEvent && !istTransient)
                    yield return et.ToDisplayString(fq);
            }
        }

        private static System.Collections.Immutable.ImmutableArray<ITypeSymbol> ImmutableArrayOf(ITypeSymbol t)
            => System.Collections.Immutable.ImmutableArray.Create(t);

        // Rekursiv ALLE Typen inkl. verschachtelter (Decider sind nested: z.B. Lager.Decider).
        private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var type in ns.GetTypeMembers())
                CollectNested(type, results);
            foreach (var sub in ns.GetNamespaceMembers())
                CollectTypes(sub, results);
        }

        private static void CollectNested(INamedTypeSymbol type, List<INamedTypeSymbol> results)
        {
            if ((type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) && !type.IsAbstract)
                results.Add(type);
            foreach (var nested in type.GetTypeMembers())
                CollectNested(nested, results);
        }

        private static string Emit(
            Dictionary<INamedTypeSymbol, SortedSet<string>> map,
            Dictionary<INamedTypeSymbol, SortedSet<string>> events,
            SymbolDisplayFormat fq)
        {
            var eindeutig = map.ToList();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Command→Aggregat-Routing aus dem Typ (ICommand<TAggregat>) und Command→Event-Abbildung aus den Decide-Signaturen.");
            sb.AppendLine($"// {eindeutig.Count} Commands.");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Mapping;");
            sb.AppendLine();
            sb.AppendLine("public static class GeneratedCommandRouting");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Command-Typ → Aggregat-Name (Cluster-Kind), aus ICommand<TAggregat>.</summary>");
            sb.AppendLine("    [global::Abstractions.RoutingTabelle(global::Abstractions.RoutingArt.CommandZuAggregat)]");
            sb.AppendLine("    public static IReadOnlyDictionary<Type, string> CommandToAggregate { get; } =");
            sb.AppendLine("        new Dictionary<Type, string>");
            sb.AppendLine("        {");

            foreach (var kv in eindeutig.OrderBy(k => k.Key.Name, System.StringComparer.Ordinal))
                sb.AppendLine($"            [typeof({kv.Key.ToDisplayString(fq)})] = \"{kv.Value.First()}\",");

            sb.AppendLine("        };");
            sb.AppendLine();

            // ★ Azyklizität-Grundlage: Command → produzierte (persistierte) Event-Typen, präzise aus den Decidern.
            sb.AppendLine("    /// <summary>Command-Typ → produzierte persistierte Event-Typen (aus den Decide-OneOf-Rückgaben).</summary>");
            sb.AppendLine("    [global::Abstractions.RoutingTabelle(global::Abstractions.RoutingArt.CommandZuEvents)]");
            sb.AppendLine("    public static IReadOnlyDictionary<Type, IReadOnlyList<Type>> CommandToEvents { get; } =");
            sb.AppendLine("        new Dictionary<Type, IReadOnlyList<Type>>");
            sb.AppendLine("        {");

            foreach (var kv in events.Where(e => e.Value.Count > 0).OrderBy(k => k.Key.Name, System.StringComparer.Ordinal))
            {
                var typen = string.Join(", ", kv.Value.Select(e => $"typeof({e})"));
                sb.AppendLine($"            [typeof({kv.Key.ToDisplayString(fq)})] = new Type[] {{ {typen} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Die produzierten Events eines Commands (leer, wenn unbekannt) — für den Azyklizitäts-Guard.</summary>");
            sb.AppendLine("    public static IEnumerable<Type> Produziert(Type command)");
            sb.AppendLine("        => CommandToEvents.TryGetValue(command, out var evts) ? evts : Array.Empty<Type>();");

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
