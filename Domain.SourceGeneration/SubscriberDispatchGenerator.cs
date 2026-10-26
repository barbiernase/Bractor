using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Domain.SourceGeneration;

/// <summary>
/// Generiert für jede Projektion (ISubscriber):
/// - SubscribedTypes (statische Liste der Input-Event-Typen)
/// - DispatchAsync (Event-Routing via switch)
/// 
/// ★ Phase 3: Handle-Signatur erkennt IAggregateEnvelope statt IMessageEnvelope.
///   Events haben immer Aggregate-Kontext → Writer-Handler nutzen IAggregateEnvelope.
///   Reader-Handler (IMessageEnvelope) werden vom ProjectionReaderDispatchGenerator behandelt.
///
/// Erkennung: Handle-Methoden mit Signatur:
///   (TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…) → Task. Eine Projektion gibt nichts aus — ihre Wirkung ist ihr Store
///   (eine andere Rückgabe meldet CQRS057).
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
        //   (TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…) → Task
        //   Fähigkeit = Interface mit Marker IWriteStore/IReadStore (0…n) — der Dispatch löst sie auf.
        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length >= 3 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, aggregateEnvelopeType) &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[2].Type, projectionWriterType) &&
                        m.Parameters.Skip(3).All(p => FaehigkeitsTypen.IstFaehigkeit(p.Type, context.SemanticModel.Compilation)) &&
                        m.ReturnType is INamedTypeSymbol { Name: "Task", IsGenericType: false })
            .ToList();

        if (handleMethods.Count == 0)
            return null;

        // Pro Handler: Input-Typ + Rückgabetyp analysieren
        var handlers = new List<HandlerInfo>();
        var allEventNamespaces = new HashSet<string>();

        foreach (var method in handleMethods)
        {
            var inputType = method.Parameters[0].Type;

            var inputTypeName = inputType.ToDisplayString();
            var inputNamespace = inputType.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(inputNamespace))
                allEventNamespaces.Add(inputNamespace);

            var handlerInfo = new HandlerInfo(inputTypeName);
            handlerInfo.Faehigkeiten.AddRange(FaehigkeitsTypen.Argumente(method, 3));
            handlers.Add(handlerInfo);
        }

        if (handlers.Count == 0)
            return null;

        return new SubscriberGeneratorModel(
            subscriberNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
            subscriberName: classSymbol.Name,
            handlers: handlers,
            eventNamespaces: allEventNamespaces.ToList(),
            codeHash: Kodeform.Hash(classSymbol)
        );
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
        sb.AppendLine("    /// <summary>Code-Hash dieser Klasse: ändert er sich (oder der Store), baut eine Sim-Umgebung die Projektion beim Start neu auf.</summary>");
        sb.AppendLine($"    public const string CodeHash = \"{model.CodeHash}\";");
        sb.AppendLine();

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

        // ── DispatchAsync ──
        // ★ Phase 3: IAggregateEnvelope statt IMessageEnvelope
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Dispatch an den richtigen Handler basierend auf Payload-Typ.");
        sb.AppendLine("    /// Jeder Handler schreibt nur in seinen Store (über die Fähigkeiten) — eine Projektion gibt nichts aus.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async Task DispatchAsync(");
        sb.AppendLine("        IAggregateEnvelope envelope,");
        sb.AppendLine("        ProjectionWriter writer,");
        sb.AppendLine("        IFaehigkeiten faehigkeiten)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (envelope.Payload)");
        sb.AppendLine("        {");

        foreach (var handler in model.Handlers)
        {
            var simpleName = GetSimpleTypeName(handler.InputTypeName);

            sb.AppendLine($"            case {simpleName} e:");
            sb.AppendLine($"                await Handle(e, envelope, writer{handler.FaehigkeitsArgumente});");
            sb.AppendLine($"                break;");
            sb.AppendLine();
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

internal class HandlerInfo
{
    public string InputTypeName { get; }
    /// <summary>Fähigkeits-Parameter (voll qualifiziert) in Signatur-Reihenfolge.</summary>
    public List<string> Faehigkeiten { get; } = new();
    public string FaehigkeitsArgumente => FaehigkeitsTypen.ArgumentListe(Faehigkeiten);

    public HandlerInfo(string inputTypeName)
    {
        InputTypeName = inputTypeName;
    }
}

internal class SubscriberGeneratorModel
{
    public string SubscriberNamespace { get; }
    public string SubscriberName { get; }
    public List<HandlerInfo> Handlers { get; }
    public List<string> EventNamespaces { get; }
    /// <summary>Code-Hash der Projektions-Klasse (alle Teile) — Teil des Projektions-Stands (konzept-werkbank §12.3 E9).</summary>
    public string CodeHash { get; }

    public SubscriberGeneratorModel(
        string subscriberNamespace,
        string subscriberName,
        List<HandlerInfo> handlers,
        List<string> eventNamespaces,
        string codeHash)
    {
        CodeHash = codeHash;
        SubscriberNamespace = subscriberNamespace;
        SubscriberName = subscriberName;
        Handlers = handlers;
        EventNamespaces = eventNamespaces;
    }
}