// In Projekt: Infrastructure.SourceGeneration
// Dateiname: EventCommandMappingGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Generiert das Event→Command-Mapping auf Aggregat-Ebene.
    /// 
    /// Ansatz: Alle Events eines Aggregats → Alle Commands desselben Aggregats.
    /// Keine yield-return-Analyse, keine Syntax-Analyse, rein Typ-basiert.
    /// 
    /// Erkennung:
    /// 1. Finde alle IState-Implementierungen → das sind die Aggregate
    /// 2. Pro Aggregat: Namespace bestimmen (z.B. Domain.Lagerartikel)
    /// 3. Alle IEvent-Typen im selben Namespace → Aggregate-Events
    /// 4. Alle ICommand-Typen im selben Namespace → Aggregate-Commands
    /// 5. Mapping: Jedes Event → alle Commands des Aggregats
    /// 6. Events ohne Aggregat-Zugehörigkeit → Array.Empty (reaktive Events etc.)
    /// 
    /// Ersetzt: EventCommandMapping.cs (händisch, mit Domain-Imports)
    /// </summary>
    [Generator]
    public class EventCommandMappingGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            // Interface-Symbole auflösen
            var iState = compilation.GetTypeByMetadataName("Abstractions.IState");
            var iEvent = compilation.GetTypeByMetadataName("Abstractions.IEvent");
            var iCommand = compilation.GetTypeByMetadataName("Abstractions.ICommand");

            if (iState == null || iEvent == null || iCommand == null)
                return;

            // 1. Alle konkreten Typen sammeln
            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            // 2. Aggregate finden (IState-Implementierungen)
            var aggregates = allTypes
                .Where(t => t.AllInterfaces.Contains(iState, SymbolEqualityComparer.Default))
                .ToList();

            // 3. Events und Commands finden
            var events = allTypes
                .Where(t => t.AllInterfaces.Contains(iEvent, SymbolEqualityComparer.Default))
                .ToList();

            var commands = allTypes
                .Where(t => t.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default))
                .ToList();

            // 4. Aggregate-Namespaces bestimmen
            var aggregateNamespaces = aggregates
                .Select(a => a.ContainingNamespace.ToDisplayString())
                .Distinct()
                .ToList();

            // 5. Events und Commands pro Namespace gruppieren
            var eventsByNamespace = events
                .GroupBy(e => e.ContainingNamespace.ToDisplayString())
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Name).ToList());

            var commandsByNamespace = commands
                .GroupBy(c => c.ContainingNamespace.ToDisplayString())
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());

            // 6. Mapping aufbauen: Event → Commands (auf Aggregat-Ebene)
            var eventToCommands = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(
                SymbolEqualityComparer.Default);

            foreach (var ns in aggregateNamespaces)
            {
                if (!eventsByNamespace.TryGetValue(ns, out var nsEvents))
                    nsEvents = new List<INamedTypeSymbol>();

                if (!commandsByNamespace.TryGetValue(ns, out var nsCommands))
                    nsCommands = new List<INamedTypeSymbol>();

                foreach (var evt in nsEvents)
                {
                    eventToCommands[evt] = nsCommands;
                }
            }

            // Events ohne Aggregat-Zugehörigkeit → leeres Array
            foreach (var evt in events)
            {
                if (!eventToCommands.ContainsKey(evt))
                {
                    eventToCommands[evt] = new List<INamedTypeSymbol>();
                }
            }

            // 7. Generieren
            var source = GenerateMapping(eventToCommands, events, commands);
            context.AddSource("GeneratedEventCommandMapping.g.cs", source);
        }

        private void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsStatic)
                {
                    results.Add(type);
                }
            }

            foreach (var subNs in ns.GetNamespaceMembers())
            {
                CollectTypes(subNs, results);
            }
        }

        private string GenerateMapping(
            Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> eventToCommands,
            List<INamedTypeSymbol> allEvents,
            List<INamedTypeSymbol> allCommands)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Event→Command-Mapping auf Aggregat-Ebene");
            sb.AppendLine("// Alle Events eines Aggregats → Alle Commands desselben Aggregats");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");

            // Namespaces sammeln
            var namespaces = new HashSet<string>();
            foreach (var t in allEvents.Concat(allCommands))
            {
                var ns = t.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrEmpty(ns) && ns != "System")
                    namespaces.Add(ns);
            }
            foreach (var ns in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }

            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Mapping;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Generiertes Event→Command-Mapping.");
            sb.AppendLine("/// Aggregat-Ebene: Alle Events eines Aggregats → Alle Commands desselben Aggregats.");
            sb.AppendLine("/// Events ohne Aggregat-Zugehörigkeit → leeres Array.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static class GeneratedEventCommandMapping");
            sb.AppendLine("{");

            // EventToCommands Dictionary
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Event-Typ → erlaubte Command-Typen.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static readonly IReadOnlyDictionary<Type, Type[]> EventToCommands = new Dictionary<Type, Type[]>");
            sb.AppendLine("    {");

            // Sortiert nach Event-Name für deterministische Ausgabe
            foreach (var kvp in eventToCommands.OrderBy(k => k.Key.Name))
            {
                var evt = kvp.Key;
                var cmds = kvp.Value;

                if (cmds.Count == 0)
                {
                    sb.AppendLine($"        [typeof({evt.Name})] = Array.Empty<Type>(),");
                }
                else
                {
                    var cmdList = string.Join(", ", cmds.Select(c => $"typeof({c.Name})"));
                    sb.AppendLine($"        [typeof({evt.Name})] = new[] {{ {cmdList} }},");
                }
            }

            sb.AppendLine("    };");
            sb.AppendLine();

            // EventNameToCommandNames (String-basiert für Proto)
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// String-basierte Variante für Proto-Kommunikation.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static readonly IReadOnlyDictionary<string, string[]> EventNameToCommandNames =");
            sb.AppendLine("        EventToCommands.ToDictionary(");
            sb.AppendLine("            kvp => kvp.Key.Name,");
            sb.AppendLine("            kvp => kvp.Value.Select(t => t.Name).ToArray());");
            sb.AppendLine();

            // CommandToEvent (Reverse)
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Reverse: Command-Typ → Event-Typen die dadurch entstehen können.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static readonly IReadOnlyDictionary<Type, Type[]> CommandToEvents;");
            sb.AppendLine();

            // Static constructor für Reverse-Mapping
            sb.AppendLine("    static GeneratedEventCommandMapping()");
            sb.AppendLine("    {");
            sb.AppendLine("        var reverse = new Dictionary<Type, HashSet<Type>>();");
            sb.AppendLine("        foreach (var (eventType, commandTypes) in EventToCommands)");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (var cmdType in commandTypes)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (!reverse.TryGetValue(cmdType, out var set))");
            sb.AppendLine("                {");
            sb.AppendLine("                    set = new HashSet<Type>();");
            sb.AppendLine("                    reverse[cmdType] = set;");
            sb.AppendLine("                }");
            sb.AppendLine("                set.Add(eventType);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        CommandToEvents = reverse.ToDictionary(");
            sb.AppendLine("            kvp => kvp.Key,");
            sb.AppendLine("            kvp => kvp.Value.ToArray());");
            sb.AppendLine("    }");

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}