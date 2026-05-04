using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Domain.SourceGeneration;

/// <summary>
/// Generiert für jede Pipeline (IPipelineHandler):
/// - TriggerTypes (statische Liste der Trigger-Typen)
/// - SubscribedEventTypes (statische Liste der Event-Typen)
/// - ProducedCommandTypes (statische Liste der Command-Typen)
/// - DispatchTriggerAsync (Trigger-Routing via switch)
/// - DispatchEventAsync (Event-Routing via switch)
///
/// Vorlage: SubscriberDispatchGenerator
///
/// Erkennung: Handle-Methoden mit Signatur:
///   (T, PipelineContext) → IEnumerable&lt;ICommand&gt; | IAsyncEnumerable&lt;ICommand&gt; | Task
///
/// Unterscheidung nach Parameter[0]:
///   IPipelineTrigger → Trigger-Kanal (DispatchTriggerAsync)
///   IEvent → Event-Kanal (DispatchEventAsync)
/// </summary>
[Generator]
public class PipelineDispatchGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pipelineProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial"),
                transform: static (ctx, _) => GetPipelineInfo(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(pipelineProvider,
            static (spc, model) => Execute(spc, model!));
    }

    private static PipelineGeneratorModel? GetPipelineInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

        if (classSymbol == null)
            return null;

        // Muss IPipelineHandler implementieren
        var pipelineInterface = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IPipelineHandler");

        if (pipelineInterface == null)
            return null;

        var implementsPipeline = classSymbol.AllInterfaces
            .Any(i => SymbolEqualityComparer.Default.Equals(i, pipelineInterface));

        if (!implementsPipeline)
            return null;

        // Benötigte Typen auflösen
        var pipelineContextType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.PipelineContext");
        var iPipelineTriggerType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IPipelineTrigger");
        var iEventType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IEvent");
        var iCommandType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.ICommand");

        if (pipelineContextType == null || iPipelineTriggerType == null ||
            iEventType == null || iCommandType == null)
            return null;

        // Handle-Methoden finden: (T, PipelineContext)
        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 2 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, pipelineContextType))
            .ToList();

        if (handleMethods.Count == 0)
            return null;

        var triggerHandlers = new List<PipelineHandlerInfo>();
        var eventHandlers = new List<PipelineHandlerInfo>();
        var allNamespaces = new HashSet<string>();

        foreach (var method in handleMethods)
        {
            var inputType = method.Parameters[0].Type;
            var returnType = method.ReturnType;

            var inputTypeName = inputType.ToDisplayString();
            var inputNamespace = inputType.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(inputNamespace))
                allNamespaces.Add(inputNamespace);

            var handlerInfo = AnalyzeReturnType(returnType, inputTypeName, allNamespaces, iCommandType);

            // Kanal bestimmen: IPipelineTrigger oder IEvent?
            if (inputType.AllInterfaces.Contains(iPipelineTriggerType, SymbolEqualityComparer.Default)
                || SymbolEqualityComparer.Default.Equals(inputType, iPipelineTriggerType))
            {
                triggerHandlers.Add(handlerInfo);
            }
            else if (inputType.AllInterfaces.Contains(iEventType, SymbolEqualityComparer.Default)
                     || SymbolEqualityComparer.Default.Equals(inputType, iEventType))
            {
                eventHandlers.Add(handlerInfo);
            }
        }

        if (triggerHandlers.Count == 0 && eventHandlers.Count == 0)
            return null;

        return new PipelineGeneratorModel(
            pipelineNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
            pipelineName: classSymbol.Name,
            triggerHandlers: triggerHandlers,
            eventHandlers: eventHandlers,
            allNamespaces: allNamespaces.ToList()
        );
    }

    /// <summary>
    /// Analysiert den Rückgabetyp einer Handle-Methode.
    /// </summary>
    private static PipelineHandlerInfo AnalyzeReturnType(
        ITypeSymbol returnType, string inputTypeName,
        HashSet<string> namespaces, INamedTypeSymbol iCommandType)
    {
        // Task → Fire-and-Forget, keine Commands
        if (returnType.Name == "Task" && returnType is INamedTypeSymbol { IsGenericType: false })
        {
            return new PipelineHandlerInfo(inputTypeName, PipelineHandlerKind.Task, new List<string>());
        }

        // IEnumerable<ICommand> → sync, yields Commands
        if (returnType is INamedTypeSymbol { IsGenericType: true } enumerable
            && enumerable.OriginalDefinition.ToDisplayString()
                .StartsWith("System.Collections.Generic.IEnumerable"))
        {
            var elementType = enumerable.TypeArguments[0];
            if (elementType.AllInterfaces.Contains(iCommandType, SymbolEqualityComparer.Default)
                || SymbolEqualityComparer.Default.Equals(elementType, iCommandType))
            {
                return new PipelineHandlerInfo(inputTypeName, PipelineHandlerKind.Enumerable, new List<string>());
            }
        }

        // IAsyncEnumerable<ICommand> → async, yields Commands
        if (returnType is INamedTypeSymbol { IsGenericType: true } asyncEnum
            && asyncEnum.OriginalDefinition.ToDisplayString()
                .StartsWith("System.Collections.Generic.IAsyncEnumerable"))
        {
            var elementType = asyncEnum.TypeArguments[0];
            if (elementType.AllInterfaces.Contains(iCommandType, SymbolEqualityComparer.Default)
                || SymbolEqualityComparer.Default.Equals(elementType, iCommandType))
            {
                return new PipelineHandlerInfo(inputTypeName, PipelineHandlerKind.AsyncEnumerable, new List<string>());
            }
        }

        // Fallback: behandle wie Task
        return new PipelineHandlerInfo(inputTypeName, PipelineHandlerKind.Task, new List<string>());
    }

    private static void Execute(SourceProductionContext context, PipelineGeneratorModel model)
    {
        var source = GeneratePipelineDispatch(model);
        context.AddSource($"{model.PipelineName}.Pipeline.Dispatch.g.cs", source);
    }

    private static string GeneratePipelineDispatch(PipelineGeneratorModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Abstractions;");

        // Namespaces
        foreach (var ns in model.AllNamespaces.OrderBy(n => n))
        {
            if (ns != model.PipelineNamespace &&
                ns != "Abstractions" &&
                ns != "System")
            {
                sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {model.PipelineNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.PipelineName}");
        sb.AppendLine("{");

        // ── TriggerTypes ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Trigger-Typen (direkte Proto.Actor Messages).");
        sb.AppendLine("    /// Aus Handle-Methoden extrahiert deren erster Parameter IPipelineTrigger implementiert.");
        sb.AppendLine("    /// </summary>");
        if (model.TriggerHandlers.Count == 0)
        {
            sb.AppendLine("    public static IReadOnlyList<Type> TriggerTypes { get; } = Array.Empty<Type>();");
        }
        else
        {
            sb.AppendLine("    public static IReadOnlyList<Type> TriggerTypes { get; } = new[]");
            sb.AppendLine("    {");
            foreach (var handler in model.TriggerHandlers)
            {
                sb.AppendLine($"        typeof({GetSimpleTypeName(handler.InputTypeName)}),");
            }
            sb.AppendLine("    };");
        }
        sb.AppendLine();

        // ── SubscribedEventTypes ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Event-Typen (PubSub-Subscriptions).");
        sb.AppendLine("    /// Aus Handle-Methoden extrahiert deren erster Parameter IEvent implementiert.");
        sb.AppendLine("    /// </summary>");
        if (model.EventHandlers.Count == 0)
        {
            sb.AppendLine("    public static IReadOnlyList<Type> SubscribedEventTypes { get; } = Array.Empty<Type>();");
        }
        else
        {
            sb.AppendLine("    public static IReadOnlyList<Type> SubscribedEventTypes { get; } = new[]");
            sb.AppendLine("    {");
            foreach (var handler in model.EventHandlers)
            {
                sb.AppendLine($"        typeof({GetSimpleTypeName(handler.InputTypeName)}),");
            }
            sb.AppendLine("    };");
        }
        sb.AppendLine();

        // ── ProducedCommandTypes ──
        // Hinweis: Die konkreten Command-Typen können wir aus der Return-Typ-Analyse
        // nicht statisch extrahieren (generisch ICommand). Diese Liste wird vom
        // PipelineActorGenerator auf Infrastructure-Ebene befüllt.
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Placeholder — konkrete Command-Typen werden vom PipelineActorGenerator befüllt.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine();

        // ── DispatchTriggerAsync ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Dispatch für Trigger (direkte Messages). Switch über IPipelineTrigger-Typen.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async Task DispatchTriggerAsync(");
        sb.AppendLine("        IPipelineTrigger trigger,");
        sb.AppendLine("        PipelineContext ctx,");
        sb.AppendLine("        Func<ICommand, Task> send)");
        sb.AppendLine("    {");

        if (model.TriggerHandlers.Count == 0)
        {
            sb.AppendLine("        await Task.CompletedTask;");
        }
        else
        {
            sb.AppendLine("        switch (trigger)");
            sb.AppendLine("        {");

            foreach (var handler in model.TriggerHandlers)
            {
                var simpleName = GetSimpleTypeName(handler.InputTypeName);
                EmitDispatchCase(sb, simpleName, "t", handler.Kind);
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // ── DispatchEventAsync ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Dispatch für Events (PubSub). Switch über IEvent-Typen aus Envelope.Payload.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async Task DispatchEventAsync(");
        sb.AppendLine("        IAggregateEnvelope envelope,");
        sb.AppendLine("        PipelineContext ctx,");
        sb.AppendLine("        Func<ICommand, Task> send)");
        sb.AppendLine("    {");

        if (model.EventHandlers.Count == 0)
        {
            sb.AppendLine("        await Task.CompletedTask;");
        }
        else
        {
            sb.AppendLine("        switch (envelope.Payload)");
            sb.AppendLine("        {");

            foreach (var handler in model.EventHandlers)
            {
                var simpleName = GetSimpleTypeName(handler.InputTypeName);
                EmitDispatchCase(sb, simpleName, "e", handler.Kind);
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitDispatchCase(
        StringBuilder sb, string typeName, string varName, PipelineHandlerKind kind)
    {
        switch (kind)
        {
            case PipelineHandlerKind.Enumerable:
                sb.AppendLine($"            case {typeName} {varName}:");
                sb.AppendLine($"                foreach (var cmd in Handle({varName}, ctx))");
                sb.AppendLine($"                    await send(cmd);");
                sb.AppendLine($"                break;");
                sb.AppendLine();
                break;

            case PipelineHandlerKind.AsyncEnumerable:
                sb.AppendLine($"            case {typeName} {varName}:");
                sb.AppendLine($"                await foreach (var cmd in Handle({varName}, ctx))");
                sb.AppendLine($"                    await send(cmd);");
                sb.AppendLine($"                break;");
                sb.AppendLine();
                break;

            case PipelineHandlerKind.Task:
                sb.AppendLine($"            case {typeName} {varName}:");
                sb.AppendLine($"                await Handle({varName}, ctx);");
                sb.AppendLine($"                break;");
                sb.AppendLine();
                break;
        }
    }

    private static string GetSimpleTypeName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }
}

// ═══════════════════════════════════════════════════════════
// Modelle
// ═══════════════════════════════════════════════════════════

internal enum PipelineHandlerKind
{
    /// <summary>IEnumerable&lt;ICommand&gt; — sync, yields Commands</summary>
    Enumerable,

    /// <summary>IAsyncEnumerable&lt;ICommand&gt; — async, yields Commands</summary>
    AsyncEnumerable,

    /// <summary>Task — Fire-and-Forget, nur Seiteneffekte</summary>
    Task,
}

internal class PipelineHandlerInfo
{
    public string InputTypeName { get; }
    public PipelineHandlerKind Kind { get; }
    public List<string> ProducedTypes { get; }

    public PipelineHandlerInfo(string inputTypeName, PipelineHandlerKind kind, List<string> producedTypes)
    {
        InputTypeName = inputTypeName;
        Kind = kind;
        ProducedTypes = producedTypes;
    }
}

internal class PipelineGeneratorModel
{
    public string PipelineNamespace { get; }
    public string PipelineName { get; }
    public List<PipelineHandlerInfo> TriggerHandlers { get; }
    public List<PipelineHandlerInfo> EventHandlers { get; }
    public List<string> AllNamespaces { get; }

    public PipelineGeneratorModel(
        string pipelineNamespace,
        string pipelineName,
        List<PipelineHandlerInfo> triggerHandlers,
        List<PipelineHandlerInfo> eventHandlers,
        List<string> allNamespaces)
    {
        PipelineNamespace = pipelineNamespace;
        PipelineName = pipelineName;
        TriggerHandlers = triggerHandlers;
        EventHandlers = eventHandlers;
        AllNamespaces = allNamespaces;
    }
}