using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Domain.SourceGeneration;

/// <summary>
/// Generiert für jede Projektion (ISubscriber):
/// - SubscribedTypes (statische Liste der Input-Event-Typen)
/// - ProducedTypes (statische Liste der Output-Event-Typen)
/// - DispatchAsync (Event-Routing via switch, mit emit-Callback)
/// 
/// ★ Phase 3: Handle-Signatur erkennt IAggregateEnvelope statt IMessageEnvelope.
///   Events haben immer Aggregate-Kontext → Writer-Handler nutzen IAggregateEnvelope.
///   Reader-Handler (IMessageEnvelope) werden vom ProjectionReaderDispatchGenerator behandelt.
///
/// Erkennung: Handle-Methoden mit Signatur:
///   (TEvent, IAggregateEnvelope, ProjectionWriter) → Task | IAsyncEnumerable&lt;T&gt; | IAsyncEnumerable&lt;OneOf&lt;...&gt;&gt;
/// </summary>
[Generator]
public class SubscriberDispatchGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var subscriberProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial"),
                transform: static (ctx, _) => GetSubscriberInfo(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(subscriberProvider,
            static (spc, model) => Execute(spc, model!));
    }

    private static SubscriberGeneratorModel? GetSubscriberInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

        if (classSymbol == null)
            return null;

        // Muss ISubscriber implementieren
        var subscriberInterface = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.ISubscriber");

        if (subscriberInterface == null)
            return null;

        var implementsSubscriber = classSymbol.AllInterfaces
            .Any(i => SymbolEqualityComparer.Default.Equals(i, subscriberInterface));

        if (!implementsSubscriber)
            return null;

        // ★ Phase 3: IAggregateEnvelope statt IMessageEnvelope
        var aggregateEnvelopeType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IAggregateEnvelope");
        var projectionWriterType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Core.ProjectionWriter");

        if (aggregateEnvelopeType == null || projectionWriterType == null)
            return null;

        // Handle-Methoden finden:
        //   (TEvent, IAggregateEnvelope, ProjectionWriter) → Task | IAsyncEnumerable<T> | IAsyncEnumerable<OneOf<...>>
        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 3 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, aggregateEnvelopeType) &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[2].Type, projectionWriterType))
            .ToList();

        if (handleMethods.Count == 0)
            return null;

        // Pro Handler: Input-Typ + Rückgabetyp analysieren
        var handlers = new List<HandlerInfo>();
        var allEventNamespaces = new HashSet<string>();

        foreach (var method in handleMethods)
        {
            var inputType = method.Parameters[0].Type;
            var returnType = method.ReturnType;

            var inputTypeName = inputType.ToDisplayString();
            var inputNamespace = inputType.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(inputNamespace))
                allEventNamespaces.Add(inputNamespace);

            var handlerInfo = AnalyzeReturnType(returnType, inputTypeName, allEventNamespaces);
            handlers.Add(handlerInfo);
        }

        if (handlers.Count == 0)
            return null;

        return new SubscriberGeneratorModel(
            subscriberNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
            subscriberName: classSymbol.Name,
            handlers: handlers,
            eventNamespaces: allEventNamespaces.ToList()
        );
    }

    /// <summary>
    /// Analysiert den Rückgabetyp einer Handle-Methode und extrahiert die Handler-Kategorie.
    /// </summary>
    private static HandlerInfo AnalyzeReturnType(
        ITypeSymbol returnType, string inputTypeName, HashSet<string> namespaces)
    {
        // Task → async, nicht-reaktiv
        if (returnType.Name == "Task" && returnType is INamedTypeSymbol { IsGenericType: false })
        {
            return new HandlerInfo(inputTypeName, HandlerKind.Task, new List<string>());
        }

        // IAsyncEnumerable<T> oder IAsyncEnumerable<OneOf<T1,T2,...>>
        if (returnType is INamedTypeSymbol { IsGenericType: true } asyncEnum
            && asyncEnum.OriginalDefinition.ToDisplayString()
                .StartsWith("System.Collections.Generic.IAsyncEnumerable"))
        {
            var elementType = asyncEnum.TypeArguments[0];

            // IAsyncEnumerable<OneOf<T1, T2, ...>>
            if (elementType is INamedTypeSymbol { Name: "OneOf", IsGenericType: true } oneOf)
            {
                var producedTypes = new List<string>();
                foreach (var typeArg in oneOf.TypeArguments)
                {
                    producedTypes.Add(typeArg.ToDisplayString());
                    var ns = typeArg.ContainingNamespace?.ToDisplayString();
                    if (!string.IsNullOrEmpty(ns))
                        namespaces.Add(ns);
                }
                return new HandlerInfo(inputTypeName, HandlerKind.AsyncEnumerableOneOf, producedTypes);
            }

            // IAsyncEnumerable<T> (einzelner Typ)
            {
                var producedTypes = new List<string> { elementType.ToDisplayString() };
                var ns = elementType.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrEmpty(ns))
                    namespaces.Add(ns);
                return new HandlerInfo(inputTypeName, HandlerKind.AsyncEnumerable, producedTypes);
            }
        }

        // Fallback: unbekannter Rückgabetyp → behandle wie Task (nicht-reaktiv)
        return new HandlerInfo(inputTypeName, HandlerKind.Task, new List<string>());
    }

    private static void Execute(SourceProductionContext context, SubscriberGeneratorModel model)
    {
        var source = GenerateSubscriberDispatch(model);
        context.AddSource($"{model.SubscriberName}.Subscriber.Dispatch.g.cs", source);
    }

    private static string GenerateSubscriberDispatch(SubscriberGeneratorModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Abstractions;");
        sb.AppendLine("using Core;");

        // Event-Namespaces hinzufügen
        foreach (var ns in model.EventNamespaces.OrderBy(n => n))
        {
            if (ns != model.SubscriberNamespace &&
                ns != "Abstractions" &&
                ns != "Core" &&
                ns != "System")
            {
                sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {model.SubscriberNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.SubscriberName}");
        sb.AppendLine("{");

        // ── SubscribedTypes ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Event-Typen auf die dieser Subscriber hört (aus Handle-Methoden extrahiert).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyList<Type> SubscribedTypes { get; } = new[]");
        sb.AppendLine("    {");
        foreach (var handler in model.Handlers)
        {
            var simpleName = GetSimpleTypeName(handler.InputTypeName);
            sb.AppendLine($"        typeof({simpleName}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        // ── ProducedTypes ──
        var allProducedTypes = model.Handlers
            .SelectMany(h => h.ProducedTypes)
            .Distinct()
            .ToList();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Event-Typen die dieser Subscriber produzieren kann (aus Rückgabetypen extrahiert).");
        sb.AppendLine("    /// </summary>");
        if (allProducedTypes.Count == 0)
        {
            sb.AppendLine("    public static IReadOnlyList<Type> ProducedTypes { get; } = Array.Empty<Type>();");
        }
        else
        {
            sb.AppendLine("    public static IReadOnlyList<Type> ProducedTypes { get; } = new[]");
            sb.AppendLine("    {");
            foreach (var producedType in allProducedTypes)
            {
                var simpleName = GetSimpleTypeName(producedType);
                sb.AppendLine($"        typeof({simpleName}),");
            }
            sb.AppendLine("    };");
        }
        sb.AppendLine();

        // ── DispatchAsync ──
        // ★ Phase 3: IAggregateEnvelope statt IMessageEnvelope
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Dispatch an den richtigen Handler basierend auf Payload-Typ.");
        sb.AppendLine("    /// Task-Handler: await. IAsyncEnumerable-Handler: iterate + emit. OneOf: unwrap + emit.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async Task DispatchAsync(");
        sb.AppendLine("        IAggregateEnvelope envelope,");
        sb.AppendLine("        ProjectionWriter writer,");
        sb.AppendLine("        Func<IEvent, Task> emit)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (envelope.Payload)");
        sb.AppendLine("        {");

        foreach (var handler in model.Handlers)
        {
            var simpleName = GetSimpleTypeName(handler.InputTypeName);

            switch (handler.Kind)
            {
                case HandlerKind.Task:
                    sb.AppendLine($"            case {simpleName} e:");
                    sb.AppendLine($"                await Handle(e, envelope, writer);");
                    sb.AppendLine($"                break;");
                    sb.AppendLine();
                    break;

                case HandlerKind.AsyncEnumerable:
                    sb.AppendLine($"            case {simpleName} e:");
                    sb.AppendLine($"                await foreach (var result in Handle(e, envelope, writer))");
                    sb.AppendLine($"                    await emit(result);");
                    sb.AppendLine($"                break;");
                    sb.AppendLine();
                    break;

                case HandlerKind.AsyncEnumerableOneOf:
                    // OneOf.Value ist jetzt IMessagePayload (Phase 1), cast zu IEvent
                    sb.AppendLine($"            case {simpleName} e:");
                    sb.AppendLine($"                await foreach (var oneOf in Handle(e, envelope, writer))");
                    sb.AppendLine($"                    await emit((IEvent)oneOf.Value);");
                    sb.AppendLine($"                break;");
                    sb.AppendLine();
                    break;
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
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

internal enum HandlerKind
{
    /// <summary>async Task — nicht-reaktiv, produziert keine Events</summary>
    Task,

    /// <summary>async IAsyncEnumerable&lt;T&gt; — reaktiv, produziert Events vom Typ T</summary>
    AsyncEnumerable,

    /// <summary>async IAsyncEnumerable&lt;OneOf&lt;T1,T2,...&gt;&gt; — reaktiv, produziert Union-Events</summary>
    AsyncEnumerableOneOf,
}

internal class HandlerInfo
{
    public string InputTypeName { get; }
    public HandlerKind Kind { get; }
    public List<string> ProducedTypes { get; }

    public HandlerInfo(string inputTypeName, HandlerKind kind, List<string> producedTypes)
    {
        InputTypeName = inputTypeName;
        Kind = kind;
        ProducedTypes = producedTypes;
    }
}

internal class SubscriberGeneratorModel
{
    public string SubscriberNamespace { get; }
    public string SubscriberName { get; }
    public List<HandlerInfo> Handlers { get; }
    public List<string> EventNamespaces { get; }

    public SubscriberGeneratorModel(
        string subscriberNamespace,
        string subscriberName,
        List<HandlerInfo> handlers,
        List<string> eventNamespaces)
    {
        SubscriberNamespace = subscriberNamespace;
        SubscriberName = subscriberName;
        Handlers = handlers;
        EventNamespaces = eventNamespaces;
    }
}