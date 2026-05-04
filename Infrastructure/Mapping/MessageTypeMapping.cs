using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;

namespace Infrastructure.Mapping;

/// <summary>
/// Zentrale Registry für alle Message-Typen.
/// 
/// ★ Schritt 4: Delegiert an GeneratedTypeRegistry — keine Reflection mehr!
///   Die Dictionaries werden beim ersten Zugriff aus dem generierten Registry befüllt.
///   API bleibt identisch — kein Umbau der Aufrufer nötig.
/// </summary>
public static class MessageTypeMapping
{
    private static readonly Dictionary<string, Type> _eventTypes = new();
    private static readonly Dictionary<string, Type> _commandTypes = new();
    private static readonly Dictionary<string, Type> _queryTypes = new();
    private static readonly Dictionary<string, Type> _queryResponseTypes = new();
    
    private static readonly Dictionary<string, HashSet<string>> _eventToCommands = new();
    
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialisiert die Registry.
    /// 
    /// ★ Schritt 4: params Assembly[] wird nur noch für API-Kompatibilität akzeptiert,
    ///   aber ignoriert — die Typen kommen aus GeneratedTypeRegistry.
    /// </summary>
    public static void Initialize(params System.Reflection.Assembly[] assemblies)
    {
        lock (_lock)
        {
            if (_initialized) return;

            // ★ Aus generiertem Registry befüllen — keine Reflection!
            foreach (var (name, type) in GeneratedTypeRegistry.Events)
                _eventTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.Commands)
                _commandTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.Queries)
                _queryTypes[name] = type;

            foreach (var (name, type) in GeneratedTypeRegistry.QueryResponses)
                _queryResponseTypes[name] = type;

            // ★ Event-to-Command Mapping aus GeneratedEventCommandMapping
            foreach (var (eventName, commandNames) in GeneratedEventCommandMapping.EventNameToCommandNames)
            {
                _eventToCommands[eventName] = commandNames.ToHashSet();
            }

            _initialized = true;
            
            Console.WriteLine($"[MessageTypeMapping] Initialized (from GeneratedTypeRegistry + GeneratedEventCommandMapping):");
            Console.WriteLine($"  Events: {_eventTypes.Count}");
            Console.WriteLine($"  Commands: {_commandTypes.Count}");
            Console.WriteLine($"  Queries: {_queryTypes.Count}");
            Console.WriteLine($"  QueryResponses: {_queryResponseTypes.Count}");
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
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
}