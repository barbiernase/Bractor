using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>
/// Extrahiert den vollständigen Wissensgraphen aus einer Solution.
///
/// Sechs Phasen:
///   1. Typ-Universum entdecken (Marker-Interfaces)
///   2. Aggregate analysieren (Decide/Apply/State)
///   3. Subscriber/Projektionen analysieren
///   4. Pipelines analysieren
///   5. Reader/Queries analysieren
///   6. Client-Stores + User Intents analysieren
///
/// Danach: Event-Fanout berechnen (welches Event → welche Consumer).
///
/// Patterns portiert von:
///   AggregateHandlerGenerator, SubscriberDispatchGenerator,
///   PipelineDispatchGenerator, ProjectionReaderDispatchGenerator,
///   TypeRegistryGenerator, EventCommandMappingGenerator
/// </summary>
public class GraphExtractor
{
    private readonly List<Compilation> _compilations;
    private readonly Dictionary<string, INamedTypeSymbol> _allTypes = new();

    // Interface-Symbole (aufgelöst in Phase 1)
    private INamedTypeSymbol? _iState;
    private INamedTypeSymbol? _iCommand;
    private INamedTypeSymbol? _iCreationCommand;
    private INamedTypeSymbol? _iEvent;
    private INamedTypeSymbol? _iTransientEvent;
    private INamedTypeSymbol? _iQuery;
    private INamedTypeSymbol? _iQueryResponse;
    private INamedTypeSymbol? _iSubscriber;
    private INamedTypeSymbol? _iPipelineHandler;
    private INamedTypeSymbol? _iPipelineTrigger;
    private INamedTypeSymbol? _iClientEvent;
    private INamedTypeSymbol? _iReadModel;
    private INamedTypeSymbol? _iAggregateEnvelope;
    private INamedTypeSymbol? _iMessageEnvelope;
    private INamedTypeSymbol? _projectionWriter;
    private INamedTypeSymbol? _pipelineContext;
    private INamedTypeSymbol? _messageContext;
    private INamedTypeSymbol? _readContext;
    private INamedTypeSymbol? _iReaderOpen;  // IReader<> open generic
    private INamedTypeSymbol? _iBus;

    public GraphExtractor(IEnumerable<Compilation> compilations)
    {
        _compilations = compilations.ToList();
        CacheAllTypes();
    }

    private void CacheAllTypes()
    {
        foreach (var compilation in _compilations)
        {
            var visitor = new TypeCollector(_allTypes);
            visitor.Visit(compilation.GlobalNamespace);
        }
        Console.WriteLine($"   {_allTypes.Count} Typen gecached");
    }

    // ═══════════════════════════════════════════════════════
    // PUBLIC: Extract
    // ═══════════════════════════════════════════════════════

    public KnowledgeGraph Extract()
    {
        var graph = new KnowledgeGraph();

        // Phase 1: Interface-Symbole auflösen
        ResolveInterfaces();

        // Phase 2: Aggregate
        Console.WriteLine("\n── Phase 2: Aggregate ──");
        graph.Aggregates = ExtractAggregates();
        Console.WriteLine($"   {graph.Aggregates.Count} Aggregate");

        // Phase 3: Projektionen
        Console.WriteLine("\n── Phase 3: Projektionen ──");
        graph.Projections = ExtractProjections();
        Console.WriteLine($"   {graph.Projections.Count} Projektionen");

        // Phase 4: Pipelines
        Console.WriteLine("\n── Phase 4: Pipelines ──");
        graph.Pipelines = ExtractPipelines();
        Console.WriteLine($"   {graph.Pipelines.Count} Pipelines");

        // Phase 5: Reader + Queries
        Console.WriteLine("\n── Phase 5: Reader + Queries ──");
        graph.Readers = ExtractReaders();
        graph.Queries = ExtractQueries();
        Console.WriteLine($"   {graph.Readers.Count} Reader, {graph.Queries.Count} Queries");

        // Phase 6: Client
        Console.WriteLine("\n── Phase 6: Client ──");
        var (stores, handlers) = ExtractClientLayer();
        graph.ClientStores = stores;
        graph.ClientHandlers = handlers;
        Console.WriteLine($"   {stores.Count} Stores, {handlers.Count} Handler");

        // Event-Fanout berechnen
        Console.WriteLine("\n── Event-Fanout ──");
        graph.EventFanouts = ComputeEventFanout(graph);
        Console.WriteLine($"   {graph.EventFanouts.Count} Events mit Fanout");

        return graph;
    }

