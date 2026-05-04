// ═══════════════════════════════════════════════════════════════════
// HandleMethodGenerator
//
// Erkennt Klassen mit Handle(TEvent, MessageContext)-Methoden.
// Unterscheidung am Rückgabetyp:
//
//   void Handle(TEvent, MessageContext)
//     → Store: Dispatch(object, MessageContext) + SubscribedTypes
//
//   IEnumerable<T> Handle(TEvent, MessageContext)
//     → Handler (sync): Dispatch(object, MessageContext, Action<object>)
//       + SubscribedTypes + ProducedTypes
//
//   IAsyncEnumerable<T> Handle(TEvent, MessageContext)
//     → Handler (async): DispatchAsync(object, MessageContext, Func<object, Task>)
//       + SubscribedTypes + ProducedTypes
// ═══════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Client.SourceGeneration;

[Generator]
public class HandleMethodGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial"),
                transform: static (ctx, _) => AnalyzeClass(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(classProvider,
            static (spc, model) => Execute(spc, model!));
    }

    private static HandleClassModel? AnalyzeClass(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (classSymbol == null) return null;

        // MessageContext-Typ finden
        var messageContextType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Client.Infrastructure.Abstractions.MessageContext");
        if (messageContextType == null) return null;

        // Alle Handle-Methoden mit (TEvent, MessageContext)
        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 2 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, messageContextType))
            .ToList();

        if (handleMethods.Count == 0) return null;

        var storeHandlers = new List<HandleMethodInfo>();
        var syncHandlers = new List<HandleMethodInfo>();
        var asyncHandlers = new List<HandleMethodInfo>();
        var allNamespaces = new HashSet<string>();

        foreach (var method in handleMethods)
        {
            var inputType = method.Parameters[0].Type;
            var inputTypeFqn = inputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var inputTypeShort = inputType.Name;
            var returnType = method.ReturnType;

            CollectNamespace(inputType, allNamespaces);

            // void → Store
            if (returnType.SpecialType == SpecialType.System_Void)
            {
                storeHandlers.Add(new HandleMethodInfo(inputTypeFqn, inputTypeShort, null));
                continue;
            }

            // IEnumerable<T> → sync Handler
            if (returnType is INamedTypeSymbol { IsGenericType: true } enumerable &&
                enumerable.OriginalDefinition.ToDisplayString()
                    .StartsWith("System.Collections.Generic.IEnumerable"))
            {
                var producedType = enumerable.TypeArguments[0];
                CollectNamespace(producedType, allNamespaces);
                syncHandlers.Add(new HandleMethodInfo(
                    inputTypeFqn, inputTypeShort, producedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                continue;
            }

            // IAsyncEnumerable<T> → async Handler
            if (returnType is INamedTypeSymbol { IsGenericType: true } asyncEnum &&
                asyncEnum.OriginalDefinition.ToDisplayString()
                    .StartsWith("System.Collections.Generic.IAsyncEnumerable"))
            {
                var producedType = asyncEnum.TypeArguments[0];
                CollectNamespace(producedType, allNamespaces);
                asyncHandlers.Add(new HandleMethodInfo(
                    inputTypeFqn, inputTypeShort, producedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                continue;
            }
        }

        if (storeHandlers.Count == 0 && syncHandlers.Count == 0 && asyncHandlers.Count == 0)
            return null;

        // Klassen-Kategorie bestimmen
        HandleClassKind kind;
        if (storeHandlers.Count > 0 && syncHandlers.Count == 0 && asyncHandlers.Count == 0)
            kind = HandleClassKind.Store;
        else if (storeHandlers.Count == 0)
            kind = HandleClassKind.Handler;
        else
            kind = HandleClassKind.Mixed; // sollte nicht vorkommen, aber sicher ist sicher

        return new HandleClassModel(
            classNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
            className: classSymbol.Name,
            kind: kind,
            storeHandlers: storeHandlers,
            syncHandlers: syncHandlers,
            asyncHandlers: asyncHandlers,
            namespaces: allNamespaces.ToList());
    }

    private static void CollectNamespace(ITypeSymbol type, HashSet<string> namespaces)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
            namespaces.Add(ns!);
    }

    private static void Execute(SourceProductionContext context, HandleClassModel model)
    {
        var source = model.Kind switch
        {
            HandleClassKind.Store => GenerateStoreDispatch(model),
            HandleClassKind.Handler => GenerateHandlerDispatch(model),
            _ => GenerateMixedDispatch(model),
        };

        context.AddSource($"{model.ClassName}.Dispatch.g.cs", source);
    }

    // ═══════════════════════════════════════════════════
    // Store: void Handle → Dispatch(object, MessageContext)
    // ═══════════════════════════════════════════════════

    private static string GenerateStoreDispatch(HandleClassModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generator: HandleMethodGenerator (Store)");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using global::Client.Infrastructure.Abstractions;");

        foreach (var ns in model.Namespaces.OrderBy(n => n))
        {
            if (ns != model.ClassNamespace)
                sb.AppendLine($"using global::{ns};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {model.ClassNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ClassName}");
        sb.AppendLine("{");

        // SubscribedTypes
        sb.AppendLine("    public static IReadOnlyList<Type> SubscribedTypes { get; } = new[]");
        sb.AppendLine("    {");
        foreach (var h in model.StoreHandlers)
            sb.AppendLine($"        typeof({h.InputTypeFqn}),");
        sb.AppendLine("    };");
        sb.AppendLine();

        // Dispatch
        sb.AppendLine("    public void Dispatch(object message, MessageContext ctx)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (message)");
        sb.AppendLine("        {");
        foreach (var h in model.StoreHandlers)
            sb.AppendLine($"            case {h.InputTypeFqn} evt: Handle(evt, ctx); break;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════
    // Handler: IEnumerable<T>/IAsyncEnumerable<T> Handle
    // → Dispatch + publish loop
    // ═══════════════════════════════════════════════════

    private static string GenerateHandlerDispatch(HandleClassModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generator: HandleMethodGenerator (Handler)");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using global::Client.Infrastructure.Abstractions;");

        foreach (var ns in model.Namespaces.OrderBy(n => n))
        {
            if (ns != model.ClassNamespace)
                sb.AppendLine($"using global::{ns};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {model.ClassNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ClassName}");
        sb.AppendLine("{");

        // SubscribedTypes
        var allHandlers = model.SyncHandlers.Concat(model.AsyncHandlers).ToList();
        sb.AppendLine("    public static IReadOnlyList<Type> SubscribedTypes { get; } = new[]");
        sb.AppendLine("    {");
        foreach (var h in allHandlers)
            sb.AppendLine($"        typeof({h.InputTypeFqn}),");
        sb.AppendLine("    };");
        sb.AppendLine();

        // ProducedTypes
        var producedTypes = allHandlers
            .Where(h => h.ProducedTypeFqn != null)
            .Select(h => h.ProducedTypeFqn!)
            .Distinct()
            .ToList();

        sb.AppendLine("    public static IReadOnlyList<Type> ProducedTypes { get; } = new[]");
        sb.AppendLine("    {");
        foreach (var pt in producedTypes)
            sb.AppendLine($"        typeof({pt}),");
        sb.AppendLine("    };");
        sb.AppendLine();

        // Sync Dispatch
        if (model.SyncHandlers.Count > 0)
        {
            // Gruppiere nach produziertem Typ für korrekte IEnumerable<T>
            var producedBySync = model.SyncHandlers
                .Select(h => h.ProducedTypeFqn!)
                .Distinct()
                .First(); // in der Regel ein Typ pro Handler

            sb.AppendLine("    public void Dispatch(object message, MessageContext ctx, Action<object> publish)");
            sb.AppendLine("    {");
            sb.AppendLine($"        IEnumerable<{producedBySync}>? results = message switch");
            sb.AppendLine("        {");
            foreach (var h in model.SyncHandlers)
                sb.AppendLine($"            {h.InputTypeFqn} evt => Handle(evt, ctx),");
            sb.AppendLine("            _ => null,");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        if (results == null) return;");
            sb.AppendLine();
            sb.AppendLine("        foreach (var produced in results)");
            sb.AppendLine("            publish(produced);");
            sb.AppendLine("    }");
        }

        // Async Dispatch
        if (model.AsyncHandlers.Count > 0)
        {
            var producedByAsync = model.AsyncHandlers
                .Select(h => h.ProducedTypeFqn!)
                .Distinct()
                .First();

            sb.AppendLine();
            sb.AppendLine("    public async Task DispatchAsync(object message, MessageContext ctx, Func<object, Task> publish)");
            sb.AppendLine("    {");
            sb.AppendLine($"        IAsyncEnumerable<{producedByAsync}>? results = message switch");
            sb.AppendLine("        {");
            foreach (var h in model.AsyncHandlers)
                sb.AppendLine($"            {h.InputTypeFqn} evt => Handle(evt, ctx),");
            sb.AppendLine("            _ => null,");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        if (results == null) return;");
            sb.AppendLine();
            sb.AppendLine("        await foreach (var produced in results)");
            sb.AppendLine("            await publish(produced);");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateMixedDispatch(HandleClassModel model)
    {
        // Fallback: generiere beide Dispatch-Varianten
        // Sollte in der Praxis nicht vorkommen (Store hat nur void, Handler nur IEnumerable)
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// WARNING: {model.ClassName} has mixed void and IEnumerable Handle methods.");
        sb.AppendLine("// Consider splitting into separate Store and Handler classes.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.ClassNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    // Mixed dispatch not fully supported — split into Store + Handler.");
        sb.AppendLine("}");
        return sb.ToString();
    }
}

// ═══════════════════════════════════════════════════
// Modelle
// ═══════════════════════════════════════════════════

internal enum HandleClassKind { Store, Handler, Mixed }

internal class HandleMethodInfo
{
    public string InputTypeFqn { get; }
    public string InputTypeShort { get; }
    public string? ProducedTypeFqn { get; }

    public HandleMethodInfo(string inputTypeFqn, string inputTypeShort, string? producedTypeFqn)
    {
        InputTypeFqn = inputTypeFqn;
        InputTypeShort = inputTypeShort;
        ProducedTypeFqn = producedTypeFqn;
    }
}

internal class HandleClassModel
{
    public string ClassNamespace { get; }
    public string ClassName { get; }
    public HandleClassKind Kind { get; }
    public List<HandleMethodInfo> StoreHandlers { get; }
    public List<HandleMethodInfo> SyncHandlers { get; }
    public List<HandleMethodInfo> AsyncHandlers { get; }
    public List<string> Namespaces { get; }

    public HandleClassModel(
        string classNamespace, string className, HandleClassKind kind,
        List<HandleMethodInfo> storeHandlers,
        List<HandleMethodInfo> syncHandlers,
        List<HandleMethodInfo> asyncHandlers,
        List<string> namespaces)
    {
        ClassNamespace = classNamespace;
        ClassName = className;
        Kind = kind;
        StoreHandlers = storeHandlers;
        SyncHandlers = syncHandlers;
        AsyncHandlers = asyncHandlers;
        Namespaces = namespaces;
    }
}