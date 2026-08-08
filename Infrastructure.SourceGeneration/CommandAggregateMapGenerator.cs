// In Projekt: Infrastructure.SourceGeneration
// Dateiname: CommandAggregateMapGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Wurzel-2-Fix, Schritt 1: PRÄZISES Command→Aggregat-Routing aus den DECIDER-SIGNATUREN
    /// statt aus der Namespace-Konvention (die <see cref="PipelineActorGenerator"/> heute nutzt).
    ///
    /// Für jede <c>IDecider&lt;TState&gt;</c>-Implementierung wird jede <c>Decide(TCommand)</c>-Methode
    /// gelesen: <c>TCommand</c> gehört zu <c>TState</c> (dem Aggregat, das den Command WIRKLICH behandelt).
    /// Strukturell garantiert statt konventions-abhängig — der stille Falsch-Route und das
    /// „kein Mapping → stiller Drop" (F7) fallen weg, weil das Ziel aus der tatsächlichen Behandlung folgt.
    ///
    /// STAND: VERDRAHTET — dies ist die Routing-Quelle. <c>GeneratedPipelines.CommandAggregateTypes</c> ist ein
    /// Passthrough hierauf; damit routen <c>HandlerOutputRouter</c>, <c>ProzessManagerActor</c>,
    /// <c>AggregateDispatcherExtensions</c> und die generierte <c>PipelineActorBase</c>-Override präzise (aus den
    /// Decidern), nicht mehr namespace-basiert. Der alte Namespace-Algorithmus im PipelineActorGenerator ist entfernt.
    /// Verifiziert byte-genau identisch zur Namespace-Map (34/34) — reine Substrat-Umschaltung, Verhalten unverändert.
    /// </summary>
    [Generator]
    public class CommandAggregateMapGenerator : ISourceGenerator
    {
        private const string IDeciderMetadataName = "Abstractions.IDecider`1";
        private const string ICommandFullName = "Abstractions.ICommand";
        private const string IEventFullName = "Abstractions.IEvent";
        private const string ITransientEventFullName = "Abstractions.ITransientEvent";

        // ★ Audit-Fix H4 (fail-fast): ein Command, den mehr als ein Aggregat behandelt, fällt aus dem
        //   eindeutigen Routing (CommandToAggregate) → als Reaktion verpufft er still, als Prozess-Send hängt
        //   der Prozess. Das war bisher nur ein Kommentar im Generat; jetzt bricht es den Build.
        private static readonly DiagnosticDescriptor CommandKollision = new DiagnosticDescriptor(
            id: "CQRS010",
            title: "Command von mehreren Aggregaten behandelt",
            messageFormat: "Command '{0}' wird von mehreren Aggregaten behandelt ({1}) — er fällt aus dem eindeutigen "
                + "Routing (CommandToAggregate) und würde als Reaktion still verpuffen bzw. einen Prozess hängen lassen. "
                + "Jeder Command darf von genau EINEM Aggregat (Decider) behandelt werden.",
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
            var compilation = context.Compilation;
            var iDecider = compilation.GetTypeByMetadataName(IDeciderMetadataName);
            var iCommand = compilation.GetTypeByMetadataName(ICommandFullName);
            if (iDecider == null || iCommand == null)
                return;
            var iEvent = compilation.GetTypeByMetadataName(IEventFullName);
            var iTransient = compilation.GetTypeByMetadataName(ITransientEventFullName);

            // Command-Symbol → Menge der behandelnden Aggregat-Namen (Menge, um Kollisionen zu erkennen).
            var map = new Dictionary<INamedTypeSymbol, SortedSet<string>>(SymbolEqualityComparer.Default);
            // Command-Symbol → produzierte PERSISTIERTE Event-Typen (FQN), aus den Decide-OneOf-Rückgabetypen.
            // Grundlage des Azyklizitäts-Boot-Guards (präzise Command→Event-Kanten statt aggregat-grob).
            var events = new Dictionary<INamedTypeSymbol, SortedSet<string>>(SymbolEqualityComparer.Default);
            // ★ P1c (TG-3): aufgelöste Aggregat-Identität → die State-Typen, die sie belegen (Kollisionserkennung).
            var identitätZuStates = new Dictionary<string, HashSet<INamedTypeSymbol>>(System.StringComparer.Ordinal);
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

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
                foreach (var method in type.GetMembers("Decide").OfType<IMethodSymbol>())
                {
                    if (method.Parameters.Length < 1)
                        continue;
                    if (method.Parameters[0].Type is not INamedTypeSymbol cmd)
                        continue;
                    if (!cmd.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default))
                        continue;

                    if (!map.TryGetValue(cmd, out var set))
                        map[cmd] = set = new SortedSet<string>(System.StringComparer.Ordinal);
                    set.Add(stateName);

                    if (!events.TryGetValue(cmd, out var evSet))
                        events[cmd] = evSet = new SortedSet<string>(System.StringComparer.Ordinal);
                    foreach (var ev in ProduzierteEvents(method.ReturnType, iEvent, iTransient, fq))
                        evSet.Add(ev);
                }
            }

            // ★ H4: Kollisionen als Build-Fehler melden (fail-fast statt stillem Drop).
            foreach (var kv in map.Where(k => k.Value.Count > 1))
                context.ReportDiagnostic(Diagnostic.Create(
                    CommandKollision,
                    kv.Key.Locations.FirstOrDefault() ?? Location.None,
                    kv.Key.Name, string.Join(", ", kv.Value)));

            // ★ P1c (TG-3): zwei verschiedene State-Typen mit derselben aufgelösten Identität → Build-Fehler.
            foreach (var kv in identitätZuStates.Where(k => k.Value.Count > 1))
                context.ReportDiagnostic(Diagnostic.Create(
                    AggregatKollision,
                    kv.Value.First().Locations.FirstOrDefault() ?? Location.None,
                    kv.Key, string.Join(", ", kv.Value.Select(s => s.ToDisplayString(fq)))));

            context.AddSource("GeneratedCommandRouting.g.cs", Emit(map, events, fq));
        }

        /// <summary>
        /// ★ P1c (TG-3): die Aggregat-Identität — <c>[AggregatName("…")]</c> falls gesetzt, sonst der einfache
        /// Typname des State (Default, keine Migration).
        /// </summary>
        private static string AggregatIdentität(INamedTypeSymbol state)
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
            if (type.TypeKind == TypeKind.Class && !type.IsAbstract)
                results.Add(type);
            foreach (var nested in type.GetTypeMembers())
                CollectNested(nested, results);
        }

        private static string Emit(
            Dictionary<INamedTypeSymbol, SortedSet<string>> map,
            Dictionary<INamedTypeSymbol, SortedSet<string>> events,
            SymbolDisplayFormat fq)
        {
            var eindeutig = map.Where(k => k.Value.Count == 1).ToList();
            var kollisionen = map.Where(k => k.Value.Count > 1).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Wurzel-2: präzises Command→Aggregat-Routing UND Command→Event-Abbildung aus den Decider-Signaturen.");
            sb.AppendLine("// Quelle: jede IDecider<TState>.Decide(TCommand) → TCommand gehört zu TState; der OneOf-Rückgabetyp nennt die Events.");
            sb.AppendLine($"// {eindeutig.Count} eindeutige Zuordnungen, {kollisionen.Count} Kollisionen.");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Mapping;");
            sb.AppendLine();
            sb.AppendLine("public static class GeneratedCommandRouting");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Command-Typ → Aggregat-Name, strukturell aus den Decidern abgeleitet.</summary>");
            sb.AppendLine("    public static IReadOnlyDictionary<Type, string> CommandToAggregate { get; } =");
            sb.AppendLine("        new Dictionary<Type, string>");
            sb.AppendLine("        {");

            foreach (var kv in eindeutig.OrderBy(k => k.Key.Name, System.StringComparer.Ordinal))
                sb.AppendLine($"            [typeof({kv.Key.ToDisplayString(fq)})] = \"{kv.Value.First()}\",");

            sb.AppendLine("        };");
            sb.AppendLine();

            // ★ Azyklizität-Grundlage: Command → produzierte (persistierte) Event-Typen, präzise aus den Decidern.
            sb.AppendLine("    /// <summary>Command-Typ → produzierte persistierte Event-Typen (aus den Decide-OneOf-Rückgaben).</summary>");
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

            if (kollisionen.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("    // ⚠ MEHRDEUTIGE COMMANDS (von mehr als einem Decider behandelt — als Build-Fehler CQRS010 gemeldet):");
                foreach (var kv in kollisionen.OrderBy(k => k.Key.Name, System.StringComparer.Ordinal))
                    sb.AppendLine($"    //   {kv.Key.Name} → {string.Join(", ", kv.Value)}");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
