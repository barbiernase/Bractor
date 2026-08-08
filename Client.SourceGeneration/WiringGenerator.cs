// ── Client.SourceGeneration/WiringGenerator.cs ──
//
// ═══════════════════════════════════════════════════════════════════
// WiringGenerator
//
// Sammelt alle relevanten Typen und erzeugt EINE statische Wiring-Klasse.
//
// Output-Pattern (identisch zu ModuleRegistryGenerator):
//   Namespace:  Microsoft.Extensions.DependencyInjection (immer gleich)
//   Klasse:     GeneratedWiring_{AssemblyName}
//   Methode:    AddClientDomain_{AssemblyName}(this IServiceCollection)
//
// Assembly-Name im Klassen- UND Methodennamen garantiert:
//   - Kein Typ-Konflikt (verschiedene Klassennamen)
//   - Kein CS0121 (verschiedene Methodennamen)
//   - Extension-Method-Syntax bleibt erhalten
//   - Kein Namespace-Import nötig
//
// Weitere generierte Members (auf derselben Klasse):
//   - SubscribeAll(IServiceProvider, IBus)
//   - UnsubscribeAll(IServiceProvider)
//   - ServerEventTypes, CommandTypes, CommandAggregateTypes
//   - RegisterQueries(QueryBridge, ClientBus)
//
// Kritische Subscription-Reihenfolge:
//   1. Store-Baum depth-first (Eltern vor Kindern)
//   2. Standalone-Stores
//   3. sync Handler
//   4. async Handler
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
        //    CompilationProvider: sieht auch referenzierte Assemblies
        var domainTypes = context.CompilationProvider
            .Select(static (compilation, _) => CollectDomainTypes(compilation));

        // 4. Assembly-Name für eindeutige Klassen-/Methodennamen
        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Unknown");

        var combined = handleClasses.Collect()
            .Combine(viewModels.Collect())
            .Combine(domainTypes)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var (source, asmName) = data;
            var (leftPair, types) = source;
            var (handleArr, vmArr) = leftPair;

            var handles = handleArr.Where(x => x != null).Select(x => x!).ToList();
            var vms = vmArr.Where(x => x != null).Select(x => x!).ToList();
            Execute(spc, handles, vms, types.ToList(), asmName);
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

        if (handleMethods.Count == 0)
        {
            var storeBaseType = context.SemanticModel.Compilation
                .GetTypeByMetadataName("Client.Infrastructure.Abstractions.StoreBase");
            if (storeBaseType == null) return null;

            var isStoreBase = false;
            var baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, storeBaseType))
                { isStoreBase = true; break; }
                baseType = baseType.BaseType;
            }
            if (!isStoreBase) return null;
        }

        var subscribedTypes = new List<string>();
        var isStore = true;
        var hasSyncHandlers = false;
        var hasAsyncHandlers = false;

        foreach (var method in handleMethods)
        {
            var inputFqn = method.Parameters[0].Type
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

        var publicPropertyTypeFqns = new List<string>();
        if (isStore)
        {
            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.IsStatic) continue;
                if (member.DeclaredAccessibility != Accessibility.Public) continue;
                publicPropertyTypeFqns.Add(member.Type.ToDisplayString());
            }
        }

        var fqn = classSymbol.ToDisplayString();
        var name = classSymbol.Name;
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var varName = char.ToLower(name[0]) + name.Substring(1);

        // IHydrationStore-Implementierung erkennen (nur relevant für Stores).
        var isHydrationStore = false;
        if (isStore)
        {
            var hydrationType = context.SemanticModel.Compilation
                .GetTypeByMetadataName("Client.Infrastructure.Abstractions.IHydrationStore");
            if (hydrationType != null)
                isHydrationStore = classSymbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, hydrationType));
        }

        return new WiringHandleClassInfo(
            fqn, name, ns, varName, isStore, hasSyncHandlers, hasAsyncHandlers,
            subscribedTypes.Distinct().ToList(),
            publicPropertyTypeFqns,
            isHydrationStore);
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
    // Analyse: Domain-Typen via Compilation
    // ═══════════════════════════════════════════════════

    private static ImmutableArray<WiringDomainTypeInfo> CollectDomainTypes(Compilation compilation)
    {
        var iCommand = compilation.GetTypeByMetadataName("Abstractions.ICommand");
        var iEvent = compilation.GetTypeByMetadataName("Abstractions.IEvent");
        var iClientEvent = compilation.GetTypeByMetadataName("Client.Infrastructure.Abstractions.IClientEvent");
        var iQuery = compilation.GetTypeByMetadataName("Abstractions.IQuery");
        var iState = compilation.GetTypeByMetadataName("Abstractions.IState");

        var results = ImmutableArray.CreateBuilder<WiringDomainTypeInfo>();

        WalkNamespace(compilation.Assembly.GlobalNamespace,
            iCommand, iEvent, iClientEvent, iQuery, iState, results);

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

    private static void WalkNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol? iCommand, INamedTypeSymbol? iEvent,
        INamedTypeSymbol? iClientEvent, INamedTypeSymbol? iQuery, INamedTypeSymbol? iState,
        ImmutableArray<WiringDomainTypeInfo>.Builder results)
    {
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

    private static WiringDomainTypeInfo? ClassifyDomainType(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? iCommand, INamedTypeSymbol? iEvent,
        INamedTypeSymbol? iClientEvent, INamedTypeSymbol? iQuery, INamedTypeSymbol? iState)
    {
        var fqn = symbol.ToDisplayString();

        if (iCommand != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iCommand)))
            return new WiringDomainTypeInfo(fqn, DomainTypeKind.Command);

        if (iQuery != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iQuery)))
            return new WiringDomainTypeInfo(fqn, DomainTypeKind.Query);

        if (iEvent != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iEvent)))
        {
            var isClientEvent = iClientEvent != null && symbol.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i, iClientEvent));
            if (!isClientEvent)
                return new WiringDomainTypeInfo(fqn, DomainTypeKind.ServerEvent);
        }

        if (iState != null && symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iState)))
            return new WiringDomainTypeInfo(fqn, DomainTypeKind.State);

        return null;
    }

    // ═══════════════════════════════════════════════════
    // Code-Generierung — Einstiegspunkt
    // ═══════════════════════════════════════════════════

    private static void Execute(
        SourceProductionContext context,
        List<WiringHandleClassInfo> handleClasses,
        List<WiringViewModelInfo> viewModels,
        List<WiringDomainTypeInfo> domainTypes,
        string assemblyName)
    {
        if (handleClasses.Count == 0 && viewModels.Count == 0 && domainTypes.Count == 0) return;

        var suffix = Sanitize(assemblyName);
        var className = $"GeneratedWiring_{suffix}";
        var methodName = $"AddClientDomain_{suffix}";

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

        var commandAggregateMap = BuildCommandAggregateTypeMap(commands, states);

        // ── Store-Baum aufbauen ──
        var storeFqns = new HashSet<string>(stores.Select(s => s.Fqn));
        var storeByFqn = stores.ToDictionary(s => s.Fqn);

        var childrenOf = stores.ToDictionary(
            s => s.Fqn,
            s => s.PublicPropertyTypeFqns
                  .Where(typeFqn => storeFqns.Contains(typeFqn))
                  .Select(typeFqn => storeByFqn[typeFqn])
                  .ToList());

        var referencedAsChild = new HashSet<string>(
            childrenOf.Values.SelectMany(children => children.Select(c => c.Fqn)));

        var rootStores = stores.Where(s => !referencedAsChild.Contains(s.Fqn)).ToList();

        // ── Usings sammeln ──
        var allNamespaces = handleClasses.Select(h => h.Namespace)
            .Concat(viewModels.Select(v => v.Namespace))
            .Distinct()
            .OrderBy(n => n);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Generator: WiringGenerator → {className}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using global::Client.Infrastructure.Abstractions;");
        sb.AppendLine("using global::Client.Infrastructure.Bus;");
        sb.AppendLine("using global::Client.Infrastructure.Connection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        foreach (var ns in allNamespaces)
            sb.AppendLine($"using global::{ns};");

        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");

        GenerateAddClient(sb, stores, syncHandlers, asyncHandlers, viewModels, className, methodName);
        GenerateSubscribeAll(sb, stores, syncHandlers, asyncHandlers, rootStores, childrenOf);
        GenerateUnsubscribeAll(sb, stores, rootStores, childrenOf);
        GenerateTypeLists(sb, commands, serverEvents, stores);
        GenerateCommandAggregateTypes(sb, commandAggregateMap);
        GenerateRegisterQueries(sb, queries);

        sb.AppendLine("}");

        context.AddSource("GeneratedWiring.g.cs", sb.ToString());
    }

    // ═══════════════════════════════════════════════════
    // Store-Baum: Depth-First Traversal
    // ═══════════════════════════════════════════════════

    private static IEnumerable<WiringHandleClassInfo> DepthFirst(
        WiringHandleClassInfo node,
        Dictionary<string, List<WiringHandleClassInfo>> childrenOf,
        HashSet<string> visited)
    {
        if (!visited.Add(node.Fqn)) yield break;
        yield return node;
        foreach (var child in childrenOf[node.Fqn])
            foreach (var descendant in DepthFirst(child, childrenOf, visited))
                yield return descendant;
    }

    // ═══════════════════════════════════════════════════
    // CommandAggregateType Mapping
    // ═══════════════════════════════════════════════════

    private static Dictionary<string, string> BuildCommandAggregateTypeMap(
        List<WiringDomainTypeInfo> commands,
        List<WiringDomainTypeInfo> states)
    {
        var stateByNamespace = new Dictionary<string, string>();
        foreach (var state in states)
        {
            var lastDot = state.Fqn.LastIndexOf('.');
            if (lastDot < 0) continue;
            var ns = state.Fqn.Substring(0, lastDot);
            var name = state.Fqn.Substring(lastDot + 1);
            stateByNamespace[ns] = name;
        }

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

    // ═══════════════════════════════════════════════════
    // AddClientDomain_{Suffix} — Extension Method
    // ═══════════════════════════════════════════════════

    private static void GenerateAddClient(
        StringBuilder sb,
        List<WiringHandleClassInfo> stores,
        List<WiringHandleClassInfo> syncHandlers,
        List<WiringHandleClassInfo> asyncHandlers,
        List<WiringViewModelInfo> viewModels,
        string className,
        string methodName)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registriert alle Stores, Handler, ViewModels und ClientWiringConfig.");
        sb.AppendLine("    /// Scoped = eine Instanz pro Circuit (Blazor Server) bzw. pro Window (MAUI).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static IServiceCollection {methodName}(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var store in stores)
            sb.AppendLine($"        services.AddScoped<{store.Fqn}>();");

        foreach (var handler in syncHandlers.Concat(asyncHandlers))
            sb.AppendLine($"        services.AddScoped<{handler.Fqn}>();");

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
        sb.AppendLine("        // ClientWiringConfig — Delegates für den generischen CircuitHandler");
        sb.AppendLine($"        services.AddSingleton(new ClientWiringConfig(");
        sb.AppendLine($"            SubscribeAll: {className}.SubscribeAll,");
        sb.AppendLine($"            UnsubscribeAll: {className}.UnsubscribeAll,");
        sb.AppendLine($"            RegisterQueries: {className}.RegisterQueries,");
        sb.AppendLine($"            ServerEventTypes: {className}.ServerEventTypes,");
        sb.AppendLine($"            CommandTypes: {className}.CommandTypes,");
        sb.AppendLine($"            CommandAggregateTypes: {className}.CommandAggregateTypes,");
        sb.AppendLine($"            HydrationStores: {className}.HydrationStores));");
        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════
    // SubscribeAll
    // ═══════════════════════════════════════════════════

    private static void GenerateSubscribeAll(
        StringBuilder sb,
        List<WiringHandleClassInfo> allStores,
        List<WiringHandleClassInfo> syncHandlers,
        List<WiringHandleClassInfo> asyncHandlers,
        List<WiringHandleClassInfo> rootStores,
        Dictionary<string, List<WiringHandleClassInfo>> childrenOf)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registriert alle Stores und Handler auf dem Bus.");
        sb.AppendLine("    /// Store-Baum wird depth-first traversiert: Eltern vor Kindern,");
        sb.AppendLine("    /// ältere Geschwister vor jüngeren (Deklarationsreihenfolge).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void SubscribeAll(IServiceProvider sp, IBus bus)");
        sb.AppendLine("    {");
        sb.AppendLine("        var clientBus = (ClientBus)bus;");
        sb.AppendLine();

        if (allStores.Count > 0)
        {
            sb.AppendLine("        // ── Instanzen auflösen ──");
            foreach (var store in allStores)
                sb.AppendLine($"        var {store.VarName} = sp.GetRequiredService<{store.Fqn}>();");
        }

        if (syncHandlers.Count > 0 || asyncHandlers.Count > 0)
        {
            foreach (var handler in syncHandlers.Concat(asyncHandlers))
                sb.AppendLine($"        var {handler.VarName} = sp.GetRequiredService<{handler.Fqn}>();");
        }

        sb.AppendLine();

        if (allStores.Count > 0)
        {
            sb.AppendLine("        // ── Store-Baum depth-first: AttachToBus + Subscribe ──");
            sb.AppendLine();

            var visited = new HashSet<string>();

            foreach (var root in rootStores)
                foreach (var node in DepthFirst(root, childrenOf, visited))
                    GenerateStoreBlock(sb, node);

            var standaloneStores = allStores.Where(s => !visited.Contains(s.Fqn)).ToList();
            if (standaloneStores.Count > 0)
            {
                sb.AppendLine("        // ── Standalone-Stores ──");
                foreach (var store in standaloneStores)
                    GenerateStoreBlock(sb, store);
            }
        }

        if (syncHandlers.Count > 0)
        {
            sb.AppendLine("        // ── Handler (sync) ──");
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
            sb.AppendLine("        // ── Handler (async) ──");
            foreach (var handler in asyncHandlers)
                foreach (var eventType in handler.SubscribedTypes)
                {
                    sb.AppendLine($"        bus.SubscribeAsync<{eventType}>(async (evt, ctx) =>");
                    sb.AppendLine($"            await {handler.VarName}.DispatchAsync(evt, ctx, async msg =>");
                    sb.AppendLine("            {");
                    sb.AppendLine("                clientBus.PostToSyncContext(() => bus.Publish(msg, MessageContext.Local()));");
                    sb.AppendLine("            }));");
                }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateStoreBlock(StringBuilder sb, WiringHandleClassInfo store)
    {
        sb.AppendLine($"        // {store.Name}");
        sb.AppendLine($"        {store.VarName}.AttachToBus(clientBus);");
        foreach (var eventType in store.SubscribedTypes)
            sb.AppendLine($"        bus.Subscribe<{eventType}>((evt, ctx) => {store.VarName}.Dispatch(evt, ctx));");
        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════
    // UnsubscribeAll
    // ═══════════════════════════════════════════════════

    private static void GenerateUnsubscribeAll(
        StringBuilder sb,
        List<WiringHandleClassInfo> allStores,
        List<WiringHandleClassInfo> rootStores,
        Dictionary<string, List<WiringHandleClassInfo>> childrenOf)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Trennt alle Stores vom Bus. Kinder vor Eltern.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void UnsubscribeAll(IServiceProvider sp)");
        sb.AppendLine("    {");

        if (allStores.Count > 0)
        {
            var visited = new HashSet<string>();
            var orderedStores = new List<WiringHandleClassInfo>();

            foreach (var root in rootStores)
                foreach (var node in DepthFirst(root, childrenOf, visited))
                    orderedStores.Add(node);

            foreach (var store in allStores.Where(s => !visited.Contains(s.Fqn)))
                orderedStores.Add(store);

            orderedStores.Reverse();

            foreach (var store in orderedStores)
                sb.AppendLine($"        sp.GetRequiredService<{store.Fqn}>().DetachFromBus();");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════
    // ServerEventTypes + CommandTypes
    // ═══════════════════════════════════════════════════

    private static void GenerateTypeLists(
        StringBuilder sb,
        List<WiringDomainTypeInfo> commands,
        List<WiringDomainTypeInfo> serverEvents,
        List<WiringHandleClassInfo> stores)
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

        // ── HydrationStores: Stores die IHydrationStore implementieren ──
        var hydrationStores = stores.Where(s => s.IsHydrationStore).ToList();
        sb.AppendLine("    /// <summary>Store-Typen die zur Start-Hydration gehören (IHydrationStore).</summary>");
        if (hydrationStores.Count == 0)
        {
            sb.AppendLine("    public static IReadOnlyList<Type> HydrationStores { get; } = Array.Empty<Type>();");
        }
        else
        {
            sb.AppendLine("    public static IReadOnlyList<Type> HydrationStores { get; } = new Type[]");
            sb.AppendLine("    {");
            foreach (var store in hydrationStores)
                sb.AppendLine($"        typeof({store.Fqn}),");
            sb.AppendLine("    };");
        }
        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════
    // CommandAggregateTypes
    // ═══════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════
    // RegisterQueries
    // ═══════════════════════════════════════════════════

    private static void GenerateRegisterQueries(
        StringBuilder sb,
        List<WiringDomainTypeInfo> queries)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registriert Query→Response-Mappings auf der QueryBridge.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void RegisterQueries(QueryBridge queryBridge, ClientBus bus)");
        sb.AppendLine("    {");
        foreach (var query in queries.OrderBy(q => q.Fqn))
            sb.AppendLine($"        queryBridge.Register<{query.Fqn}, Abstractions.IQueryResponse>(bus);");
        sb.AppendLine("    }");
    }

    // ═══════════════════════════════════════════════════
    // Helper
    // ═══════════════════════════════════════════════════

    private static string Sanitize(string name)
        => new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}

// ═══════════════════════════════════════════════════
// Modelle
// ═══════════════════════════════════════════════════

internal enum DomainTypeKind { Command, ServerEvent, Query, State }

internal class WiringDomainTypeInfo
{
    public string Fqn { get; }
    public DomainTypeKind Kind { get; }
    public WiringDomainTypeInfo(string fqn, DomainTypeKind kind) { Fqn = fqn; Kind = kind; }
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
    public List<string> PublicPropertyTypeFqns { get; }
    public bool IsHydrationStore { get; }

    public WiringHandleClassInfo(
        string fqn, string name, string ns, string varName,
        bool isStore, bool hasSyncHandlers, bool hasAsyncHandlers,
        List<string> subscribedTypes, List<string> publicPropertyTypeFqns,
        bool isHydrationStore = false)
    {
        Fqn = fqn; Name = name; Namespace = ns; VarName = varName;
        IsStore = isStore; HasSyncHandlers = hasSyncHandlers; HasAsyncHandlers = hasAsyncHandlers;
        SubscribedTypes = subscribedTypes; PublicPropertyTypeFqns = publicPropertyTypeFqns;
        IsHydrationStore = isHydrationStore;
    }
}

internal class WiringViewModelInfo
{
    public string Fqn { get; }
    public string Name { get; }
    public string Namespace { get; }
    public WiringViewModelInfo(string fqn, string name, string ns) { Fqn = fqn; Name = name; Namespace = ns; }
}