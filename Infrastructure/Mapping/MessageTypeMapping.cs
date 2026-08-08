using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;

namespace Infrastructure.Mapping;

/// <summary>
/// Kategorien für Nachrichtentypen.
/// Bestimmt das Routing: Events werden empfangen (PubSub),
/// alles andere wird gesendet (Aggregate, Pipeline, QueryService).
/// </summary>
public enum MessageCategory
{
    Event,
    Command,
    Query,
    QueryResponse,
    Trigger,
    Unknown
}

/// <summary>
/// Zentrale und einzige Runtime-Registry für alle Message-Typen.
/// 
/// Ersetzt den ehemaligen EventTypeResolver — eine Klasse, eine Initialisierung.
/// Alle Aufrufer (SubscriptionTracker, CapabilitiesHandler, PubSubStartupService)
/// nutzen ausschließlich diese Klasse.
///
/// Daten kommen aus GeneratedTypeRegistry — keine Reflection.
/// </summary>
public static class MessageTypeMapping
{
    private static readonly Dictionary<string, Type> _eventTypes = new();
    private static readonly Dictionary<string, Type> _commandTypes = new();
    private static readonly Dictionary<string, Type> _queryTypes = new();
    private static readonly Dictionary<string, Type> _queryResponseTypes = new();
    private static readonly Dictionary<string, Type> _triggerTypes = new();
    
    private static readonly Dictionary<string, HashSet<string>> _eventToCommands = new();
    
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialisiert die Registry aus dem generierten Code.
    /// Keine Reflection — alles kommt aus GeneratedTypeRegistry.
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;

