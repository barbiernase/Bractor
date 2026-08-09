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

            // TriggerType→Pipeline-Klassenname-Mapping aufbauen
            var triggerToClass = BuildTriggerToPipelineClassMapping(context.Compilation, sorted);

            string actorsSource = GeneratePipelineActorsFile(sorted);
            string registrationSource = GeneratePipelinesRegistrationFile(sorted, triggerToClass);

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
        /// Baut ein Mapping TriggerType → PipelineId auf.
        /// Analysiert die Handle()-Methoden jeder Pipeline:
        /// erster Parameter implementiert IPipelineTrigger → Trigger-Typ.
        /// PipelineId wird aus dem PipelineId-Property (string literal) extrahiert.
        /// </summary>
        /// <summary>
        /// Baut ein Mapping: Trigger-Typ → Pipeline-Klassenname.
        /// Klassenname statt PipelineId, weil PipelineId nur zur Laufzeit
        /// aus der Handler-Instanz gelesen werden kann (Cross-Assembly).
        /// </summary>
        private Dictionary<INamedTypeSymbol, string> BuildTriggerToPipelineClassMapping(
            Compilation compilation,
            List<INamedTypeSymbol> pipelineSymbols)
        {
            var result = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
            
            var iPipelineTrigger = compilation.GetTypeByMetadataName("Abstractions.IPipelineTrigger");
            if (iPipelineTrigger == null)
                return result;

            // Self-Messages filtern — sind Pipeline-intern, kein Trigger-Routing
            var iPipelineSelf = compilation.GetTypeByMetadataName("Abstractions.IPipelineSelfMessage");

            foreach (var pipeline in pipelineSymbols)
            {
                var pipelineClassName = pipeline.Name;

                // Handle()-Methoden finden, deren erster Parameter IPipelineTrigger implementiert
                var handleMethods = pipeline.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.Name == "Handle" && m.Parameters.Length >= 2);

                foreach (var method in handleMethods)
                {
                    var inputType = method.Parameters[0].Type as INamedTypeSymbol;
                    if (inputType == null) continue;

                    // Self-Messages explizit ausschließen
                    if (iPipelineSelf != null &&
                        inputType.AllInterfaces.Contains(iPipelineSelf, SymbolEqualityComparer.Default))
                        continue;

                    if (inputType.AllInterfaces.Contains(iPipelineTrigger, SymbolEqualityComparer.Default)
                        || SymbolEqualityComparer.Default.Equals(inputType, iPipelineTrigger))
                    {
                        result[inputType] = pipelineClassName;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Extrahiert den PipelineId-String aus dem Property einer Pipeline-Klasse.
        /// Unterstützt: public string PipelineId => "xyz";
        /// </summary>
        private string ExtractPipelineId(INamedTypeSymbol pipelineSymbol)
        {
            var property = pipelineSymbol.GetMembers("PipelineId")
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            if (property == null)
                return null;

            // Syntax-Node des Properties holen
            var syntaxRef = property.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
                return null;

            var syntaxNode = syntaxRef.GetSyntax();
            var text = syntaxNode.ToString();

            // "image-processing" aus => "image-processing" extrahieren
            var startQuote = text.IndexOf('"');
            var endQuote = text.LastIndexOf('"');
            if (startQuote >= 0 && endQuote > startQuote)
            {
                return text.Substring(startQuote + 1, endQuote - startQuote - 1);
            }

            return null;
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
                sb.AppendLine($"        Func<ICommand, Task> sendCommand,");
                sb.AppendLine($"        Func<IPipelineTrigger, Task> sendTrigger,");
                sb.AppendLine($"        Func<ITransientEvent, Task> broadcastTransient)");
                sb.AppendLine($"        => _logic.DispatchTriggerAsync(trigger, ctx, sendCommand, sendTrigger, broadcastTransient);");
                sb.AppendLine();

                // DispatchEventAsync
                sb.AppendLine($"    protected override Task DispatchEventAsync(");
                sb.AppendLine($"        IAggregateEnvelope envelope, PipelineContext ctx,");
                sb.AppendLine($"        Func<ICommand, Task> sendCommand,");
                sb.AppendLine($"        Func<IPipelineTrigger, Task> sendTrigger,");
                sb.AppendLine($"        Func<ITransientEvent, Task> broadcastTransient)");
                sb.AppendLine($"        => _logic.DispatchEventAsync(envelope, ctx, sendCommand, sendTrigger, broadcastTransient);");
                sb.AppendLine();

                // DispatchSelfAsync
                sb.AppendLine($"    protected override Task DispatchSelfAsync(");
                sb.AppendLine($"        IPipelineSelfMessage selfMsg, PipelineContext ctx,");
                sb.AppendLine($"        Func<ICommand, Task> sendCommand,");
                sb.AppendLine($"        Func<IPipelineTrigger, Task> sendTrigger,");
                sb.AppendLine($"        Func<ITransientEvent, Task> broadcastTransient)");
                sb.AppendLine($"        => _logic.DispatchSelfAsync(selfMsg, ctx, sendCommand, sendTrigger, broadcastTransient);");

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
            Dictionary<INamedTypeSymbol, string> triggerToClass)
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
            // Trigger-Namespaces für das Mapping
            foreach (var kvp in triggerToClass)
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
            sb.AppendLine("using Core;");
            sb.AppendLine("using Infrastructure.Pipeline.Actors;");
            sb.AppendLine("using Infrastructure.Projections;");
            sb.AppendLine("using Infrastructure.PubSub;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Proto;");
            sb.AppendLine("using Proto.Cluster;");
            sb.AppendLine("using Proto.DependencyInjection;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
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
            sb.AppendLine("        InitializeTriggerMapping(provider);");
            sb.AppendLine("        var kinds = new List<ClusterKind>();");
            sb.AppendLine();

            foreach (var symbol in pipelineSymbols)
            {
                string name = symbol.Name;
                string actorName = $"{name}PipelineActor";

                // PipelineId wird zur Laufzeit aus dem Handler gelesen,
                // weil ExtractPipelineId bei Cross-Assembly-Symbolen keine Syntax hat.
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var handler_{name} = provider.GetRequiredService<{name}>();");
                sb.AppendLine($"            var kindName_{name} = $\"Pipeline-{{handler_{name}.PipelineId}}\";");
                sb.AppendLine($"            kinds.Add(new ClusterKind(");
                sb.AppendLine($"                kindName_{name},");
                sb.AppendLine($"                Props.FromProducer(() =>");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    var cluster = system.Cluster();");
                sb.AppendLine($"                    var publisher = provider.GetRequiredService<Infrastructure.PubSub.BrokerPublisher>();");
                sb.AppendLine($"                    var logger = provider.GetService<ILogger<{actorName}>>();");
                sb.AppendLine($"                    return new {actorName}(handler_{name}, cluster, publisher, logger);");
                sb.AppendLine($"                }})");
                sb.AppendLine($"            ));");
                sb.AppendLine($"        }}");
                sb.AppendLine();
            }

            sb.AppendLine("        return kinds.ToArray();");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ── CommandAggregateTypes (Passthrough auf die präzise, Decider-basierte Map) ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Command-Typ → AggregateType-Name (Routing). Delegiert an die PRÄZISE, aus den");
            sb.AppendLine("    /// Decider-Signaturen abgeleitete Map (GeneratedCommandRouting) — NICHT mehr namespace-basiert.");
            sb.AppendLine("    /// Strukturell garantiert: das Ziel ist das Aggregat, dessen Decider den Command behandelt.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static IReadOnlyDictionary<Type, string> CommandAggregateTypes");
            sb.AppendLine("        => global::Infrastructure.Mapping.GeneratedCommandRouting.CommandToAggregate;");
            sb.AppendLine();

            // ── TriggerToPipelineId (Laufzeit-Initialisierung) ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Trigger-Typ → PipelineId.");
            sb.AppendLine("    /// Wird vom PipelineActorBase und CqrsClientService für das Trigger-Routing verwendet.");
            sb.AppendLine("    /// Wird zur Laufzeit initialisiert, weil PipelineId nur aus Handler-Instanzen lesbar ist.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static IReadOnlyDictionary<Type, string>? _triggerToPipelineId;");
            sb.AppendLine("    public static IReadOnlyDictionary<Type, string> TriggerToPipelineId =>");
            sb.AppendLine("        _triggerToPipelineId ?? throw new InvalidOperationException(");
            sb.AppendLine("            \"TriggerToPipelineId not initialized — GetPipelineKinds must be called first.\");");
            sb.AppendLine();
            sb.AppendLine("    private static void InitializeTriggerMapping(IServiceProvider provider)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_triggerToPipelineId != null) return;");
            sb.AppendLine("        _triggerToPipelineId = new Dictionary<Type, string>");
            sb.AppendLine("        {");

            foreach (var kvp in triggerToClass.OrderBy(k => k.Key.Name))
            {
                // kvp.Key = Trigger-Typ (z.B. DateiErkannt)
                // kvp.Value = Pipeline-Klassenname (z.B. ImageProcessingPipeline)
                sb.AppendLine($"            [typeof({kvp.Key.Name})] = provider.GetRequiredService<{kvp.Value}>().PipelineId,");
            }

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ── AddGeneratedPipelineEventPulls (P6.2) ──
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// P6.2: verdrahtet den EVENT-Pfad (Kanal 2) jeder Pipeline mit Event-Handlern über die");
            sb.AppendLine("    /// geordnete Pull-/Signal-Maschine (statt des verlustbehafteten Push-Brokers). Kind +");
            sb.AppendLine("    /// PullPathRegistration je Pipeline mit nicht-leeren SubscribedEventTypes; der");
            sb.AppendLine("    /// GenericPullStartupService (aus AddGeneratedPullPaths) weckt sie.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static IServiceCollection AddGeneratedPipelineEventPulls(this IServiceCollection services)");
            sb.AppendLine("    {");
            foreach (var symbol in pipelineSymbols)
            {
                var n = symbol.Name;
                // Nur PERSISTIERTE Event-Typen wandern auf Pull. Transiente Events (ITransientEvent) sind
                // nicht im Log → der Pull-Pfad sähe sie nie; sie bleiben per Invariante 6 auf dem Push-Kanal
                // (das transient-gefilterte Broker-Abo im PipelineActorBase).
                sb.AppendLine($"        var persistierte_{n} = {n}.SubscribedEventTypes");
                sb.AppendLine("            .Where(t => !typeof(ITransientEvent).IsAssignableFrom(t)).ToList();");
                sb.AppendLine($"        if (persistierte_{n}.Count > 0)");
                sb.AppendLine("        {");
                sb.AppendLine($"            services.AddSingleton<IClusterKindContributor, {n}EventPullKind>();");
                sb.AppendLine($"            services.AddSingleton(new PullPathRegistration(");
                sb.AppendLine($"                {n}EventPullKind.KindName, {n}EventPullKind.KindName, persistierte_{n}));");
                sb.AppendLine("        }");
            }
            sb.AppendLine("        services.AddHostedService<GenericPullStartupService>();   // idempotent (TryAddEnumerable)");
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");

            sb.AppendLine("}");
            sb.AppendLine();

            // ── {Name}EventPullKind je Pipeline (P6.2) ──
            foreach (var symbol in pipelineSymbols)
            {
                var n = symbol.Name;
                sb.AppendLine($"internal sealed class {n}EventPullKind : IClusterKindContributor");
                sb.AppendLine("{");
                sb.AppendLine($"    public const string KindName = \"pull-pipeline-{n}\";");
                sb.AppendLine();
                sb.AppendLine("    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)");
                sb.AppendLine("    {");
                sb.AppendLine("        var eventStore = provider.GetRequiredService<IEventStoreRepository>();");
                sb.AppendLine("        var depsSink = provider.GetService<IReadModelDepsSink>();");
                sb.AppendLine("        return new ClusterKind(KindName, Props.FromProducer(() =>");
                sb.AppendLine("            new SignalAdapterActor(eventStore, () =>");
                sb.AppendLine("            {");
                sb.AppendLine($"                var handler = provider.GetRequiredService<{n}>();");
                sb.AppendLine("                var cluster = system.Cluster();");
                sb.AppendLine("                var publisher = provider.GetService<BrokerPublisher>();");
                sb.AppendLine("                var router = new HandlerOutputRouter(cluster, publisher, handler.PipelineId);");
                sb.AppendLine("                // Emittierend (Achse B, P4.2): best-effort IEmittentenCursor, KEIN Reset.");
                sb.AppendLine("                var emittentenCursor = provider.GetService<IEmittentenCursor>();");
                sb.AppendLine("                Func<EventEnvelope, Func<IPipelineOutput, Task>> emitFactory =");
                sb.AppendLine("                    ev => DetachedEmit.Wrap(router.EmitFor(ev, CancellationToken.None));");
                sb.AppendLine("                Func<IPipelineTrigger, string, Task> sendTrigger =");
                sb.AppendLine("                    (trig, _) => PipelineTriggerSender.SendAsync(cluster, trig, null);");
                sb.AppendLine("                var dispatch = PipelineEventPullBridge.Wrap(");
                sb.AppendLine("                    handler.DispatchEventAsync, emitFactory, sendTrigger);");
                sb.AppendLine("                return (handler.PipelineId, (IProjectionTracker?)null, emittentenCursor, dispatch);");
                sb.AppendLine("            }, depsSink)));");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}