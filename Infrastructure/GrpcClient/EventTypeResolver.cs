using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Infrastructure.Mapping;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Löst Event-Typ-Namen zu Type-Objekten auf.
/// Wird vom SubscriptionTracker und PubSubStartupService verwendet.
/// 
/// ★ Schritt 4: Delegiert an GeneratedTypeRegistry — keine Reflection mehr!
///   API bleibt identisch — kein Umbau der Aufrufer nötig.
/// </summary>
public static class EventTypeResolver
{
    private static readonly Dictionary<string, Type> _eventTypes = new();
    private static readonly Dictionary<string, Type> _commandTypes = new();
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialisiert den Resolver.
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

            _initialized = true;
            
            Console.WriteLine($"[EventTypeResolver] Initialized (from GeneratedTypeRegistry) with {_eventTypes.Count} events, {_commandTypes.Count} commands");
        }
    }

    /// <summary>
    /// Löst einen Event-Typ-Namen zu einem Type auf.
    /// "*" gibt null zurück (Wildcard für alle Typen).
    /// </summary>
    public static Type? ResolveEventType(string eventTypeName)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(eventTypeName) || eventTypeName == "*")
            return null;

        return _eventTypes.TryGetValue(eventTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Löst einen Command-Typ-Namen zu einem Type auf.
    /// </summary>
    public static Type? ResolveCommandType(string commandTypeName)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(commandTypeName))
            return null;

        return _commandTypes.TryGetValue(commandTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Gibt alle registrierten Event-Typen zurück.
    /// </summary>
    public static IEnumerable<Type> GetAllEventTypes()
    {
        EnsureInitialized();
        return _eventTypes.Values;
    }

    /// <summary>
    /// Gibt alle registrierten Command-Typen zurück.
    /// </summary>
    public static IEnumerable<Type> GetAllCommandTypes()
    {
        EnsureInitialized();
        return _commandTypes.Values;
    }

    /// <summary>
    /// Prüft ob ein Event-Typ registriert ist.
    /// </summary>
    public static bool IsKnownEventType(string eventTypeName)
    {
        EnsureInitialized();
        return eventTypeName == "*" || _eventTypes.ContainsKey(eventTypeName);
    }

    /// <summary>
    /// Parst eine AggregateId. "*" wird zu null (Wildcard).
    /// </summary>
    public static Guid? ParseAggregateId(string aggregateId)
    {
        if (string.IsNullOrEmpty(aggregateId) || aggregateId == "*")
            return null;

        return Guid.TryParse(aggregateId, out var guid) ? guid : null;
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}


/*using System.Reflection;
using Abstractions;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Löst Event-Typ-Namen zu Type-Objekten auf.
/// Wird vom OutboundChildActor verwendet, um Subscriptions zu verwalten.
/// </summary>
public static class EventTypeResolver
{
    private static readonly Dictionary<string, Type> _eventTypes = new();
    private static readonly Dictionary<string, Type> _commandTypes = new();
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialisiert den Resolver mit allen Event- und Command-Typen aus den gegebenen Assemblies.
    /// Wird automatisch aufgerufen wenn nötig.
    /// </summary>
    public static void Initialize(params Assembly[] assemblies)
    {
        lock (_lock)
        {
            if (_initialized) return;

            var assembliesToScan = assemblies.Length > 0 
                ? assemblies 
                : AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assembliesToScan)
            {
                try
                {
                    var types = assembly.GetTypes();
                    
                    foreach (var type in types)
                    {
                        if (type.IsInterface || type.IsAbstract)
                            continue;

                        if (typeof(IEvent).IsAssignableFrom(type))
                        {
                            _eventTypes[type.Name] = type;
                        }
                        
                        if (typeof(ICommand).IsAssignableFrom(type))
                        {
                            _commandTypes[type.Name] = type;
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Assembly konnte nicht geladen werden - ignorieren
                }
            }

            _initialized = true;
            
            Console.WriteLine($"[EventTypeResolver] Initialized with {_eventTypes.Count} events, {_commandTypes.Count} commands");
        }
    }

    /// <summary>
    /// Löst einen Event-Typ-Namen zu einem Type auf.
    /// "*" gibt null zurück (Wildcard für alle Typen).
    /// </summary>
    public static Type? ResolveEventType(string eventTypeName)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(eventTypeName) || eventTypeName == "*")
            return null;

        return _eventTypes.TryGetValue(eventTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Löst einen Command-Typ-Namen zu einem Type auf.
    /// </summary>
    public static Type? ResolveCommandType(string commandTypeName)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(commandTypeName))
            return null;

        return _commandTypes.TryGetValue(commandTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Gibt alle registrierten Event-Typen zurück.
    /// </summary>
    public static IEnumerable<Type> GetAllEventTypes()
    {
        EnsureInitialized();
        return _eventTypes.Values;
    }

    /// <summary>
    /// Gibt alle registrierten Command-Typen zurück.
    /// </summary>
    public static IEnumerable<Type> GetAllCommandTypes()
    {
        EnsureInitialized();
        return _commandTypes.Values;
    }

    /// <summary>
    /// Prüft ob ein Event-Typ registriert ist.
    /// </summary>
    public static bool IsKnownEventType(string eventTypeName)
    {
        EnsureInitialized();
        return eventTypeName == "*" || _eventTypes.ContainsKey(eventTypeName);
    }

    /// <summary>
    /// Parst eine AggregateId. "*" wird zu null (Wildcard).
    /// </summary>
    public static Guid? ParseAggregateId(string aggregateId)
    {
        if (string.IsNullOrEmpty(aggregateId) || aggregateId == "*")
            return null;

        return Guid.TryParse(aggregateId, out var guid) ? guid : null;
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}*/