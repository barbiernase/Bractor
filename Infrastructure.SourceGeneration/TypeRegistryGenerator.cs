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
    /// Single-Pass: Ein Durchlauf über alle Typen, Kategorisierung per Interface.
    /// Neue Kategorien = ein Eintrag in der categories-Liste, sonst nichts.
    ///
    /// Erkennt:
    /// - IEvent (alle Events, inkl. ITransientEvent)
    /// - ICommand (alle Commands, inkl. ICreationCommand)
    /// - IQuery (alle Queries)
    /// - IQueryResponse (alle Responses)
    /// - IPipelineTrigger (alle Trigger)
    /// - ITransientEvent (Subset von IEvent, für Marten-Ausschluss)
    ///
    /// Speist:
    /// - MessageTypeMapping (einzige Runtime-Registry)
    /// - MartenEventTypeRegistration
    /// </summary>
    [Generator]
    public class TypeRegistryGenerator : ISourceGenerator
    {
        /// <summary>
        /// Definition einer Nachrichtenkategorie.
        /// </summary>
        private class CategoryDef
        {
            public string InterfaceFullName { get; }
            public string DictionaryName { get; }
            public string Comment { get; }
            public INamedTypeSymbol Symbol { get; set; }
            public List<INamedTypeSymbol> Types { get; } = new List<INamedTypeSymbol>();

            public CategoryDef(string interfaceFullName, string dictionaryName, string comment)
            {
                InterfaceFullName = interfaceFullName;
                DictionaryName = dictionaryName;
                Comment = comment;
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            // ═══════════════════════════════════════════════════
            // Kategorien — neue Kategorie = ein Eintrag hier
            // ═══════════════════════════════════════════════════

            var categories = new[]
            {
                new CategoryDef("Abstractions.IEvent",             "Events",         "EVENTS (alle, inkl. ITransientEvent)"),
                new CategoryDef("Abstractions.ICommand",           "Commands",        "COMMANDS (alle, inkl. ICreationCommand)"),
                new CategoryDef("Abstractions.IQuery",             "Queries",         "QUERIES"),
                new CategoryDef("Abstractions.IQueryResponse",     "QueryResponses",  "QUERY RESPONSES"),
                new CategoryDef("Abstractions.IPipelineTrigger",   "Triggers",        "TRIGGERS (Pipeline-Eingänge)"),
                new CategoryDef("Abstractions.IStateChangeSignal", "Signals",         "SIGNALS (StateChangeVia{Event}, nur Weckrufe)"),
            };

            // TransientEvent: kein eigenes Dictionary, aber nötig für PersistableEvents
            var transientDef = new CategoryDef("Abstractions.ITransientEvent", "_transient", "");

            // Interface-Symbole auflösen
            foreach (var cat in categories)
            {
                cat.Symbol = compilation.GetTypeByMetadataName(cat.InterfaceFullName);
            }
            transientDef.Symbol = compilation.GetTypeByMetadataName(transientDef.InterfaceFullName);

            // Mindestens IEvent und ICommand müssen existieren
            if (categories[0].Symbol == null || categories[1].Symbol == null)
                return;

            // ═══════════════════════════════════════════════════
            // Single-Pass: alle Typen kategorisieren
            // ═══════════════════════════════════════════════════

            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            // Self-Messages komplett ignorieren — sind Pipeline-intern,
            // kein Proto-Mapping, kein TypeRegistry, kein Marten
            var iPipelineSelf = compilation.GetTypeByMetadataName("Abstractions.IPipelineSelfMessage");

            foreach (var type in allTypes)
            {
                // Self-Messages explizit ausschließen
                if (iPipelineSelf != null &&
                    type.AllInterfaces.Contains(iPipelineSelf, SymbolEqualityComparer.Default))
                    continue;

                var interfaces = type.AllInterfaces;

                foreach (var cat in categories)
                {
                    if (cat.Symbol != null &&
                        interfaces.Contains(cat.Symbol, SymbolEqualityComparer.Default))
                    {
                        cat.Types.Add(type);
                    }
                }

                if (transientDef.Symbol != null &&
                    interfaces.Contains(transientDef.Symbol, SymbolEqualityComparer.Default))
                {
                    transientDef.Types.Add(type);
                }
            }

            // Sortieren für deterministische Ausgabe
            foreach (var cat in categories)
            {
                cat.Types.Sort((a, b) => string.Compare(a.Name, b.Name));
            }
            transientDef.Types.Sort((a, b) => string.Compare(a.Name, b.Name));

            // PersistableEvents = Events OHNE TransientEvents
            var transientSet = new HashSet<INamedTypeSymbol>(
                transientDef.Types, SymbolEqualityComparer.Default);
            var persistableEvents = categories[0].Types
                .Where(e => !transientSet.Contains(e))
                .ToList();

            // ═══════════════════════════════════════════════════
            // Code generieren
            // ═══════════════════════════════════════════════════

            var source = GenerateRegistry(categories, persistableEvents);
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
            }

            foreach (var subNs in ns.GetNamespaceMembers())
            {
                CollectTypes(subNs, results);
            }
        }

        // ═══════════════════════════════════════════════════════
        // Code-Generierung
        // ═══════════════════════════════════════════════════════

        private string GenerateRegistry(
            CategoryDef[] categories,
            List<INamedTypeSymbol> persistableEvents)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Statisches Typ-Registry — Single-Pass-Kategorisierung");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");

            // Namespaces über alle Kategorien sammeln
            var namespaces = new HashSet<string>();
            foreach (var cat in categories)
            {
                foreach (var t in cat.Types)
                {
                    var ns = t.ContainingNamespace?.ToDisplayString();
                    if (!string.IsNullOrEmpty(ns) && ns != "System")
                        namespaces.Add(ns);
                }
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

            // Dictionaries — datengetrieben
            foreach (var cat in categories)
            {
                sb.AppendLine($"    // ═══════════════════════════════════════════════════════");
                sb.AppendLine($"    // {cat.Comment}");
                sb.AppendLine($"    // ═══════════════════════════════════════════════════════");
                GenerateDictionary(sb, cat.DictionaryName, cat.Types);
            }

            // PersistableEvents — Spezialfall für Marten
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
        // Snake_case Konvertierung
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
