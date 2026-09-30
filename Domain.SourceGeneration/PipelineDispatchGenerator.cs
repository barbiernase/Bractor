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
///   (T, PipelineContext, Fähigkeit…) → (Async)Enumerable&lt;OneOf&lt;…&gt;&gt; | Task
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
        var iPipelineSelfType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IPipelineSelfMessage");
        var iEventType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IEvent");
        var iCommandType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.ICommand");

        // Planungs-Typen über das Symbol (nicht über den Namen): Frist<TCmd> / FristStorno<TCmd>.
        var fristTyp = context.SemanticModel.Compilation.GetTypeByMetadataName("Abstractions.Frist`1");
        var stornoTyp = context.SemanticModel.Compilation.GetTypeByMetadataName("Abstractions.FristStorno`1");

        if (pipelineContextType == null || iPipelineTriggerType == null ||
            iEventType == null || iCommandType == null)
            return null;

        // Handle-Methoden finden: (T, PipelineContext)
        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length >= 2 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, pipelineContextType) &&
                        m.Parameters.Skip(2).All(p => FaehigkeitsTypen.IstFaehigkeit(p.Type, context.SemanticModel.Compilation)))
            .ToList();

        if (handleMethods.Count == 0)
            return null;

        var triggerHandlers = new List<PipelineHandlerInfo>();
        var eventHandlers = new List<PipelineHandlerInfo>();
        var selfHandlers = new List<PipelineHandlerInfo>();
        var allNamespaces = new HashSet<string>();

        foreach (var method in handleMethods)
        {
            var inputType = method.Parameters[0].Type;
            var returnType = method.ReturnType;

            var inputTypeName = inputType.ToDisplayString();
            var inputNamespace = inputType.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(inputNamespace))
                allNamespaces.Add(inputNamespace);

            var handlerInfo = AnalyzeReturnType(returnType, inputTypeName, allNamespaces, fristTyp, stornoTyp);
            handlerInfo.FaehigkeitsArgumente = FaehigkeitsTypen.ArgumentListe(FaehigkeitsTypen.Argumente(method, 2));

            // Kanal bestimmen: IPipelineSelfMessage, IPipelineTrigger oder IEvent?
            // Self-Messages zuerst prüfen (könnten theoretisch auch Trigger sein,
            // aber Self hat Vorrang — Self-Messages sind Pipeline-intern)
            if (iPipelineSelfType != null &&
                (inputType.AllInterfaces.Contains(iPipelineSelfType, SymbolEqualityComparer.Default)
                 || SymbolEqualityComparer.Default.Equals(inputType, iPipelineSelfType)))
            {
                selfHandlers.Add(handlerInfo);
            }
            else if (inputType.AllInterfaces.Contains(iPipelineTriggerType, SymbolEqualityComparer.Default)
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

        if (triggerHandlers.Count == 0 && eventHandlers.Count == 0 && selfHandlers.Count == 0)
            return null;

        return new PipelineGeneratorModel(
            pipelineNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
            pipelineName: classSymbol.Name,
            triggerHandlers: triggerHandlers,
            eventHandlers: eventHandlers,
            selfHandlers: selfHandlers,
            allNamespaces: allNamespaces.ToList()
        );
    }

    /// <summary>
    /// Analysiert den Rückgabetyp einer Handle-Methode: Task, IEnumerable&lt;OneOf&lt;...&gt;&gt;,
    /// IAsyncEnumerable&lt;OneOf&lt;...&gt;&gt;. Ein offener Element-Typ (<c>ICommand</c> …) ist CQRS050 und
    /// kommt hier nicht mehr an.
    /// </summary>
    private static PipelineHandlerInfo AnalyzeReturnType(
        ITypeSymbol returnType, string inputTypeName, HashSet<string> namespaces,
        INamedTypeSymbol? fristTyp, INamedTypeSymbol? stornoTyp)
    {
        if (returnType is INamedTypeSymbol { IsGenericType: true } folge && IsOneOfType(folge.TypeArguments[0]))
        {
            var def = folge.OriginalDefinition.ToDisplayString();
            var kind = def.StartsWith("System.Collections.Generic.IAsyncEnumerable") ? PipelineHandlerKind.OneOfAsyncEnumerable
                : def.StartsWith("System.Collections.Generic.IEnumerable") ? PipelineHandlerKind.OneOfEnumerable
                : (PipelineHandlerKind?)null;
            if (kind is { } k)
            {
                var info = new PipelineHandlerInfo(inputTypeName, k, new List<string>());
                // Frist<TCmd>/FristStorno<TCmd>: der Kontext (Command-Typname) wird hier als Konstante eingesetzt.
                foreach (var a in ((INamedTypeSymbol)folge.TypeArguments[0]).TypeArguments.OfType<INamedTypeSymbol>())
                {
                    var istFrist = SymbolEqualityComparer.Default.Equals(a.OriginalDefinition, fristTyp);
                    var istStorno = SymbolEqualityComparer.Default.Equals(a.OriginalDefinition, stornoTyp);
                    if (istFrist || istStorno)
                        info.Fristen.Add((a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            a.TypeArguments[0].ToDisplayString(), istStorno));
                }
                return info;
            }
        }

        // Task (Fire-and-Forget, nur Seiteneffekte)
        return new PipelineHandlerInfo(inputTypeName, PipelineHandlerKind.Task, new List<string>());
    }

    /// <summary>
    /// Prüft ob ein Typ ein OneOf&lt;...&gt; aus dem Abstractions-Namespace ist.
    /// </summary>
    private static bool IsOneOfType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true, IsValueType: true } named
               && named.Name == "OneOf"
               && named.ContainingNamespace?.ToDisplayString() == "Abstractions";
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
        sb.AppendLine("        Func<ICommand, Task> sendCommand,");
        sb.AppendLine("        Func<IPipelineTrigger, Task> sendTrigger,");
        sb.AppendLine("        Func<ITransientEvent, Task> broadcastTransient,");
        sb.AppendLine("        Func<IPlanung, Task> plane,");
        sb.AppendLine("        IFaehigkeiten faehigkeiten)");
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
                EmitDispatchCase(sb, simpleName, "t", handler);
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
        sb.AppendLine("        Func<ICommand, Task> sendCommand,");
        sb.AppendLine("        Func<IPipelineTrigger, Task> sendTrigger,");
        sb.AppendLine("        Func<ITransientEvent, Task> broadcastTransient,");
        sb.AppendLine("        Func<IPlanung, Task> plane,");
        sb.AppendLine("        IFaehigkeiten faehigkeiten)");
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
                EmitDispatchCase(sb, simpleName, "e", handler);
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // ── DispatchSelfAsync ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Dispatch für Self-Messages (ScheduleSelf). Switch über IPipelineSelfMessage-Typen.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async Task DispatchSelfAsync(");
        sb.AppendLine("        IPipelineSelfMessage selfMsg,");
        sb.AppendLine("        PipelineContext ctx,");
        sb.AppendLine("        Func<ICommand, Task> sendCommand,");
        sb.AppendLine("        Func<IPipelineTrigger, Task> sendTrigger,");
        sb.AppendLine("        Func<ITransientEvent, Task> broadcastTransient,");
        sb.AppendLine("        Func<IPlanung, Task> plane,");
        sb.AppendLine("        IFaehigkeiten faehigkeiten)");
        sb.AppendLine("    {");

        if (model.SelfHandlers.Count == 0)
        {
            sb.AppendLine("        await Task.CompletedTask;");
        }
        else
        {
            sb.AppendLine("        switch (selfMsg)");
            sb.AppendLine("        {");

            foreach (var handler in model.SelfHandlers)
            {
                var simpleName = GetSimpleTypeName(handler.InputTypeName);
                EmitDispatchCase(sb, simpleName, "s", handler);
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitDispatchCase(
        StringBuilder sb, string typeName, string varName, PipelineHandlerInfo handler)
    {
        var aufruf = $"Handle({varName}, ctx{handler.FaehigkeitsArgumente})";
        switch (handler.Kind)
        {
            case PipelineHandlerKind.Task:
                sb.AppendLine($"            case {typeName} {varName}:");
                sb.AppendLine($"                await {aufruf};");
                sb.AppendLine($"                break;");
                sb.AppendLine();
                break;

            case PipelineHandlerKind.OneOfEnumerable:
                sb.AppendLine($"            case {typeName} {varName}:");
                sb.AppendLine($"                foreach (var oneOf in {aufruf})");
                sb.AppendLine($"                {{");
                EmitOneOfSwitch(sb, handler);
                sb.AppendLine($"                }}");
                sb.AppendLine($"                break;");
                sb.AppendLine();
                break;

            case PipelineHandlerKind.OneOfAsyncEnumerable:
                sb.AppendLine($"            case {typeName} {varName}:");
                sb.AppendLine($"                await foreach (var oneOf in {aufruf})");
                sb.AppendLine($"                {{");
                EmitOneOfSwitch(sb, handler);
                sb.AppendLine($"                }}");
                sb.AppendLine($"                break;");
                sb.AppendLine();
                break;
        }
    }

    /// <summary>
    /// Generiert den inneren Switch für OneOf-Output-Routing.
    /// Reihenfolge wichtig: ITransientEvent vor IEvent prüfen (ITransientEvent : IEvent).
    /// </summary>
    private static void EmitOneOfSwitch(StringBuilder sb, PipelineHandlerInfo handler)
    {
        sb.AppendLine($"                    switch (oneOf.Value)");
        sb.AppendLine($"                    {{");
        foreach (var (typ, cmd, storno) in handler.Fristen)
        {
            sb.AppendLine($"                        case {typ} __frist:");
            sb.AppendLine(storno
                ? $"                            await plane(new FristAuftrag(\"{cmd}\", __frist.ZielAggregatId, null));"
                : $"                            await plane(new FristAuftrag(\"{cmd}\", __frist.ZielAggregatId, __frist.Dauer));");
            sb.AppendLine($"                            break;");
        }
        sb.AppendLine($"                        case ISelbstPlanung __selbst:");
        sb.AppendLine($"                            await plane(__selbst);");
        sb.AppendLine($"                            break;");
        sb.AppendLine($"                        case ICommand cmd:");
        sb.AppendLine($"                            await sendCommand(cmd);");
        sb.AppendLine($"                            break;");
        sb.AppendLine($"                        case IPipelineTrigger trig:");
        sb.AppendLine($"                            await sendTrigger(trig);");
        sb.AppendLine($"                            break;");
        sb.AppendLine($"                        case ITransientEvent te:");
        sb.AppendLine($"                            await broadcastTransient(te);");
        sb.AppendLine($"                            break;");
        sb.AppendLine($"                    }}");
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
    /// <summary>Task — Fire-and-Forget, nur Seiteneffekte</summary>
    Task,

    /// <summary>IEnumerable&lt;OneOf&lt;...&gt;&gt; — sync, yields gemischte Outputs</summary>
    OneOfEnumerable,

    /// <summary>IAsyncEnumerable&lt;OneOf&lt;...&gt;&gt; — async, yields gemischte Outputs</summary>
    OneOfAsyncEnumerable,
}

internal class PipelineHandlerInfo
{
    public string InputTypeName { get; }
    public PipelineHandlerKind Kind { get; }
    public List<string> ProducedTypes { get; }
    /// <summary>„, faehigkeiten.Hole&lt;…&gt;()" je Fähigkeits-Parameter.</summary>
    public string FaehigkeitsArgumente { get; set; } = "";
    /// <summary>Frist-Varianten des OneOf: (Typ voll qualifiziert, Command-Typname = Kontext, Storno?).</summary>
    public List<(string Typ, string Cmd, bool Storno)> Fristen { get; } = new();

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
    public List<PipelineHandlerInfo> SelfHandlers { get; }
    public List<string> AllNamespaces { get; }

    public PipelineGeneratorModel(
        string pipelineNamespace,
        string pipelineName,
        List<PipelineHandlerInfo> triggerHandlers,
        List<PipelineHandlerInfo> eventHandlers,
        List<PipelineHandlerInfo> selfHandlers,
        List<string> allNamespaces)
    {
        PipelineNamespace = pipelineNamespace;
        PipelineName = pipelineName;
        TriggerHandlers = triggerHandlers;
        EventHandlers = eventHandlers;
        SelfHandlers = selfHandlers;
        AllNamespaces = allNamespaces;
    }
}