    // ═══════════════════════════════════════════════════════
    // PHASE 1: Interface-Symbole auflösen
    // ═══════════════════════════════════════════════════════

    private void ResolveInterfaces()
    {
        Console.WriteLine("\n── Phase 1: Interfaces auflösen ──");

        // Suche über alle Compilations — Interface könnte in jeder liegen
        foreach (var compilation in _compilations)
        {
            _iState ??= compilation.GetTypeByMetadataName("Abstractions.IState");
            _iCommand ??= compilation.GetTypeByMetadataName("Abstractions.ICommand");
            _iCreationCommand ??= compilation.GetTypeByMetadataName("Abstractions.ICreationCommand");
            _iEvent ??= compilation.GetTypeByMetadataName("Abstractions.IEvent");
            _iTransientEvent ??= compilation.GetTypeByMetadataName("Abstractions.ITransientEvent");
            _iQuery ??= compilation.GetTypeByMetadataName("Abstractions.IQuery");
            _iQueryResponse ??= compilation.GetTypeByMetadataName("Abstractions.IQueryResponse");
            _iSubscriber ??= compilation.GetTypeByMetadataName("Abstractions.ISubscriber");
            _iPipelineHandler ??= compilation.GetTypeByMetadataName("Abstractions.IPipelineHandler");
            _iPipelineTrigger ??= compilation.GetTypeByMetadataName("Abstractions.IPipelineTrigger");
            _iClientEvent ??= compilation.GetTypeByMetadataName("Client.Infrastructure.Abstractions.IClientEvent");
            _iReadModel ??= compilation.GetTypeByMetadataName("Abstractions.IReadModel");
            _iAggregateEnvelope ??= compilation.GetTypeByMetadataName("Abstractions.IAggregateEnvelope");
            _iMessageEnvelope ??= compilation.GetTypeByMetadataName("Abstractions.IMessageEnvelope");
            _projectionWriter ??= compilation.GetTypeByMetadataName("Core.ProjectionWriter");
            _pipelineContext ??= compilation.GetTypeByMetadataName("Abstractions.PipelineContext");
            _messageContext ??= compilation.GetTypeByMetadataName("Client.Infrastructure.Abstractions.MessageContext")
                             ?? compilation.GetTypeByMetadataName("Abstractions.MessageContext");
            _readContext ??= compilation.GetTypeByMetadataName("Abstractions.ReadContext");
            _iReaderOpen ??= compilation.GetTypeByMetadataName("Abstractions.IReader`1");
            _iBus ??= compilation.GetTypeByMetadataName("Client.Infrastructure.Abstractions.IBus");
        }

        var resolved = new[] { _iState, _iCommand, _iEvent, _iSubscriber, _iPipelineHandler, _iQuery }
            .Count(x => x != null);
        Console.WriteLine($"   {resolved}/6 Kern-Interfaces aufgelöst");
    }

    // ═══════════════════════════════════════════════════════
    // PHASE 2: Aggregate
    // Pattern: AggregateHandlerGenerator
    // ═══════════════════════════════════════════════════════

    private List<AggregateNode> ExtractAggregates()
    {
        var result = new List<AggregateNode>();
        if (_iState == null) return result;

        foreach (var (_, typeSymbol) in _allTypes)
        {
            if (!Implements(typeSymbol, _iState)) continue;

            var deciderSymbol = typeSymbol.GetTypeMembers("Decider").FirstOrDefault();
            var applierSymbol = typeSymbol.GetTypeMembers("Applier").FirstOrDefault();
            if (deciderSymbol == null || applierSymbol == null) continue;

            var aggregate = new AggregateNode
            {
                Name = typeSymbol.Name,
                Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                FullName = typeSymbol.ToDisplayString(),
                State = ExtractState(typeSymbol),
                Decides = ExtractDecides(deciderSymbol),
                Applies = ExtractApplies(applierSymbol),
            };

            result.Add(aggregate);
            Console.WriteLine($"   ✓ {aggregate.Name}: {aggregate.Decides.Count} Decides, {aggregate.Applies.Count} Applies");
        }

        return result;
    }

