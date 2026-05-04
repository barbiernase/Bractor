// ═══════════════════════════════════════════════════════════════════
// WiringGenerator
//
// Sammelt alle relevanten Typen und erzeugt EINE statische Wiring-Klasse:
//
//   - AddClientDomain(IServiceCollection) → DI-Registrierung
//   - SubscribeAll(IServiceProvider, IBus) → Bus-Subscriptions
//   - ServerEventTypes → IEvent (ohne IClientEvent)
//   - CommandTypes → ICommand
//   - CommandAggregateTypes → Command-Typ → AggregateType-Name
//   - RegisterQueries(QueryBridge, ClientBus)
//       → konkrete generische Aufrufe, KEIN Reflection
//
// Kritische Subscription-Reihenfolge:
//   1. Stores (sync)       — void Handle → Mutation
//   2. sync Handler (sync) — IEnumerable Handle → Produktion
//   3. async Handler       — IAsyncEnumerable Handle → Background
// ═══════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Client.SourceGeneration;

[Generator]
public class WiringGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Stores + Handler (Handle-Methoden)
        var handleClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial"),
                transform: static (ctx, _) => AnalyzeHandleClass(ctx))
            .Where(static m => m is not null);

        // 2. ViewModels (IViewModel)
        var viewModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c &&
                                            c.Modifiers.Any(m => m.Text == "partial") &&
                                            c.BaseList != null,
                transform: static (ctx, _) => AnalyzeViewModel(ctx))
            .Where(static m => m is not null);

        // 3. Domain-Typen (Commands, Events, Queries, States)
        //    WICHTIG: CompilationProvider statt SyntaxProvider!
        //    SyntaxProvider sieht nur Syntax-Nodes im eigenen Projekt.
        //    Commands/Events/States leben aber in referenzierten Assemblies (Domain-Projekt).
        //    CompilationProvider hat Zugriff auf ALLE Typen via Semantic Model.
        var domainTypes = context.CompilationProvider
            .Select(static (compilation, _) => CollectDomainTypes(compilation));

        // Alles einsammeln und kombinieren
        var combined = handleClasses.Collect()
            .Combine(viewModels.Collect())
            .Combine(domainTypes);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var handles = source.Left.Left.Where(x => x != null).Select(x => x!).ToList();
            var vms = source.Left.Right.Where(x => x != null).Select(x => x!).ToList();
            var types = source.Right.ToList();
            Execute(spc, handles, vms, types);
        });
    }

    // ═══════════════════════════════════════════════════
    // Analyse: Handle-Klassen (Stores + Handler)
    // ═══════════════════════════════════════════════════

    private static WiringHandleClassInfo? AnalyzeHandleClass(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (classSymbol == null) return null;

        var messageContextType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Client.Infrastructure.Abstractions.MessageContext");
        if (messageContextType == null) return null;

        var handleMethods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 2 &&
                        SymbolEqualityComparer.Default.Equals(
                            m.Parameters[1].Type, messageContextType))
            .ToList();

        if (handleMethods.Count == 0) return null;

        var subscribedTypes = new List<string>();
        var isStore = true;
        var hasSyncHandlers = false;
        var hasAsyncHandlers = false;

        foreach (var method in handleMethods)
        {
            var inputFqn = method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            subscribedTypes.Add(inputFqn);

            if (method.ReturnType.SpecialType != SpecialType.System_Void)
            {
                isStore = false;
                if (method.ReturnType is INamedTypeSymbol { IsGenericType: true } generic)
                {
                    if (generic.OriginalDefinition.ToDisplayString()
                        .StartsWith("System.Collections.Generic.IAsyncEnumerable"))
                        hasAsyncHandlers = true;
                    else
                        hasSyncHandlers = true;
                }
            }
        }

        var fqn = classSymbol.ToDisplayString();
        var name = classSymbol.Name;
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var varName = char.ToLower(name[0]) + name.Substring(1);

        return new WiringHandleClassInfo(
            fqn, name, ns, varName, isStore, hasSyncHandlers, hasAsyncHandlers,
            subscribedTypes.Distinct().ToList());
    }

    // ═══════════════════════════════════════════════════
    // Analyse: ViewModels
    // ═══════════════════════════════════════════════════

    private static WiringViewModelInfo? AnalyzeViewModel(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (classSymbol == null) return null;

        var viewModelInterface = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Client.Infrastructure.Abstractions.IViewModel");
        if (viewModelInterface == null) return null;

        if (!classSymbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, viewModelInterface)))
            return null;

        return new WiringViewModelInfo(
            classSymbol.ToDisplayString(), classSymbol.Name,
            classSymbol.ContainingNamespace.ToDisplayString());
    }

    // ═══════════════════════════════════════════════════
    // Analyse: Domain-Typen via Compilation (NICHT SyntaxProvider!)
    //
    // SyntaxProvider sieht nur Dateien im eigenen Projekt.
    // Commands, Events, States, Queries leben im Domain-Projekt
    // (referenzierte Assembly). CompilationProvider sieht alles.
    // ═══════════════════════════════════════════════════

    private static ImmutableArray<WiringDomainTypeInfo> CollectDomainTypes(Compilation compilation)
    {
        var iCommand = compilation.GetTypeByMetadataName("Abstractions.ICommand");
        var iEvent = compilation.GetTypeByMetadataName("Abstractions.IEvent");
        var iClientEvent = compilation.GetTypeByMetadataName("Client.Infrastructure.Abstractions.IClientEvent");
        var iQuery = compilation.GetTypeByMetadataName("Abstractions.IQuery");
        var iState = compilation.GetTypeByMetadataName("Abstractions.IState");

        var results = ImmutableArray.CreateBuilder<WiringDomainTypeInfo>();

        // Eigene Compilation durchsuchen (Client.Domain-Projekt)
        WalkNamespace(compilation.Assembly.GlobalNamespace,
            iCommand, iEvent, iClientEvent, iQuery, iState, results);

        // Referenzierte Assemblies durchsuchen (Domain-Projekt!)
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
            {
                WalkNamespace(assembly.GlobalNamespace,
                    iCommand, iEvent, iClientEvent, iQuery, iState, results);
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Rekursiv durch Namespaces laufen und Domain-Typen sammeln.
    /// Überspringt bekannte Framework-Namespaces für Performance.
    /// </summary>
    private static void WalkNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol? iCommand, INamedTypeSymbol? iEvent,
        INamedTypeSymbol? iClientEvent, INamedTypeSymbol? iQuery, INamedTypeSymbol? iState,
        ImmutableArray<WiringDomainTypeInfo>.Builder results)
    {
        // Framework-Namespaces überspringen (Performance)
        var name = ns.Name;
        if (name is "System" or "Microsoft" or "Google" or "Grpc" or "Proto" or
            "ProtoRepo" or "Marten" or "Npgsql" or "StackExchange" or "Serilog" or
            "JasperFx" or "Weasel" or "Newtonsoft" or "NodaTime" or "Polly" or
            "Proto.Cluster" or "Internal" or "Runtime" or "Reflection" or
            "CodeAnalysis" or "Diagnostics" or "Collections" or "Linq" or
            "Threading" or "Globalization" or "Resources" or "Buffers")
            return;

        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                WalkNamespace(childNs, iCommand, iEvent, iClientEvent, iQuery, iState, results);
            }
            else if (member is INamedTypeSymbol type &&
                     !type.IsAbstract &&
                     type.DeclaredAccessibility == Accessibility.Public &&
                     type.TypeKind is TypeKind.Class or TypeKind.Struct)
            {
                var info = ClassifyDomainType(type, iCommand, iEvent, iClientEvent, iQuery, iState);
                if (info != null)
                    results.Add(info);
            }
        }
    }

    /// <summary>
    /// Prüft ob ein Typ ein Command, Event, Query oder State ist.
    /// Gleiche Logik wie vorher AnalyzeDomainType, aber ohne SyntaxProvider-Abhängigkeit.
    /// </summary>
    private static WiringDomainTypeInfo? ClassifyDomainType(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? iCommand, INamedTypeSymbol? iEvent,
        INamedTypeSymbol? iClientEvent, INamedTypeSymbol? iQuery, INamedTypeSymbol? iState)
    {
        var fqn = symbol.ToDisplayString();

        // ICommand?
        if (iCommand != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iCommand)))
        {
            return new WiringDomainTypeInfo(fqn, DomainTypeKind.Command);
        }

        // IQuery? (non-generic Abstractions.IQuery)
        // ResponseType wird nicht gebraucht — QueryBridge nutzt IQueryResponse als Bound.
        if (iQuery != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iQuery)))
        {
            return new WiringDomainTypeInfo(fqn, DomainTypeKind.Query);
        }

        // IEvent aber NICHT IClientEvent → Server-Event
        if (iEvent != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iEvent)))
        {
            var isClientEvent = iClientEvent != null && symbol.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i, iClientEvent));
            if (!isClientEvent)
                return new WiringDomainTypeInfo(fqn, DomainTypeKind.ServerEvent);
        }

        // IState? → für CommandAggregateTypes (Namespace-Matching)
        if (iState != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iState)))
        {
            return new WiringDomainTypeInfo(fqn, DomainTypeKind.State);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════
    // Code-Generierung
    // ═══════════════════════════════════════════════════

    private static void Execute(
        SourceProductionContext context,
        List<WiringHandleClassInfo> handleClasses,
        List<WiringViewModelInfo> viewModels,
        List<WiringDomainTypeInfo> domainTypes)
    {
        if (handleClasses.Count == 0 && viewModels.Count == 0 && domainTypes.Count == 0) return;

        // Deduplizieren
        handleClasses = handleClasses.GroupBy(h => h.Fqn).Select(g => g.First()).ToList();
        viewModels = viewModels.GroupBy(v => v.Fqn).Select(g => g.First()).ToList();
        domainTypes = domainTypes.GroupBy(d => d.Fqn).Select(g => g.First()).ToList();

        var stores = handleClasses.Where(h => h.IsStore).ToList();
        var syncHandlers = handleClasses.Where(h => !h.IsStore && h.HasSyncHandlers).ToList();
        var asyncHandlers = handleClasses.Where(h => !h.IsStore && h.HasAsyncHandlers).ToList();

        var commands = domainTypes.Where(d => d.Kind == DomainTypeKind.Command).ToList();
        var serverEvents = domainTypes.Where(d => d.Kind == DomainTypeKind.ServerEvent).ToList();
        var queries = domainTypes.Where(d => d.Kind == DomainTypeKind.Query).ToList();
        var states = domainTypes.Where(d => d.Kind == DomainTypeKind.State).ToList();

        // Command → AggregateType Mapping bauen
        var commandAggregateMap = BuildCommandAggregateTypeMap(commands, states);

        var wiringNamespace = handleClasses.Select(h => h.Namespace)
            .Concat(viewModels.Select(v => v.Namespace))
            .GroupBy(n => n)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "Domain.Client";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generator: WiringGenerator");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using global::Client.Infrastructure.Abstractions;");
        sb.AppendLine("using global::Client.Infrastructure.Bus;");
        sb.AppendLine("using global::Client.Infrastructure.Connection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        var allUsings = handleClasses.Select(h => h.Namespace)
            .Concat(viewModels.Select(v => v.Namespace))
            .Where(n => n != wiringNamespace)
            .Distinct()
            .OrderBy(n => n);

        foreach (var ns in allUsings)
            sb.AppendLine($"using global::{ns};");

        sb.AppendLine();
        sb.AppendLine($"namespace {wiringNamespace};");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedWiring");
        sb.AppendLine("{");

        GenerateAddClient(sb, stores, syncHandlers, asyncHandlers, viewModels);
        GenerateSubscribeAll(sb, stores, syncHandlers, asyncHandlers);
        GenerateTypeLists(sb, commands, serverEvents);
        GenerateCommandAggregateTypes(sb, commandAggregateMap);
        GenerateRegisterQueries(sb, queries);

        sb.AppendLine("}");

        context.AddSource("GeneratedWiring.g.cs", sb.ToString());
    }

    // ═══════════════════════════════════════════════════
    // CommandAggregateType Mapping (Namespace-Convention)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Baut das Mapping Command-FQN → AggregateType-Name.
    /// Convention: Commands im selben Namespace wie ein IState gehören zu diesem Aggregate.
    ///
    /// Beispiel:
    ///   Domain.Todo.ErstelleTodo     → Namespace "Domain.Todo" → IState "TodoItem" → "TodoItem"
    ///   Domain.Lagerartikel.BucheWareneingang → "Domain.Lagerartikel" → "Lagerartikel"
    /// </summary>
    private static Dictionary<string, string> BuildCommandAggregateTypeMap(
        List<WiringDomainTypeInfo> commands,
        List<WiringDomainTypeInfo> states)
    {
        // 1. State-Namespace → State-Name (einfacher Typname, nicht FQN)
        //    "Domain.Todo.TodoItem" → Namespace="Domain.Todo", Name="TodoItem"
        var stateByNamespace = new Dictionary<string, string>();
        foreach (var state in states)
        {
            var lastDot = state.Fqn.LastIndexOf('.');
            if (lastDot < 0) continue;
            var ns = state.Fqn.Substring(0, lastDot);
            var name = state.Fqn.Substring(lastDot + 1);
            stateByNamespace[ns] = name;
        }

        // 2. Pro Command: Namespace matchen → AggregateType
        //    "Domain.Todo.ErstelleTodo" → Namespace="Domain.Todo" → "TodoItem"
        var map = new Dictionary<string, string>();
        foreach (var cmd in commands)
        {
            var lastDot = cmd.Fqn.LastIndexOf('.');
            if (lastDot < 0) continue;
            var ns = cmd.Fqn.Substring(0, lastDot);
            if (stateByNamespace.TryGetValue(ns, out var aggregateType))
                map[cmd.Fqn] = aggregateType;
        }
        return map;
    }

    // ─── AddClientDomain ───

    private static void GenerateAddClient(
        StringBuilder sb,
        List<WiringHandleClassInfo> stores,
        List<WiringHandleClassInfo> syncHandlers,
        List<WiringHandleClassInfo> asyncHandlers,
        List<WiringViewModelInfo> viewModels)
    {
        sb.AppendLine("    public static IServiceCollection AddClientDomain(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var store in stores)
            sb.AppendLine($"        services.AddSingleton<{store.Fqn}>();");
        foreach (var handler in syncHandlers.Concat(asyncHandlers))
            sb.AppendLine($"        services.AddSingleton<{handler.Fqn}>();");

        sb.AppendLine();

        foreach (var vm in viewModels)
        {
            sb.AppendLine($"        services.AddTransient<{vm.Fqn}>(sp =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var vm = ActivatorUtilities.CreateInstance<{vm.Fqn}>(sp);");
            sb.AppendLine("            var bus = sp.GetRequiredService<IBus>();");
            sb.AppendLine("            vm.__InitBus(msg => bus.Publish(msg, MessageContext.Local()));");
            sb.AppendLine("            return vm;");
            sb.AppendLine("        });");
        }

        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ─── SubscribeAll ───

    private static void GenerateSubscribeAll(
        StringBuilder sb,
        List<WiringHandleClassInfo> stores,
        List<WiringHandleClassInfo> syncHandlers,
        List<WiringHandleClassInfo> asyncHandlers)
    {
        sb.AppendLine("    public static void SubscribeAll(IServiceProvider sp, IBus bus)");
        sb.AppendLine("    {");

        foreach (var store in stores)
            sb.AppendLine($"        var {store.VarName} = sp.GetRequiredService<{store.Fqn}>();");
        foreach (var handler in syncHandlers.Concat(asyncHandlers))
            sb.AppendLine($"        var {handler.VarName} = sp.GetRequiredService<{handler.Fqn}>();");

        sb.AppendLine();

        if (stores.Count > 0)
        {
            sb.AppendLine("        // 1. STORES — sync Subscribe, void Handle → Mutation");
            foreach (var store in stores)
                foreach (var eventType in store.SubscribedTypes)
                    sb.AppendLine($"        bus.Subscribe<{eventType}>((evt, ctx) => {store.VarName}.Dispatch(evt, ctx));");
            sb.AppendLine();
        }

        if (syncHandlers.Count > 0)
        {
            sb.AppendLine("        // 2. HANDLER (sync) — IEnumerable Handle → Produktion");
            foreach (var handler in syncHandlers)
                foreach (var eventType in handler.SubscribedTypes)
                {
                    sb.AppendLine($"        bus.Subscribe<{eventType}>((evt, ctx) =>");
                    sb.AppendLine($"            {handler.VarName}.Dispatch(evt, ctx, msg => bus.Publish(msg, MessageContext.Local())));");
                }
            sb.AppendLine();
        }

        if (asyncHandlers.Count > 0)
        {
            sb.AppendLine("        // 3. HANDLER (async) — IAsyncEnumerable Handle → Background");
            foreach (var handler in asyncHandlers)
                foreach (var eventType in handler.SubscribedTypes)
                {
                    sb.AppendLine($"        bus.SubscribeAsync<{eventType}>(async (evt, ctx) =>");
                    sb.AppendLine($"            await {handler.VarName}.DispatchAsync(evt, ctx, async msg =>");
                    sb.AppendLine("            {");
                    sb.AppendLine("                if (bus is ClientBus clientBus)");
                    sb.AppendLine("                    clientBus.PostToSyncContext(() => bus.Publish(msg, MessageContext.Local()));");
                    sb.AppendLine("                else");
                    sb.AppendLine("                    bus.Publish(msg, MessageContext.Local());");
                    sb.AppendLine("            }));");
                }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ─── ServerEventTypes + CommandTypes ───

    private static void GenerateTypeLists(
        StringBuilder sb,
        List<WiringDomainTypeInfo> commands,
        List<WiringDomainTypeInfo> serverEvents)
    {
        sb.AppendLine("    /// <summary>Server-Event-Typen für Capabilities-Handshake + VersioningModule.</summary>");
        if (serverEvents.Count == 0)
        {
            sb.AppendLine("    public static IReadOnlyList<Type> ServerEventTypes { get; } = Array.Empty<Type>();");
        }
        else
        {
            sb.AppendLine("    public static IReadOnlyList<Type> ServerEventTypes { get; } = new Type[]");
            sb.AppendLine("    {");
            foreach (var evt in serverEvents)
                sb.AppendLine($"        typeof({evt.Fqn}),");
            sb.AppendLine("    };");
        }
        sb.AppendLine();

        sb.AppendLine("    /// <summary>Command-Typen für ConnectionModule.</summary>");
        if (commands.Count == 0)
        {
            sb.AppendLine("    public static IReadOnlyList<Type> CommandTypes { get; } = Array.Empty<Type>();");
        }
        else
        {
            sb.AppendLine("    public static IReadOnlyList<Type> CommandTypes { get; } = new Type[]");
            sb.AppendLine("    {");
            foreach (var cmd in commands)
                sb.AppendLine($"        typeof({cmd.Fqn}),");
            sb.AppendLine("    };");
        }
        sb.AppendLine();
    }

    // ─── CommandAggregateTypes ───

    private static void GenerateCommandAggregateTypes(
        StringBuilder sb,
        Dictionary<string, string> commandAggregateMap)
    {
        sb.AppendLine("    /// <summary>Command-Typ → AggregateType-Name für ConnectionModule.</summary>");
        sb.AppendLine("    public static IReadOnlyDictionary<Type, string> CommandAggregateTypes { get; } = new Dictionary<Type, string>");
        sb.AppendLine("    {");
        foreach (var kvp in commandAggregateMap.OrderBy(k => k.Key))
            sb.AppendLine($"        [typeof({kvp.Key})] = \"{kvp.Value}\",");
        sb.AppendLine("    };");
        sb.AppendLine();
    }

    // ─── RegisterQueries (kein Reflection!) ───

    private static void GenerateRegisterQueries(
        StringBuilder sb,
        List<WiringDomainTypeInfo> queries)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registriert Query→Response-Mappings auf der QueryBridge.");
        sb.AppendLine("    /// TResponse = IQueryResponse — der konkrete Typ wird vom ProtoMapper");
        sb.AppendLine("    /// zur Runtime aufgelöst, nicht vom Typ-Parameter.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void RegisterQueries(QueryBridge queryBridge, ClientBus bus)");
        sb.AppendLine("    {");
        foreach (var query in queries.OrderBy(q => q.Fqn))
        {
            sb.AppendLine($"        queryBridge.Register<{query.Fqn}, Abstractions.IQueryResponse>(bus);");
        }
        sb.AppendLine("    }");
    }
}

// ═══════════════════════════════════════════════════
// Modelle
// ═══════════════════════════════════════════════════

internal enum DomainTypeKind { Command, ServerEvent, Query, State }

internal class WiringDomainTypeInfo
{
    public string Fqn { get; }
    public DomainTypeKind Kind { get; }

    public WiringDomainTypeInfo(string fqn, DomainTypeKind kind)
    {
        Fqn = fqn;
        Kind = kind;
    }
}

internal class WiringHandleClassInfo
{
    public string Fqn { get; }
    public string Name { get; }
    public string Namespace { get; }
    public string VarName { get; }
    public bool IsStore { get; }
    public bool HasSyncHandlers { get; }
    public bool HasAsyncHandlers { get; }
    public List<string> SubscribedTypes { get; }

    public WiringHandleClassInfo(
        string fqn, string name, string ns, string varName,
        bool isStore, bool hasSyncHandlers, bool hasAsyncHandlers,
        List<string> subscribedTypes)
    {
        Fqn = fqn;
        Name = name;
        Namespace = ns;
        VarName = varName;
        IsStore = isStore;
        HasSyncHandlers = hasSyncHandlers;
        HasAsyncHandlers = hasAsyncHandlers;
        SubscribedTypes = subscribedTypes;
    }
}

internal class WiringViewModelInfo
{
    public string Fqn { get; }
    public string Name { get; }
    public string Namespace { get; }

    public WiringViewModelInfo(string fqn, string name, string ns)
    {
        Fqn = fqn;
        Name = name;
        Namespace = ns;
    }
}