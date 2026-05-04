using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Domain.SourceGeneration;

/// <summary>
/// Generiert den zentralen ProjectionQueryService.
///
/// Konstruktor nimmt Reader (via DI) und Projektionen (für SubscriberId).
/// Reader werden NICHT intern erstellt — DI liefert fertige Instanzen mit ihrem ReadStore.
/// Projektionen werden nur für SubscriberId gebraucht (Deps-Routing).
///
/// Handle-Signatur: (IQuery, IMessageEnvelope, ReadContext)
/// ExecuteAsync erstellt QueryEnvelope für Transport-Metadaten.
/// </summary>
[Generator]
public class ProjectionQueryServiceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projectionsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial") &&
                                            c.BaseList != null,
                transform: static (ctx, _) => GetProjectionInfo(ctx))
            .Where(static m => m is not null);

        var collectedProjections = projectionsProvider.Collect();

        context.RegisterSourceOutput(collectedProjections,
            static (spc, projections) => Execute(spc, projections!));
    }

    private static QueryServiceProjectionInfo? GetProjectionInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

        if (classSymbol == null)
            return null;

        var readerInterfaceDef = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IReader`1");

        if (readerInterfaceDef == null)
            return null;

        var readerInterface = classSymbol.AllInterfaces
            .FirstOrDefault(i => i.OriginalDefinition.Equals(readerInterfaceDef, SymbolEqualityComparer.Default));

        if (readerInterface == null)
            return null;

        var projectionType = readerInterface.TypeArguments[0] as INamedTypeSymbol;
        if (projectionType == null)
            return null;

        var messageEnvelopeType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.IMessageEnvelope");
        var readContextType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Abstractions.ReadContext");

        if (messageEnvelopeType == null || readContextType == null)
            return null;

        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 3 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, messageEnvelopeType) &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[2].Type, readContextType))
            .ToList();

        if (handleMethods.Count == 0)
            return null;

        var queryTypes = handleMethods
            .Select(m => m.Parameters[0].Type.ToDisplayString())
            .Distinct()
            .ToList();

        bool trackDeps = DetectTrackDeps(classSymbol, context.SemanticModel.Compilation);

        return new QueryServiceProjectionInfo(
            projectionFullName: projectionType.ToDisplayString(),
            projectionName: projectionType.Name,
            projectionNamespace: projectionType.ContainingNamespace.ToDisplayString(),
            readerFullName: classSymbol.ToDisplayString(),
            readerName: classSymbol.Name,
            readerNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
            queryTypes: queryTypes,
            trackDeps: trackDeps
        );
    }

    private static bool DetectTrackDeps(INamedTypeSymbol readerSymbol, Compilation compilation)
    {
        var attrType = compilation
            .GetTypeByMetadataName("Abstractions.ProjectionReaderAttribute");

        if (attrType == null)
            return false;

        var attr = readerSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrType));

        if (attr == null)
            return false;

        var trackDepsArg = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "TrackDeps");

        if (trackDepsArg.Key != null)
            return (bool)trackDepsArg.Value.Value!;

        return true;
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<QueryServiceProjectionInfo?> projections)
    {
        var validProjections = projections
            .Where(p => p != null)
            .Cast<QueryServiceProjectionInfo>()
            .GroupBy(p => p.ReaderFullName)
            .Select(g => g.First())
            .OrderBy(p => p.ReaderName)
            .ToList();

        if (validProjections.Count == 0)
            return;

        var source = GenerateQueryService(validProjections);
        context.AddSource("ProjectionQueryService.g.cs", source);
    }

    private static string GenerateQueryService(List<QueryServiceProjectionInfo> projections)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Abstractions;");

        var namespaces = projections
            .SelectMany(p => new[] { p.ProjectionNamespace, p.ReaderNamespace })
            .Distinct()
            .OrderBy(n => n);
        foreach (var ns in namespaces)
        {
            if (ns != "Abstractions")
                sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Domain.Projections;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Zentraler Service für Query-Ausführung.");
        sb.AppendLine("/// Reader kommen via DI (mit ihrem ReadStore), Projektionen liefern SubscriberId.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class ProjectionQueryService");
        sb.AppendLine("{");

        // ── Felder ──
        foreach (var proj in projections)
        {
            var readerField = $"_{ToCamelCase(proj.ReaderName)}";
            sb.AppendLine($"    private readonly {proj.ReaderName} {readerField};");
        }
        sb.AppendLine($"    private readonly IReadModelDepsReader? _depsReader;");
        sb.AppendLine($"    private readonly Dictionary<Type, QueryHandlerEntry> _handlers;");
        sb.AppendLine();

        // ── Konstruktor ──
        // Reader via DI (fertige Instanzen mit ReadStore)
        // Projektionen nur für SubscriberId (Deps-Routing)
        sb.AppendLine("    public ProjectionQueryService(");
        foreach (var proj in projections)
        {
            var readerParam = ToCamelCase(proj.ReaderName);
            sb.AppendLine($"        {proj.ReaderName} {readerParam},");
        }
        foreach (var proj in projections)
        {
            var projParam = ToCamelCase(proj.ProjectionName);
            sb.AppendLine($"        {proj.ProjectionName} {projParam},");
        }
        sb.AppendLine("        IReadModelDepsReader? depsReader = null)");
        sb.AppendLine("    {");

        foreach (var proj in projections)
        {
            var readerField = $"_{ToCamelCase(proj.ReaderName)}";
            var readerParam = ToCamelCase(proj.ReaderName);
            sb.AppendLine($"        {readerField} = {readerParam};");
        }
        sb.AppendLine("        _depsReader = depsReader;");
        sb.AppendLine();

        // Handler-Dictionary: Reader dispatcht, Projektion liefert SubscriberId
        sb.AppendLine("        _handlers = new Dictionary<Type, QueryHandlerEntry>");
        sb.AppendLine("        {");

        foreach (var proj in projections)
        {
            var readerField = $"_{ToCamelCase(proj.ReaderName)}";
            var projParam = ToCamelCase(proj.ProjectionName);
            var trackDeps = proj.TrackDeps ? "true" : "false";

            foreach (var queryType in proj.QueryTypes)
            {
                var simpleType = GetSimpleTypeName(queryType);
                sb.AppendLine($"            [typeof({simpleType})] = new((q, env, ctx) => {readerField}.DispatchAsync(q, env, ctx), {projParam}.SubscriberId, {trackDeps}),");
            }
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── ExecuteAsync ──
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Führt eine Query aus. Erstellt QueryEnvelope für Transport-Metadaten.");
        sb.AppendLine("    /// Deps sind null wenn TrackDeps=false oder kein ctx.Track() aufgerufen wurde.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async Task<QueryResponse<IQueryResponse>> ExecuteAsync(IQuery query)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(query);");
        sb.AppendLine();
        sb.AppendLine("        if (!_handlers.TryGetValue(query.GetType(), out var entry))");
        sb.AppendLine("            throw new NotSupportedException($\"No handler registered for query type {query.GetType().Name}\");");
        sb.AppendLine();
        sb.AppendLine("        var ctx = new ReadContext();");
        sb.AppendLine("        var envelope = new QueryEnvelope();");
        sb.AppendLine("        var data = await entry.Handler(query, envelope, ctx);");
        sb.AppendLine();
        sb.AppendLine("        IReadOnlyList<AggregateMeta>? deps = null;");
        sb.AppendLine("        if (entry.TrackDeps && ctx.HasTrackedIds && _depsReader != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            deps = await _depsReader.ReadAsync(entry.SubscriberId, ctx.TrackedIds);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return new QueryResponse<IQueryResponse> { Data = data, Deps = deps };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── SupportedQueryTypes ──
        sb.AppendLine("    public static IReadOnlyList<Type> SupportedQueryTypes { get; } = new[]");
        sb.AppendLine("    {");
        foreach (var proj in projections)
        {
            foreach (var queryType in proj.QueryTypes)
            {
                var simpleType = GetSimpleTypeName(queryType);
                sb.AppendLine($"        typeof({simpleType}),");
            }
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        // ── QueryHandlerEntry ──
        sb.AppendLine("    private record QueryHandlerEntry(");
        sb.AppendLine("        Func<IQuery, IMessageEnvelope, ReadContext, Task<IQueryResponse>> Handler,");
        sb.AppendLine("        string SubscriberId,");
        sb.AppendLine("        bool TrackDeps);");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetSimpleTypeName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}

// ═══════════════════════════════════════════════════════════
// Modell
// ═══════════════════════════════════════════════════════════

internal class QueryServiceProjectionInfo
{
    public string ProjectionFullName { get; }
    public string ProjectionName { get; }
    public string ProjectionNamespace { get; }
    public string ReaderFullName { get; }
    public string ReaderName { get; }
    public string ReaderNamespace { get; }
    public List<string> QueryTypes { get; }
    public bool TrackDeps { get; }

    public QueryServiceProjectionInfo(
        string projectionFullName,
        string projectionName,
        string projectionNamespace,
        string readerFullName,
        string readerName,
        string readerNamespace,
        List<string> queryTypes,
        bool trackDeps)
    {
        ProjectionFullName = projectionFullName;
        ProjectionName = projectionName;
        ProjectionNamespace = projectionNamespace;
        ReaderFullName = readerFullName;
        ReaderName = readerName;
        ReaderNamespace = readerNamespace;
        QueryTypes = queryTypes;
        TrackDeps = trackDeps;
    }
}