            foreach (var (name, type) in GeneratedTypeRegistry.Events)
                _eventTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.Commands)
                _commandTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.Queries)
                _queryTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.QueryResponses)
                _queryResponseTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.Triggers)
                _triggerTypes[name] = type;

            // ★ P1a (TG-1): event → Commands DESSELBEN Aggregats — präzise aus den Decider-Signaturen
            //   (GeneratedCommandRouting: CommandToAggregate + CommandToEvents), NICHT mehr aus Namespace-
            //   Gruppierung (die gelöschte GeneratedEventCommandMapping). Ein Event gehört zum Aggregat seines
            //   Erzeuger-Commands; die „erlaubten Commands" des Alt-Capability-Pfads (Blazor-Client) sind die
            //   Geschwister-Commands dieses Aggregats.
            var aggregatCommands = new Dictionary<string, HashSet<string>>();
            foreach (var (cmd, agg) in GeneratedCommandRouting.CommandToAggregate)
            {
                if (!aggregatCommands.TryGetValue(agg, out var set))
                    aggregatCommands[agg] = set = new HashSet<string>();
                set.Add(cmd.Name);
            }
            foreach (var (cmd, evts) in GeneratedCommandRouting.CommandToEvents)
            {
                if (!GeneratedCommandRouting.CommandToAggregate.TryGetValue(cmd, out var agg)) continue;
                if (!aggregatCommands.TryGetValue(agg, out var geschwister)) continue;
                foreach (var evt in evts)
                {
                    if (!_eventToCommands.TryGetValue(evt.Name, out var set))
                        _eventToCommands[evt.Name] = set = new HashSet<string>();
                    set.UnionWith(geschwister);
                }
            }

            _initialized = true;
            
            Console.WriteLine($"[MessageTypeMapping] Initialized:");
            Console.WriteLine($"  Events: {_eventTypes.Count}");
            Console.WriteLine($"  Commands: {_commandTypes.Count}");
            Console.WriteLine($"  Queries: {_queryTypes.Count}");
            Console.WriteLine($"  QueryResponses: {_queryResponseTypes.Count}");
            Console.WriteLine($"  Triggers: {_triggerTypes.Count}");
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

    // =========================================================================
    // UNIVERSELLE AUFLÖSUNG
    // =========================================================================

    /// <summary>
    /// Löst einen Typ-Namen über alle Kategorien auf.
    /// Einziger Einstiegspunkt für SubscriptionTracker, CapabilitiesHandler, etc.
    /// </summary>
    public static (Type? Type, MessageCategory Category) Resolve(string typeName)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(typeName))
            return (null, MessageCategory.Unknown);

        if (_eventTypes.TryGetValue(typeName, out var e))         return (e, MessageCategory.Event);
        if (_commandTypes.TryGetValue(typeName, out var c))       return (c, MessageCategory.Command);
        if (_triggerTypes.TryGetValue(typeName, out var t))        return (t, MessageCategory.Trigger);
        if (_queryTypes.TryGetValue(typeName, out var q))         return (q, MessageCategory.Query);
        if (_queryResponseTypes.TryGetValue(typeName, out var r)) return (r, MessageCategory.QueryResponse);
        return (null, MessageCategory.Unknown);
    }

    /// <summary>
    /// Gibt alle registrierten Event-Type-Objekte zurück.
    /// Ersetzt EventTypeResolver.GetAllEventTypes().
    /// </summary>
    public static IEnumerable<Type> GetAllEventTypes()
    {
        EnsureInitialized();
        return _eventTypes.Values;
    }

    /// <summary>
    /// Gibt alle registrierten Command-Type-Objekte zurück.
    /// Ersetzt EventTypeResolver.GetAllCommandTypes().
    /// </summary>
    public static IEnumerable<Type> GetAllCommandTypes()
    {
        EnsureInitialized();
        return _commandTypes.Values;
    }

    // =========================================================================
    // EVENT METHODS
    // =========================================================================

    public static bool IsKnownEventType(string eventTypeName)
    {
        EnsureInitialized();
        return _eventTypes.ContainsKey(eventTypeName);
    }

    public static Type? GetEventType(string eventTypeName)
    {
        EnsureInitialized();
        return _eventTypes.TryGetValue(eventTypeName, out var type) ? type : null;
    }

    public static IEnumerable<string> GetAllEventTypeNames()
    {
        EnsureInitialized();
        return _eventTypes.Keys;
    }

    // =========================================================================
    // COMMAND METHODS
    // =========================================================================

    public static bool IsKnownCommandType(string commandTypeName)
    {
        EnsureInitialized();
        return _commandTypes.ContainsKey(commandTypeName);
    }

    public static Type? GetCommandType(string commandTypeName)
    {
        EnsureInitialized();
        return _commandTypes.TryGetValue(commandTypeName, out var type) ? type : null;
    }

    public static IEnumerable<string> GetAllowedCommandNames(IEnumerable<string> eventTypeNames)
    {
        EnsureInitialized();
        var allowedCommands = new HashSet<string>();
        
        foreach (var eventTypeName in eventTypeNames)
        {
            if (_eventToCommands.TryGetValue(eventTypeName, out var commands))
            {
                allowedCommands.UnionWith(commands);
            }
        }
        
        return allowedCommands;
    }

    // =========================================================================
    // QUERY METHODS
    // =========================================================================

    public static bool IsKnownQueryType(string queryTypeName)
    {
        EnsureInitialized();
        return _queryTypes.ContainsKey(queryTypeName);
    }

    public static Type? GetQueryType(string queryTypeName)
    {
        EnsureInitialized();
        return _queryTypes.TryGetValue(queryTypeName, out var type) ? type : null;
    }

    public static IEnumerable<string> GetAllQueryTypeNames()
    {
        EnsureInitialized();
        return _queryTypes.Keys;
    }

    // =========================================================================
    // QUERY RESPONSE METHODS
    // =========================================================================

    public static bool IsKnownQueryResponseType(string responseTypeName)
    {
        EnsureInitialized();
        return _queryResponseTypes.ContainsKey(responseTypeName);
    }

    public static Type? GetQueryResponseType(string responseTypeName)
    {
        EnsureInitialized();
        return _queryResponseTypes.TryGetValue(responseTypeName, out var type) ? type : null;
    }

    // =========================================================================
    // TRIGGER METHODS
    // =========================================================================

    public static bool IsKnownTriggerType(string triggerTypeName)
    {
        EnsureInitialized();
        return _triggerTypes.ContainsKey(triggerTypeName);
    }

    public static Type? GetTriggerType(string triggerTypeName)
    {
        EnsureInitialized();
        return _triggerTypes.TryGetValue(triggerTypeName, out var type) ? type : null;
    }

    public static IEnumerable<string> GetAllTriggerTypeNames()
    {
        EnsureInitialized();
        return _triggerTypes.Keys;
    }
}