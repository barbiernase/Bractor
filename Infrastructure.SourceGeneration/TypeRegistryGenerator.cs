// In Projekt: Infrastructure.SourceGeneration
// Dateiname: TypeRegistryGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Generiert ein statisches Typ-Registry für alle Domain-Message-Typen.
    /// 
    /// Ersetzt Runtime-Reflection in:
    /// - MessageTypeMapping.Initialize()
    /// - EventTypeResolver.Initialize()
    /// - MartenEventTypeRegistration.DiscoverEventTypes()
    /// 
    /// Erkennt:
    /// - IEvent (alle Events, inkl. ITransientEvent)
    /// - ICommand (alle Commands, inkl. ICreationCommand)
    /// - IQuery (alle Queries)
    /// - IQueryResponse (alle Responses)
    /// - ITransientEvent (Subset von IEvent, für Marten-Ausschluss)
    /// </summary>
    [Generator]
    public class TypeRegistryGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            // Interface-Symbole auflösen
            var iEvent = compilation.GetTypeByMetadataName("Abstractions.IEvent");
            var iTransientEvent = compilation.GetTypeByMetadataName("Abstractions.ITransientEvent");
            var iCommand = compilation.GetTypeByMetadataName("Abstractions.ICommand");
            var iQuery = compilation.GetTypeByMetadataName("Abstractions.IQuery");
            var iQueryResponse = compilation.GetTypeByMetadataName("Abstractions.IQueryResponse");

            if (iEvent == null || iCommand == null)
                return;

            // Alle konkreten Typen sammeln
            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            var events = new List<INamedTypeSymbol>();
            var transientEvents = new List<INamedTypeSymbol>();
            var commands = new List<INamedTypeSymbol>();
            var queries = new List<INamedTypeSymbol>();
            var queryResponses = new List<INamedTypeSymbol>();

            foreach (var type in allTypes)
            {
                bool isEvent = type.AllInterfaces.Contains(iEvent, SymbolEqualityComparer.Default);
                bool isTransient = iTransientEvent != null &&
                                   type.AllInterfaces.Contains(iTransientEvent, SymbolEqualityComparer.Default);
                bool isCommand = type.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default);
                bool isQuery = iQuery != null &&
                               type.AllInterfaces.Contains(iQuery, SymbolEqualityComparer.Default);
                bool isQueryResponse = iQueryResponse != null &&
                                       type.AllInterfaces.Contains(iQueryResponse, SymbolEqualityComparer.Default);

                if (isEvent)
                {
                    events.Add(type);
                    if (isTransient)
                        transientEvents.Add(type);
                }
                if (isCommand) commands.Add(type);
                if (isQuery) queries.Add(type);
                if (isQueryResponse) queryResponses.Add(type);
            }

            // Sortieren für deterministische Ausgabe
            events = events.OrderBy(t => t.Name).ToList();
            transientEvents = transientEvents.OrderBy(t => t.Name).ToList();
            commands = commands.OrderBy(t => t.Name).ToList();
            queries = queries.OrderBy(t => t.Name).ToList();
            queryResponses = queryResponses.OrderBy(t => t.Name).ToList();

            // Persistable Events = Events OHNE TransientEvents
            var transientSet = new HashSet<INamedTypeSymbol>(transientEvents, SymbolEqualityComparer.Default);
            var persistableEvents = events.Where(e => !transientSet.Contains(e)).ToList();

            var source = GenerateRegistry(events, persistableEvents, commands, queries, queryResponses);
            context.AddSource("GeneratedTypeRegistry.g.cs", source);
        }

        private void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsStatic)
                {
                    results.Add(type);
                }
                // Records sind auch TypeKind.Class in Roslyn
            }

            foreach (var subNs in ns.GetNamespaceMembers())
            {
                CollectTypes(subNs, results);
            }
        }

        private string GenerateRegistry(
            List<INamedTypeSymbol> events,
            List<INamedTypeSymbol> persistableEvents,
            List<INamedTypeSymbol> commands,
            List<INamedTypeSymbol> queries,
            List<INamedTypeSymbol> queryResponses)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Statisches Typ-Registry — ersetzt Runtime-Reflection");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");

            // Alle benötigten Namespaces sammeln
            var namespaces = new HashSet<string>();
            foreach (var t in events.Concat(commands).Concat(queries).Concat(queryResponses))
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
            sb.AppendLine("/// Compile-Time Typ-Registry für alle Domain-Message-Typen.");
            sb.AppendLine("/// Generiert vom TypeRegistryGenerator — keine Reflection nötig.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static class GeneratedTypeRegistry");
            sb.AppendLine("{");

            // Events
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            sb.AppendLine("    // EVENTS (alle, inkl. ITransientEvent)");
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            GenerateDictionary(sb, "Events", events);

            // Commands
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            sb.AppendLine("    // COMMANDS (alle, inkl. ICreationCommand)");
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            GenerateDictionary(sb, "Commands", commands);

            // Queries
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            sb.AppendLine("    // QUERIES");
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            GenerateDictionary(sb, "Queries", queries);

            // QueryResponses
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            sb.AppendLine("    // QUERY RESPONSES");
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            GenerateDictionary(sb, "QueryResponses", queryResponses);

            // PersistableEvents (für Marten: IEvent ohne ITransientEvent, mit snake_case)
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            sb.AppendLine("    // PERSISTABLE EVENTS (für Marten: ohne ITransientEvent)");
            sb.AppendLine("    // ═══════════════════════════════════════════════════════");
            sb.AppendLine("    public static readonly IReadOnlyList<(Type Type, string SnakeCaseName)> PersistableEvents = new (Type, string)[]");
            sb.AppendLine("    {");
            foreach (var type in persistableEvents)
            {
                var snakeCaseName = ToSnakeCase(SanitizeName(type.Name));
                sb.AppendLine($"        (typeof({type.Name}), \"{snakeCaseName}\"),");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            // PersistableEventTypes (nur Type-Liste, für einfachere Nutzung)
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Nur die Type-Objekte der persistierbaren Events (Convenience).");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static readonly IReadOnlyList<Type> PersistableEventTypes = new Type[]");
            sb.AppendLine("    {");
            foreach (var type in persistableEvents)
            {
                sb.AppendLine($"        typeof({type.Name}),");
            }
            sb.AppendLine("    };");

            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateDictionary(StringBuilder sb, string name, List<INamedTypeSymbol> types)
        {
            sb.AppendLine($"    public static readonly IReadOnlyDictionary<string, Type> {name} = new Dictionary<string, Type>");
            sb.AppendLine("    {");
            foreach (var type in types)
            {
                sb.AppendLine($"        [\"{type.Name}\"] = typeof({type.Name}),");
            }
            sb.AppendLine("    };");
            sb.AppendLine();
        }

        // ═══════════════════════════════════════════════════════
        // Snake_case Konvertierung (Duplikat aus NameSanitizer,
        // da Source Generators keine Runtime-Referenzen nutzen können)
        // ═══════════════════════════════════════════════════════

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return name
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
                .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue")
                .Replace("ß", "ss");
        }

        private static string ToSnakeCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(text[0]));

            for (int i = 1; i < text.Length; ++i)
            {
                char c = text[i];
                if (char.IsUpper(c))
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}