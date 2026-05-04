// In Projekt: Infrastructure.SourceGeneration
// Dateiname: PipelineActorGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Generiert für jede IPipelineHandler-Implementierung:
    ///
    /// 1. PipelineActors.g.cs — Actor-Klassen (XyzPipelineActor : PipelineActorBase&lt;Xyz&gt;)
    /// 2. GeneratedPipelines.g.cs — DI-Registrierung, Spawn-Infos, CommandAggregateTypes, ClusterKind
    ///
    /// Vorlage: SubscriberActorGenerator / AggregateActorGenerator
    ///
    /// Command→AggregateType-Mapping: Nutzt dieselbe Namespace-Konvention
    /// wie EventCommandMappingGenerator. Commands in Domain.X gehören zu Aggregate X.
    /// </summary>
    [Generator]
    public class PipelineActorGenerator : ISourceGenerator
    {
        private const string IPipelineHandlerFullName = "Abstractions.IPipelineHandler";
        private const string IStateFullName = "Abstractions.IState";
        private const string ICommandFullName = "Abstractions.ICommand";

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var pipelineSymbols = FindIPipelineHandlerImplementations(context.Compilation);

            if (pipelineSymbols.Count == 0)
                return;

            var sorted = pipelineSymbols.OrderBy(s => s.Name).ToList();

            // Command→AggregateType-Mapping aufbauen
            var commandAggregateTypes = BuildCommandAggregateTypeMapping(context.Compilation);

            string actorsSource = GeneratePipelineActorsFile(sorted);
            string registrationSource = GeneratePipelinesRegistrationFile(sorted, commandAggregateTypes);

            context.AddSource("PipelineActors.g.cs", actorsSource);
            context.AddSource("GeneratedPipelines.g.cs", registrationSource);
        }

        // ═══════════════════════════════════════════════════════
        // Type Discovery
        // ═══════════════════════════════════════════════════════

        private List<INamedTypeSymbol> FindIPipelineHandlerImplementations(Compilation compilation)
        {
            var results = new List<INamedTypeSymbol>();
            var iPipelineHandler = compilation.GetTypeByMetadataName(IPipelineHandlerFullName);

            if (iPipelineHandler == null)
                return results;

            void FindTypes(INamespaceSymbol namespaceSymbol)
            {
                foreach (var type in namespaceSymbol.GetTypeMembers())
                {
                    if (type.TypeKind == TypeKind.Class &&
                        !type.IsAbstract &&
                        type.AllInterfaces.Contains(iPipelineHandler, SymbolEqualityComparer.Default))
                    {
                        results.Add(type);
                    }
                }

                foreach (var subNamespace in namespaceSymbol.GetNamespaceMembers())
                {
                    FindTypes(subNamespace);
                }
            }

            FindTypes(compilation.GlobalNamespace);
            return results;
        }

        /// <summary>
        /// Baut ein Mapping Command-Typ → AggregateType-Name auf.
        /// Nutzt dieselbe Namespace-Konvention wie EventCommandMappingGenerator:
        /// IState-Implementierungen definieren Aggregate-Namespaces,
        /// ICommand-Typen im selben Namespace gehören zu diesem Aggregate.
        /// </summary>
        private Dictionary<INamedTypeSymbol, string> BuildCommandAggregateTypeMapping(Compilation compilation)
        {
            var result = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);

            var iState = compilation.GetTypeByMetadataName(IStateFullName);
            var iCommand = compilation.GetTypeByMetadataName(ICommandFullName);

            if (iState == null || iCommand == null)
                return result;

            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            // Aggregate finden → Namespace → Name
            var aggregatesByNamespace = allTypes
                .Where(t => t.AllInterfaces.Contains(iState, SymbolEqualityComparer.Default))
                .ToDictionary(
                    a => a.ContainingNamespace.ToDisplayString(),
                    a => a.Name);

            // Commands ihren Aggregaten zuordnen
            var commands = allTypes
                .Where(t => t.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default));

            foreach (var cmd in commands)
            {
                var ns = cmd.ContainingNamespace.ToDisplayString();
                if (aggregatesByNamespace.TryGetValue(ns, out var aggregateName))
                {
                    result[cmd] = aggregateName;
                }
            }

            return result;
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

        // ═══════════════════════════════════════════════════════
        // PipelineActors.g.cs
        // ═══════════════════════════════════════════════════════

        private string GeneratePipelineActorsFile(List<INamedTypeSymbol> pipelineSymbols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Pipeline-Actors delegieren an IPipelineHandler-Logik-Klassen");
            sb.AppendLine();

            var namespaces = new HashSet<string>();
            foreach (var symbol in pipelineSymbols)
            {
                namespaces.Add(symbol.ContainingNamespace.ToDisplayString());
            }

            sb.AppendLine("using Abstractions;");
            sb.AppendLine("using Infrastructure.Pipeline;");
            sb.AppendLine("using Infrastructure.PubSub;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Proto.Cluster;");
            foreach (var ns in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();

            sb.AppendLine("namespace Infrastructure.Pipeline.Actors;");
            sb.AppendLine();

            foreach (var symbol in pipelineSymbols)
            {
                string name = symbol.Name;
                string actorName = $"{name}PipelineActor";

                sb.AppendLine($"public class {actorName} : PipelineActorBase<{name}>");
                sb.AppendLine("{");

                // Konstruktor
                sb.AppendLine($"    public {actorName}(");
                sb.AppendLine($"        {name} logic,");
                sb.AppendLine($"        Cluster cluster,");
                sb.AppendLine($"        Infrastructure.PubSub.BrokerPublisher? publisher = null,");
                sb.AppendLine($"        ILogger<{actorName}>? logger = null)");
                sb.AppendLine($"        : base(logic, cluster, publisher, logger) {{ }}");
                sb.AppendLine();

                // GetSubscribedEventTypes
                sb.AppendLine($"    protected override IReadOnlyList<Type> GetSubscribedEventTypes()");
                sb.AppendLine($"        => {name}.SubscribedEventTypes;");
                sb.AppendLine();

                // GetTriggerTypes
                sb.AppendLine($"    protected override IReadOnlyList<Type> GetTriggerTypes()");
                sb.AppendLine($"        => {name}.TriggerTypes;");
                sb.AppendLine();

                // GetCommandAggregateTypes
                sb.AppendLine($"    protected override IReadOnlyDictionary<Type, string> GetCommandAggregateTypes()");
                sb.AppendLine($"        => GeneratedPipelines.CommandAggregateTypes;");
                sb.AppendLine();

                // DispatchTriggerAsync
                sb.AppendLine($"    protected override Task DispatchTriggerAsync(");
                sb.AppendLine($"        IPipelineTrigger trigger, PipelineContext ctx,");
                sb.AppendLine($"        Func<ICommand, Task> send)");
                sb.AppendLine($"        => _logic.DispatchTriggerAsync(trigger, ctx, send);");
                sb.AppendLine();

                // DispatchEventAsync
                sb.AppendLine($"    protected override Task DispatchEventAsync(");
                sb.AppendLine($"        IAggregateEnvelope envelope, PipelineContext ctx,");
                sb.AppendLine($"        Func<ICommand, Task> send)");
                sb.AppendLine($"        => _logic.DispatchEventAsync(envelope, ctx, send);");

                sb.AppendLine("}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════
        // GeneratedPipelines.g.cs
        // ═══════════════════════════════════════════════════════

        private string GeneratePipelinesRegistrationFile(
            List<INamedTypeSymbol> pipelineSymbols,
            Dictionary<INamedTypeSymbol, string> commandAggregateTypes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// DI-Registrierung, Spawn-Infos und Command-Routing für Pipeline-Actors");
            sb.AppendLine();

            var namespaces = new HashSet<string>();
            foreach (var symbol in pipelineSymbols)
            {
                namespaces.Add(symbol.ContainingNamespace.ToDisplayString());
            }
            // Command-Namespaces für das Mapping
            foreach (var kvp in commandAggregateTypes)
            {
                var ns = kvp.Key.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrEmpty(ns))
                    namespaces.Add(ns);
            }

            foreach (var ns in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine("using Abstractions;");
            sb.AppendLine("using Infrastructure.Pipeline.Actors;");
            sb.AppendLine("using Infrastructure.PubSub;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Proto;");
            sb.AppendLine("using Proto.Cluster;");
            sb.AppendLine("using Proto.DependencyInjection;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();

            sb.AppendLine("namespace Infrastructure.Pipeline;");
            sb.AppendLine();

            sb.AppendLine("public static class GeneratedPipelines");
            sb.AppendLine("{");

            // ── RegisterAllPipelines ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Registriert alle Pipeline-Handler als Singleton im DI-Container.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static IServiceCollection RegisterAllPipelines(IServiceCollection services)");
            sb.AppendLine("    {");

            foreach (var symbol in pipelineSymbols)
            {
                sb.AppendLine($"        services.AddSingleton<{symbol.Name}>();");
                sb.AppendLine($"        Console.WriteLine(\"  + {symbol.Name}\");");
            }

            sb.AppendLine();
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ── GetPipelineSpawnInfos ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Liefert Props für alle Pipeline-Actors.");
            sb.AppendLine("    /// Wird von PipelineStartupService genutzt.");
            sb.AppendLine("    /// BrokerPublisher wird aus dem IServiceProvider aufgelöst.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static IEnumerable<(string Name, string PipelineId, Props Props)> GetPipelineSpawnInfos(");
            sb.AppendLine("        IServiceProvider provider,");
            sb.AppendLine("        Cluster cluster)");
            sb.AppendLine("    {");
            sb.AppendLine("        var publisher = provider.GetRequiredService<Infrastructure.PubSub.BrokerPublisher>();");
            sb.AppendLine();

            foreach (var symbol in pipelineSymbols)
            {
                string name = symbol.Name;
                string actorName = $"{name}PipelineActor";

                sb.AppendLine($"        yield return (");
                sb.AppendLine($"            \"{name}\",");
                sb.AppendLine($"            provider.GetRequiredService<{name}>().PipelineId,");
                sb.AppendLine($"            Props.FromProducer(() =>");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                var logic = provider.GetRequiredService<{name}>();");
                sb.AppendLine($"                var logger = provider.GetService<ILogger<{actorName}>>();");
                sb.AppendLine($"                return new {actorName}(logic, cluster, publisher, logger);");
                sb.AppendLine($"            }})");
                sb.AppendLine($"        );");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine();

            // ── GetPipelineKinds ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Erstellt ClusterKinds für Pipeline-Actors.");
            sb.AppendLine("    /// Ermöglicht Adressierung per ClusterIdentity (für Trigger-Messages).");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static ClusterKind[] GetPipelineKinds(");
            sb.AppendLine("        IServiceProvider provider,");
            sb.AppendLine("        ActorSystem system)");
            sb.AppendLine("    {");
            sb.AppendLine("        var kinds = new List<ClusterKind>();");
            sb.AppendLine();

            foreach (var symbol in pipelineSymbols)
            {
                string name = symbol.Name;
                string actorName = $"{name}PipelineActor";

                sb.AppendLine($"        kinds.Add(new ClusterKind(");
                sb.AppendLine($"            \"Pipeline\",");
                sb.AppendLine($"            Props.FromProducer(() =>");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                // Lazy: Cluster + Publisher erst bei Spawn aufloesen (Cluster ist dann konfiguriert)");
                sb.AppendLine($"                var cluster = system.Cluster();");
                sb.AppendLine($"                var publisher = provider.GetRequiredService<Infrastructure.PubSub.BrokerPublisher>();");
                sb.AppendLine($"                var logic = provider.GetRequiredService<{name}>();");
                sb.AppendLine($"                var logger = provider.GetService<ILogger<{actorName}>>();");
                sb.AppendLine($"                return new {actorName}(logic, cluster, publisher, logger);");
                sb.AppendLine($"            }})");
                sb.AppendLine($"        ));");
                sb.AppendLine();
            }

            sb.AppendLine("        return kinds.ToArray();");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ── CommandAggregateTypes ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Command-Typ → AggregateType-Name.");
            sb.AppendLine("    /// Wird vom PipelineActorBase für das Routing verwendet.");
            sb.AppendLine("    /// Leitet sich aus der Namespace-Konvention ab");
            sb.AppendLine("    /// (gleicher Algorithmus wie EventCommandMappingGenerator).");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static IReadOnlyDictionary<Type, string> CommandAggregateTypes { get; } =");
            sb.AppendLine("        new Dictionary<Type, string>");
            sb.AppendLine("        {");

            foreach (var kvp in commandAggregateTypes.OrderBy(k => k.Key.Name))
            {
                sb.AppendLine($"            [typeof({kvp.Key.Name})] = \"{kvp.Value}\",");
            }

            sb.AppendLine("        };");

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}