    private StateNode ExtractState(INamedTypeSymbol stateType)
    {
        var state = new StateNode();

        // Properties (formal)
        foreach (var member in stateType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.IsStatic || member.IsIndexer) continue;
            if (member.DeclaredAccessibility != Accessibility.Public) continue;

            // Derived Properties: expression-bodied getter ohne setter
            if (member.SetMethod == null && member.GetMethod != null)
            {
                var code = GetPropertyCode(member);
                if (!string.IsNullOrEmpty(code))
                {
                    state.DerivedProperties.Add(new DerivedProperty
                    {
                        Name = member.Name,
                        CodePayload = code,
                    });
                    continue;
                }
            }

            state.Fields.Add(new FieldInfo
            {
                Name = member.Name,
                Type = member.Type.ToDisplayString(),
            });
        }

        // Methoden die wie Derived Properties wirken (GetBild etc.)
        foreach (var method in stateType.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.IsStatic || method.MethodKind != MethodKind.Ordinary) continue;
            if (method.DeclaredAccessibility != Accessibility.Public) continue;
            if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) continue;

            var code = GetMethodBody(method);
            if (!string.IsNullOrEmpty(code))
            {
                state.DerivedProperties.Add(new DerivedProperty
                {
                    Name = $"{method.Name}({string.Join(", ", method.Parameters.Select(p => p.Type.ToDisplayString() + " " + p.Name))})",
                    CodePayload = code,
                });
            }
        }

        return state;
    }

    private List<DecideNode> ExtractDecides(INamedTypeSymbol deciderType)
    {
        var result = new List<DecideNode>();

        var decideMethods = deciderType.GetMembers("Decide")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 1)
            .ToList();

        foreach (var method in decideMethods)
        {
            var commandType = method.Parameters[0].Type;
            var commandTypeName = commandType.ToDisplayString();
            var isCreation = _iCreationCommand != null && Implements(commandType, _iCreationCommand);

            var universe = ExtractOneOfUniverse(method.ReturnType);
            var body = GetMethodBody(method);

            result.Add(new DecideNode
            {
                CommandType = commandTypeName,
                IsCreation = isCreation,
                Universe = universe,
                CodePayload = body ?? "",
            });
        }

        return result;
    }

    private List<DecideOutput> ExtractOneOfUniverse(ITypeSymbol returnType)
    {
        var outputs = new List<DecideOutput>();

        // IEnumerable<OneOf<T1, T2, ...>> oder IEnumerable<T>
        if (returnType is INamedTypeSymbol { IsGenericType: true } enumerableType)
        {
            var elementType = enumerableType.TypeArguments.FirstOrDefault();

            if (elementType is INamedTypeSymbol { IsGenericType: true, Name: "OneOf" } oneOfType)
            {
                // OneOf<T1, T2, T3, ...> → jeder Typparameter ist ein möglicher Output
                foreach (var typeArg in oneOfType.TypeArguments)
                {
                    outputs.Add(new DecideOutput
                    {
                        EventType = typeArg.ToDisplayString(),
                        Kind = IsTransient(typeArg) ? "reject" : "persist",
                    });
                }
            }
            else if (elementType != null)
            {
                // IEnumerable<TEvent> → einzelner Output-Typ
                outputs.Add(new DecideOutput
                {
                    EventType = elementType.ToDisplayString(),
                    Kind = IsTransient(elementType) ? "reject" : "persist",
                });
            }
        }

        return outputs;
    }

    private List<ApplyNode> ExtractApplies(INamedTypeSymbol applierType)
    {
        var result = new List<ApplyNode>();

        var applyMethods = applierType.GetMembers("Apply")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 1)
            .ToList();

        foreach (var method in applyMethods)
        {
            result.Add(new ApplyNode
            {
                EventType = method.Parameters[0].Type.ToDisplayString(),
                CodePayload = GetMethodBody(method) ?? "",
            });
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════
    // PHASE 3: Projektionen
    // Pattern: SubscriberDispatchGenerator
    // ═══════════════════════════════════════════════════════

    private List<ProjectionNode> ExtractProjections()
    {
        var result = new List<ProjectionNode>();
        if (_iSubscriber == null) return result;

        foreach (var (_, typeSymbol) in _allTypes)
        {
            if (!Implements(typeSymbol, _iSubscriber)) continue;

            var handles = FindHandleMethods(typeSymbol,
                secondParam: _iAggregateEnvelope,
                thirdParam: _projectionWriter,
                requiredParams: 3);

            // SubscriberId aus Property extrahieren
            var subscriberIdProp = typeSymbol.GetMembers("SubscriberId")
                .OfType<IPropertySymbol>()
                .FirstOrDefault();
            var subscriberId = GetPropertyCode(subscriberIdProp) ?? typeSymbol.Name;

            var projection = new ProjectionNode
            {
                Name = typeSymbol.Name,
                FullName = typeSymbol.ToDisplayString(),
                SubscriberId = subscriberId.Trim('"'),
                Handles = handles.Select(h => new ProjectionHandleNode
                {
                    EventType = h.InputType,
                    CodePayload = h.Body,
                }).ToList(),
            };

            result.Add(projection);
            Console.WriteLine($"   ✓ {projection.Name}: {projection.Handles.Count} Handles");
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════
    // PHASE 4: Pipelines
    // Pattern: PipelineDispatchGenerator
    // ═══════════════════════════════════════════════════════

    private List<PipelineNode> ExtractPipelines()
    {
        var result = new List<PipelineNode>();
        if (_iPipelineHandler == null) return result;

        foreach (var (_, typeSymbol) in _allTypes)
        {
            if (!Implements(typeSymbol, _iPipelineHandler)) continue;

            var allHandles = typeSymbol.GetMembers("Handle")
                .OfType<IMethodSymbol>()
                .Where(m => m.Parameters.Length == 2)
                .ToList();

            var pipelineIdProp = typeSymbol.GetMembers("PipelineId")
                .OfType<IPropertySymbol>()
                .FirstOrDefault();
            var pipelineId = GetPropertyCode(pipelineIdProp) ?? typeSymbol.Name;

            var handles = new List<PipelineHandleNode>();
            foreach (var method in allHandles)
            {
                var inputType = method.Parameters[0].Type;
                var inputTypeName = inputType.ToDisplayString();
                var isTrigger = _iPipelineTrigger != null && Implements(inputType, _iPipelineTrigger);

                var producedCommands = ExtractProducedCommands(method);
                var body = GetMethodBody(method);

                handles.Add(new PipelineHandleNode
                {
                    InputType = inputTypeName,
                    InputKind = isTrigger ? "trigger" : "event",
                    ProducedCommands = producedCommands,
                    CodePayload = body ?? "",
                });
            }

            result.Add(new PipelineNode
            {
                Name = typeSymbol.Name,
                FullName = typeSymbol.ToDisplayString(),
                PipelineId = pipelineId.Trim('"'),
                Handles = handles,
            });

            Console.WriteLine($"   ✓ {typeSymbol.Name}: {handles.Count} Handles");
        }

        return result;
    }

    /// <summary>
    /// Extrahiert Command-Typen die von einer Pipeline-Handle-Methode erzeugt werden.
    /// Strategie: Finde alle ObjectCreationExpression im Body deren Typ ICommand implementiert.
    /// </summary>
    private List<string> ExtractProducedCommands(IMethodSymbol method)
    {
        var commands = new List<string>();

        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return commands;

        var syntaxNode = syntaxRef.GetSyntax();
        var tree = syntaxNode.SyntaxTree;
        var compilation = _compilations.FirstOrDefault(c => c.ContainsSyntaxTree(tree));
        if (compilation == null) return commands;

        var semanticModel = compilation.GetSemanticModel(tree);

        // Finde alle "new XyzCommand(...)" oder "new Xyz(...)" im Body
        var creations = syntaxNode.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>();

        foreach (var creation in creations)
        {
            var typeInfo = semanticModel.GetTypeInfo(creation);
            if (typeInfo.Type is INamedTypeSymbol createdType)
            {
                if (_iCommand != null && Implements(createdType, _iCommand))
                {
                    var name = createdType.ToDisplayString();
                    if (!commands.Contains(name))
                        commands.Add(name);
                }
            }
        }

        // Auch ImplicitObjectCreation: "yield return new(...)" bei target-typed new
        var implicitCreations = syntaxNode.DescendantNodes()
            .OfType<ImplicitObjectCreationExpressionSyntax>();

        foreach (var creation in implicitCreations)
        {
            var typeInfo = semanticModel.GetTypeInfo(creation);
            if (typeInfo.ConvertedType is INamedTypeSymbol createdType)
            {
                if (_iCommand != null && Implements(createdType, _iCommand))
                {
                    var name = createdType.ToDisplayString();
                    if (!commands.Contains(name))
                        commands.Add(name);
                }
            }
        }

        return commands;
    }

    // ═══════════════════════════════════════════════════════
    // PHASE 5: Reader + Queries
    // Pattern: ProjectionReaderDispatchGenerator
    // ═══════════════════════════════════════════════════════

    private List<ReaderNode> ExtractReaders()
    {
        var result = new List<ReaderNode>();
        if (_iReaderOpen == null) return result;

        foreach (var (_, typeSymbol) in _allTypes)
        {
            // Implementiert IReader<TProjection>?
            var readerInterfaceName = _iReaderOpen.ToDisplayString();
            var readerInterface = typeSymbol.AllInterfaces
                .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == readerInterfaceName);
            if (readerInterface == null) continue;

            var projectionType = readerInterface.TypeArguments[0];

            // TrackDeps aus [ProjectionReader]-Attribut
            var trackDeps = true; // default
            var attr = typeSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "ProjectionReaderAttribute");
            if (attr != null)
            {
                var trackDepsArg = attr.NamedArguments
                    .FirstOrDefault(a => a.Key == "TrackDeps");
                if (trackDepsArg.Value.Value is bool tdValue)
                    trackDeps = tdValue;
            }

            // Handle-Methoden: (IQuery, IMessageEnvelope, ReadContext) → T
            var handles = new List<ReaderHandleNode>();
            var handleMethods = typeSymbol.GetMembers("Handle")
                .OfType<IMethodSymbol>()
                .Where(m => m.Parameters.Length == 3
                    && (_iMessageEnvelope != null && SymbolEquals(m.Parameters[1].Type, _iMessageEnvelope))
                    && (_readContext != null && SymbolEquals(m.Parameters[2].Type, _readContext)))
                .ToList();

            foreach (var method in handleMethods)
            {
                var queryType = method.Parameters[0].Type.ToDisplayString();
                var responseTypes = ExtractResponseTypes(method.ReturnType);
                var body = GetMethodBody(method);

                handles.Add(new ReaderHandleNode
                {
                    QueryType = queryType,
                    ResponseTypes = responseTypes,
                    CodePayload = body ?? "",
                });
            }

            result.Add(new ReaderNode
            {
                Name = typeSymbol.Name,
                FullName = typeSymbol.ToDisplayString(),
                ProjectionName = projectionType.Name,
                TrackDeps = trackDeps,
                Handles = handles,
            });

            Console.WriteLine($"   ✓ {typeSymbol.Name}: {handles.Count} Handles");
        }

        return result;
    }

    private List<string> ExtractResponseTypes(ITypeSymbol returnType)
    {
        var types = new List<string>();

        // Task<T> → unwrap T
        if (returnType is INamedTypeSymbol { IsGenericType: true, Name: "Task" } taskType)
        {
            returnType = taskType.TypeArguments[0];
        }

        // OneOf<T1, T2> → mehrere Responses
        if (returnType is INamedTypeSymbol { IsGenericType: true, Name: "OneOf" } oneOf)
        {
            foreach (var arg in oneOf.TypeArguments)
                types.Add(arg.ToDisplayString());
        }
        else
        {
            types.Add(returnType.ToDisplayString());
        }

        return types;
    }

    private List<QueryNode> ExtractQueries()
    {
        var result = new List<QueryNode>();
        if (_iQuery == null) return result;

        foreach (var (_, typeSymbol) in _allTypes)
        {
            if (!Implements(typeSymbol, _iQuery)) continue;

            var fields = ExtractConstructorFields(typeSymbol);
            result.Add(new QueryNode
            {
                Name = typeSymbol.Name,
                FullName = typeSymbol.ToDisplayString(),
                Fields = fields,
            });
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════
    // PHASE 6: Client Layer
    // ═══════════════════════════════════════════════════════

    private (List<ClientStoreNode>, List<ClientHandlerNode>) ExtractClientLayer()
    {
        var stores = new List<ClientStoreNode>();
        var handlers = new List<ClientHandlerNode>();

        foreach (var (_, typeSymbol) in _allTypes)
        {
            // Heuristik: Hat ein IBus-Feld UND Handle-Methoden mit MessageContext?
            var hasBusField = typeSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => f.Type.Name == "IBus" || f.Type.Name == "ClientBus");

            if (!hasBusField) continue;

            var handleMethods = typeSymbol.GetMembers("Handle")
                .OfType<IMethodSymbol>()
                .Where(m => m.Parameters.Length == 2)
                .ToList();

            if (handleMethods.Count == 0) continue;

            // Unterscheidung: Store (hat ObservableProperty) vs. Handler (emittiert Events)
            var hasObservableState = typeSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => f.GetAttributes()
                    .Any(a => a.AttributeClass?.Name == "ObservablePropertyAttribute"));

            if (hasObservableState)
            {
                stores.Add(ExtractClientStore(typeSymbol, handleMethods));
            }
            else
            {
                handlers.Add(ExtractClientHandler(typeSymbol, handleMethods));
            }
        }

        return (stores, handlers);
    }

    private ClientStoreNode ExtractClientStore(INamedTypeSymbol typeSymbol, List<IMethodSymbol> handleMethods)
    {
        var store = new ClientStoreNode
        {
            Name = typeSymbol.Name,
            FullName = typeSymbol.ToDisplayString(),
        };

        // Handle-Methoden klassifizieren
        foreach (var method in handleMethods)
        {
            var inputType = method.Parameters[0].Type;
            var inputTypeName = inputType.ToDisplayString();
            var body = GetMethodBody(method);
            var kind = ClassifyClientInput(inputType);

            var handle = new StoreHandleNode
            {
                InputType = inputTypeName,
                InputKind = kind,
                CodePayload = body ?? "",
            };

            switch (kind)
            {
                case "server-event": store.EventHandles.Add(handle); break;
                case "query-response": store.ResponseHandles.Add(handle); break;
                case "rejection": store.RejectionHandles.Add(handle); break;
                default: store.InfraHandles.Add(handle); break;
            }
        }

        // User Intents: öffentliche Methoden die Publish aufrufen
        store.UserIntents = ExtractUserIntents(typeSymbol);

        // Observable State: Felder mit [ObservableProperty]
        store.ObservableState = ExtractObservableState(typeSymbol);

        // Derived Properties: expression-bodied public properties
        store.DerivedProperties = ExtractDerivedProperties(typeSymbol);

        Console.WriteLine($"   ✓ Store {typeSymbol.Name}: {store.EventHandles.Count} events, " +
                          $"{store.ResponseHandles.Count} responses, {store.UserIntents.Count} intents");

        return store;
    }

    private ClientHandlerNode ExtractClientHandler(INamedTypeSymbol typeSymbol, List<IMethodSymbol> handleMethods)
    {
        var handler = new ClientHandlerNode
        {
            Name = typeSymbol.Name,
            FullName = typeSymbol.ToDisplayString(),
        };

        foreach (var method in handleMethods)
        {
            var inputTypeName = method.Parameters[0].Type.ToDisplayString();
            handler.HandledEvents.Add(inputTypeName);

            // Return-Typ analysieren für emittierte Client-Events
            if (method.ReturnType is INamedTypeSymbol { IsGenericType: true } returnType)
            {
                var elementType = returnType.TypeArguments.FirstOrDefault();
                if (elementType != null && _iClientEvent != null && Implements(elementType, _iClientEvent))
                {
                    var emittedName = elementType.ToDisplayString();
                    if (!handler.EmittedClientEvents.Contains(emittedName))
                        handler.EmittedClientEvents.Add(emittedName);
                }
            }
        }

        // Gesamt-Code als Payload (da alle Handles dasselbe tun)
        handler.CodePayload = GetMethodBody(handleMethods.First()) ?? "";

        Console.WriteLine($"   ✓ Handler {typeSymbol.Name}: {handler.HandledEvents.Count} events → {handler.EmittedClientEvents.Count} emitted");

        return handler;
    }

    private string ClassifyClientInput(ITypeSymbol inputType)
    {
        if (_iEvent != null && Implements(inputType, _iEvent))
        {
            if (_iTransientEvent != null && Implements(inputType, _iTransientEvent))
                return "rejection";
            return "server-event";
        }
        if (_iQueryResponse != null && Implements(inputType, _iQueryResponse))
            return "query-response";
        if (_iClientEvent != null && Implements(inputType, _iClientEvent))
            return "client-event";
        return "infrastructure";
    }

    private List<UserIntentNode> ExtractUserIntents(INamedTypeSymbol typeSymbol)
    {
        var intents = new List<UserIntentNode>();

        var publicMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public
                && m.MethodKind == MethodKind.Ordinary
                && m.Name != "Handle"
                && !m.Name.StartsWith("get_")
                && !m.Name.StartsWith("set_"))
            .ToList();

        foreach (var method in publicMethods)
        {
            var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) continue;

            var syntaxNode = syntaxRef.GetSyntax();
            var tree = syntaxNode.SyntaxTree;
            var compilation = _compilations.FirstOrDefault(c => c.ContainsSyntaxTree(tree));
            if (compilation == null) continue;

            var semanticModel = compilation.GetSemanticModel(tree);

            // Finde alle Publish-Aufrufe im Body
            var publishedTypes = new List<string>();

            var invocations = syntaxNode.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression.ToString().Contains("Publish"));

            foreach (var inv in invocations)
            {
                var arg = inv.ArgumentList.Arguments.FirstOrDefault();
                if (arg == null) continue;

                var typeInfo = semanticModel.GetTypeInfo(arg.Expression);
                var publishedType = typeInfo.Type ?? typeInfo.ConvertedType;
                if (publishedType != null)
                {
                    var name = publishedType.ToDisplayString();
                    if (!publishedTypes.Contains(name))
                        publishedTypes.Add(name);
                }
            }

            // Nur Methoden die etwas publishen sind User Intents
            if (publishedTypes.Count == 0) continue;

            var sig = $"{method.Name}({string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))})";
            var body = GetMethodBody(method);

            intents.Add(new UserIntentNode
            {
                MethodName = method.Name,
                Signature = sig,
                Publishes = publishedTypes,
                CodePayload = body ?? "",
            });
        }

        return intents;
    }

    private List<FieldInfo> ExtractObservableState(INamedTypeSymbol typeSymbol)
    {
        var fields = new List<FieldInfo>();

        foreach (var field in typeSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var hasObservable = field.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "ObservablePropertyAttribute");
            if (!hasObservable) continue;

            // [ObservableProperty] private int _seite → Property "Seite"
            var propName = field.Name.TrimStart('_');
            propName = char.ToUpper(propName[0]) + propName[1..];

            fields.Add(new FieldInfo
            {
                Name = propName,
                Type = field.Type.ToDisplayString(),
            });
        }

        // Auch öffentliche non-backing Properties (z.B. Items, die Collection)
        foreach (var prop in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.IsStatic || prop.IsIndexer) continue;
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.SetMethod != null && prop.SetMethod.DeclaredAccessibility != Accessibility.Public) continue;

            // Überspringen wenn schon von [ObservableProperty] erfasst
            if (fields.Any(f => f.Name == prop.Name)) continue;

            // Nur Properties mit eigenem Getter (nicht expression-bodied Derived)
            if (prop.SetMethod == null && prop.GetMethod != null)
            {
                // Könnte Derived Property sein — überspringen
                continue;
            }

            fields.Add(new FieldInfo
            {
                Name = prop.Name,
                Type = prop.Type.ToDisplayString(),
            });
        }

        return fields;
    }

    private List<DerivedProperty> ExtractDerivedProperties(INamedTypeSymbol typeSymbol)
    {
        var derived = new List<DerivedProperty>();

        foreach (var prop in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.IsStatic || prop.IsIndexer) continue;
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;

            // Expression-bodied get-only property = Derived
            if (prop.SetMethod == null && prop.GetMethod != null)
            {
                var code = GetPropertyCode(prop);
                if (!string.IsNullOrEmpty(code))
                {
                    derived.Add(new DerivedProperty
                    {
                        Name = prop.Name,
                        CodePayload = code,
                    });
                }
            }
        }

        return derived;
    }

    // ═══════════════════════════════════════════════════════
    // EVENT FANOUT (berechnet)
    // ═══════════════════════════════════════════════════════

    private List<EventFanout> ComputeEventFanout(KnowledgeGraph graph)
    {
        // Alle Events sammeln (aus Decide-Universen)
        var allEvents = graph.Aggregates
            .SelectMany(a => a.Decides)
            .SelectMany(d => d.Universe)
            .Select(o => new { o.EventType, o.Kind })
            .DistinctBy(e => e.EventType)
            .ToList();

        var fanouts = new List<EventFanout>();

        foreach (var evt in allEvents)
        {
            var fanout = new EventFanout
            {
                EventType = evt.EventType,
                Kind = evt.Kind,
            };

            // Welche Projektionen konsumieren dieses Event?
            fanout.Projections = graph.Projections
                .Where(p => p.Handles.Any(h => h.EventType == evt.EventType))
                .Select(p => p.Name)
                .ToList();

            // Welche Pipelines reagieren?
            fanout.Pipelines = graph.Pipelines
                .Where(p => p.Handles.Any(h => h.InputType == evt.EventType))
                .Select(p => p.Name)
                .ToList();

            // Welche Client-Stores handlen es?
            fanout.ClientStores = graph.ClientStores
                .Where(s => s.EventHandles.Any(h => h.InputType == evt.EventType)
                          || s.RejectionHandles.Any(h => h.InputType == evt.EventType))
                .Select(s => s.Name)
                .ToList();

            // Welche Client-Handler handlen es?
            fanout.ClientHandlers = graph.ClientHandlers
                .Where(h => h.HandledEvents.Contains(evt.EventType))
                .Select(h => h.Name)
                .ToList();

            fanouts.Add(fanout);
        }

        return fanouts;
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════

    private struct HandleMethodInfo
    {
        public string InputType;
        public string Body;
    }

    private List<HandleMethodInfo> FindHandleMethods(
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol? secondParam,
        INamedTypeSymbol? thirdParam,
        int requiredParams)
    {
        var result = new List<HandleMethodInfo>();

        var methods = classSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == requiredParams)
            .ToList();

        foreach (var method in methods)
        {
            if (requiredParams >= 2 && secondParam != null
                && !SymbolEquals(method.Parameters[1].Type, secondParam))
                continue;

            if (requiredParams >= 3 && thirdParam != null
                && !SymbolEquals(method.Parameters[2].Type, thirdParam))
                continue;

            result.Add(new HandleMethodInfo
            {
                InputType = method.Parameters[0].Type.ToDisplayString(),
                Body = GetMethodBody(method) ?? "",
            });
        }

        return result;
    }

    private List<FieldInfo> ExtractConstructorFields(INamedTypeSymbol typeSymbol)
    {
        var fields = new List<FieldInfo>();

        var constructor = typeSymbol.InstanceConstructors
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault(c => c.Parameters.Length > 0);

        if (constructor != null)
        {
            foreach (var param in constructor.Parameters)
            {
                fields.Add(new FieldInfo
                {
                    Name = param.Name,
                    Type = param.Type.ToDisplayString(),
                });
            }
        }

        return fields;
    }

    /// <summary>
    /// Extrahiert den vollständigen Methodenbody als String.
    /// Geht von IMethodSymbol → SyntaxReference → SyntaxNode → Body.
    /// </summary>
    private string? GetMethodBody(IMethodSymbol method)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var syntaxNode = syntaxRef.GetSyntax();

        if (syntaxNode is MethodDeclarationSyntax methodDecl)
        {
            // Block body: { ... }
            if (methodDecl.Body != null)
                return methodDecl.Body.ToFullString().Trim();

            // Expression body: => ...
            if (methodDecl.ExpressionBody != null)
                return methodDecl.ExpressionBody.Expression.ToFullString().Trim();
        }

        // Fallback: gesamte Syntax
        return syntaxNode.ToFullString().Trim();
    }

    /// <summary>
    /// Extrahiert den Property-Wert (expression-bodied oder Initializer).
    /// </summary>
    private string? GetPropertyCode(IPropertySymbol? property)
    {
        if (property == null) return null;

        var syntaxRef = property.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var syntaxNode = syntaxRef.GetSyntax();

        if (syntaxNode is PropertyDeclarationSyntax propDecl)
        {
            // Expression body: => ...
            if (propDecl.ExpressionBody != null)
                return propDecl.ExpressionBody.Expression.ToFullString().Trim();

            // Initializer: = "value"
            if (propDecl.Initializer != null)
                return propDecl.Initializer.Value.ToFullString().Trim();
        }

        return null;
    }

    private bool Implements(ITypeSymbol type, INamedTypeSymbol interfaceSymbol)
    {
        // String-Vergleich statt SymbolEqualityComparer — 
        // Symbole aus verschiedenen Compilations sind NIE gleich per Reference.
        // Das ist das gleiche Pattern wie im MultiCompilationAnalyzer.
        var targetName = interfaceSymbol.ToDisplayString();
        if (type is INamedTypeSymbol namedType)
        {
            return namedType.AllInterfaces.Any(i =>
                i.ToDisplayString() == targetName
                || i.OriginalDefinition.ToDisplayString() == targetName);
        }
        return false;
    }

    private bool SymbolEquals(ITypeSymbol a, INamedTypeSymbol b)
    {
        return a.ToDisplayString() == b.ToDisplayString();
    }

    private bool IsTransient(ITypeSymbol type)
    {
        return _iTransientEvent != null && Implements(type, _iTransientEvent);
    }

    private class TypeCollector : SymbolVisitor
    {
        private readonly Dictionary<string, INamedTypeSymbol> _types;
        public TypeCollector(Dictionary<string, INamedTypeSymbol> types) => _types = types;

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
                member.Accept(this);
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (!symbol.IsAbstract && symbol.TypeKind != TypeKind.Interface)
                _types.TryAdd(symbol.ToDisplayString(), symbol);
            foreach (var nested in symbol.GetTypeMembers())
                nested.Accept(this);
        }
    }
}