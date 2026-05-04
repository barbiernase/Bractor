// ═══════════════════════════════════════════════════════════════════
// ViewModelGenerator
//
// Erkennt: Klassen mit IViewModel-Marker
// Scannt: private Methoden die mit '_' beginnen
// Prüft: Rückgabetyp implementiert ICommand, IQuery, IClientEvent
//         (oder nullable davon)
//
// Erzeugt pro ViewModel:
//   - private Action<object> _publish
//   - public void __InitBus(Action<object> publish) (E3)
//   - public void PascalCase(...) — null-Check bei nullable
//   - public IRelayCommand[<T>] PascalCaseCommand — bei 0–1 Params
//
// HINWEIS: Kein field ??= — explizite Backing Fields (C#12 Kompatibilität)
// ═══════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Client.SourceGeneration;

[Generator]
public class ViewModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial") &&
                                            c.BaseList != null,
                transform: static (ctx, _) => AnalyzeClass(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(classProvider,
            static (spc, model) => Execute(spc, model!));
    }

    private static ViewModelModel? AnalyzeClass(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (classSymbol == null) return null;

        // Muss IViewModel implementieren
        var viewModelInterface = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Client.Infrastructure.Abstractions.IViewModel");
        if (viewModelInterface == null) return null;

        var implementsViewModel = classSymbol.AllInterfaces
            .Any(i => SymbolEqualityComparer.Default.Equals(i, viewModelInterface));
        if (!implementsViewModel) return null;

        // Bus-relevante Interfaces finden
        var iCommand = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.ICommand");
        var iQuery = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IQuery");
        var iClientEvent = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Client.Infrastructure.Abstractions.IClientEvent");

        if (iCommand == null && iQuery == null && iClientEvent == null)
            return null;

        var busInterfaces = new List<INamedTypeSymbol>();
        if (iCommand != null) busInterfaces.Add(iCommand);
        if (iQuery != null) busInterfaces.Add(iQuery);
        if (iClientEvent != null) busInterfaces.Add(iClientEvent);

        // Private Methoden scannen die mit '_' beginnen
        var methods = new List<ViewModelMethodInfo>();
        var namespaces = new HashSet<string>();

        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Private) continue;
            if (!member.Name.StartsWith("_")) continue;
            if (member.Name.StartsWith("__")) continue; // __InitBus etc. überspringen

            var returnType = member.ReturnType;
            var isNullable = false;

            // Nullable<T> oder T? unwrappen
            if (returnType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
            {
                returnType = nullableType.TypeArguments[0];
                isNullable = true;
            }
            else if (returnType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                // Reference type nullable (T?)
                returnType = returnType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                isNullable = true;
            }

            // Prüfen ob Rückgabetyp eines der Bus-Interfaces implementiert
            var isBusRelevant = busInterfaces.Any(bi =>
                returnType.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, bi)) ||
                SymbolEqualityComparer.Default.Equals(returnType, bi));

            if (!isBusRelevant) continue;

            // Name transformieren: _camelCase → PascalCase
            var privateName = member.Name; // z.B. "_todoErledigen"
            var publicName = ToPascalCase(privateName); // z.B. "TodoErledigen"

            // Parameter sammeln
            var parameters = member.Parameters.Select(p => new ViewModelParamInfo(
                p.Type.ToDisplayString(),
                p.Name)).ToList();

            foreach (var p in member.Parameters)
                CollectNamespace(p.Type, namespaces);
            CollectNamespace(member.ReturnType, namespaces);

            methods.Add(new ViewModelMethodInfo(
                privateName, publicName, parameters, isNullable));
        }

        if (methods.Count == 0) return null;

        return new ViewModelModel(
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            methods,
            namespaces.ToList());
    }

    private static void Execute(SourceProductionContext context, ViewModelModel model)
    {
        var source = GenerateViewModel(model);
        context.AddSource($"{model.ClassName}.ViewModel.g.cs", source);
    }

    private static string GenerateViewModel(ViewModelModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generator: ViewModelGenerator");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using CommunityToolkit.Mvvm.Input;");

        foreach (var ns in model.Namespaces.OrderBy(n => n))
        {
            if (ns != model.ClassNamespace)
                sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {model.ClassNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ClassName}");
        sb.AppendLine("{");

        // _publish Feld + __InitBus (E3)
        sb.AppendLine("    private Action<object> _publish = null!;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Wird vom Framework aufgerufen. Nicht manuell aufrufen.</summary>");
        sb.AppendLine("    public void __InitBus(Action<object> publish) => _publish = publish;");
        sb.AppendLine();

        // Öffentliche Methoden
        foreach (var m in model.Methods)
        {
            var paramList = string.Join(", ",
                m.Parameters.Select(p => $"{p.TypeName} {p.Name}"));
            var argList = string.Join(", ",
                m.Parameters.Select(p => p.Name));

            if (m.IsNullable)
            {
                sb.AppendLine($"    public void {m.PublicName}({paramList})");
                sb.AppendLine("    {");
                sb.AppendLine($"        var r = {m.PrivateName}({argList});");
                sb.AppendLine("        if (r != null) _publish(r);");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    public void {m.PublicName}({paramList})");
                sb.AppendLine($"        => _publish({m.PrivateName}({argList}));");
            }

            sb.AppendLine();
        }

        // IRelayCommand Properties (nur bei 0–1 Parametern)
        // Explizite Backing Fields (kein field ??=)
        var commandMethods = model.Methods
            .Where(m => m.Parameters.Count <= 1)
            .ToList();

        if (commandMethods.Count > 0)
        {
            // Backing Fields
            foreach (var m in commandMethods)
            {
                var fieldName = $"_{char.ToLower(m.PublicName[0])}{m.PublicName.Substring(1)}Command";
                if (m.Parameters.Count == 0)
                    sb.AppendLine($"    private IRelayCommand? {fieldName};");
                else
                    sb.AppendLine($"    private IRelayCommand<{m.Parameters[0].TypeName}>? {fieldName};");
            }

            sb.AppendLine();

            // Properties
            foreach (var m in commandMethods)
            {
                var fieldName = $"_{char.ToLower(m.PublicName[0])}{m.PublicName.Substring(1)}Command";
                if (m.Parameters.Count == 0)
                {
                    sb.AppendLine($"    public IRelayCommand {m.PublicName}Command");
                    sb.AppendLine($"        => {fieldName} ??= new RelayCommand({m.PublicName});");
                }
                else
                {
                    sb.AppendLine($"    public IRelayCommand<{m.Parameters[0].TypeName}> {m.PublicName}Command");
                    sb.AppendLine($"        => {fieldName} ??= new RelayCommand<{m.Parameters[0].TypeName}>({m.PublicName});");
                }
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private static string ToPascalCase(string name)
    {
        // "_todoErledigen" → "TodoErledigen"
        if (name.StartsWith("_"))
            name = name.Substring(1);
        if (name.Length == 0) return name;
        return char.ToUpper(name[0]) + name.Substring(1);
    }

    private static void CollectNamespace(ITypeSymbol type, HashSet<string> namespaces)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
            namespaces.Add(ns!);
    }
}

// ═══════════════════════════════════════════════════
// Modelle
// ═══════════════════════════════════════════════════

internal class ViewModelParamInfo
{
    public string TypeName { get; }
    public string Name { get; }

    public ViewModelParamInfo(string typeName, string name)
    {
        TypeName = typeName;
        Name = name;
    }
}

internal class ViewModelMethodInfo
{
    public string PrivateName { get; }
    public string PublicName { get; }
    public List<ViewModelParamInfo> Parameters { get; }
    public bool IsNullable { get; }

    public ViewModelMethodInfo(string privateName, string publicName,
        List<ViewModelParamInfo> parameters, bool isNullable)
    {
        PrivateName = privateName;
        PublicName = publicName;
        Parameters = parameters;
        IsNullable = isNullable;
    }
}

internal class ViewModelModel
{
    public string ClassNamespace { get; }
    public string ClassName { get; }
    public List<ViewModelMethodInfo> Methods { get; }
    public List<string> Namespaces { get; }

    public ViewModelModel(string classNamespace, string className,
        List<ViewModelMethodInfo> methods, List<string> namespaces)
    {
        ClassNamespace = classNamespace;
        ClassName = className;
        Methods = methods;
        Namespaces = namespaces;
    }
}