using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;

namespace Infrastructure.Mapping;

/// <summary>
/// Statisches Mapping von Event-Typen zu erlaubten Command-Typen.
/// 
/// ★ Schritt 5: Delegiert an GeneratedEventCommandMapping — keine Domain-Imports mehr!
///   API bleibt identisch — kein Umbau der Aufrufer nötig.
/// </summary>
public static class EventCommandMapping
{
    private static readonly IReadOnlyDictionary<Type, Type[]> _eventToCommands 
        = GeneratedEventCommandMapping.EventToCommands;

    private static readonly IReadOnlyDictionary<string, string[]> _eventNameToCommandNames 
        = GeneratedEventCommandMapping.EventNameToCommandNames;

    private static readonly IReadOnlyDictionary<Type, Type[]> _commandToEvents
        = GeneratedEventCommandMapping.CommandToEvents;

    /// <summary>
    /// Gibt die erlaubten Command-Typen für eine Liste von Event-Typen zurück.
    /// </summary>
    public static IReadOnlySet<Type> GetAllowedCommands(IEnumerable<Type> eventTypes)
    {
        var result = new HashSet<Type>();
        
        foreach (var eventType in eventTypes)
        {
            if (_eventToCommands.TryGetValue(eventType, out var commands))
            {
                foreach (var cmd in commands)
                {
                    result.Add(cmd);
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Gibt die erlaubten Command-Namen für eine Liste von Event-Namen zurück.
    /// Für Proto-Kommunikation.
    /// </summary>
    public static IReadOnlySet<string> GetAllowedCommandNames(IEnumerable<string> eventTypeNames)
    {
        var result = new HashSet<string>();
        
        foreach (var eventName in eventTypeNames)
        {
            if (_eventNameToCommandNames.TryGetValue(eventName, out var commands))
            {
                foreach (var cmd in commands)
                {
                    result.Add(cmd);
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Prüft ob ein Command-Typ zu den erlaubten Commands gehört.
    /// </summary>
    public static bool IsCommandAllowed(Type commandType, IReadOnlySet<Type> allowedCommands)
    {
        return allowedCommands.Contains(commandType);
    }

    /// <summary>
    /// Prüft ob ein Command-Name zu den erlaubten Commands gehört.
    /// </summary>
    public static bool IsCommandAllowed(string commandName, IReadOnlySet<string> allowedCommandNames)
    {
        return allowedCommandNames.Contains(commandName);
    }

    /// <summary>
    /// Gibt alle bekannten Event-Typen zurück.
    /// </summary>
    public static IEnumerable<Type> GetAllEventTypes() => _eventToCommands.Keys;

    /// <summary>
    /// Gibt alle bekannten Event-Namen zurück.
    /// </summary>
    public static IEnumerable<string> GetAllEventTypeNames() => _eventNameToCommandNames.Keys;

    /// <summary>
    /// Prüft ob ein Event-Name bekannt ist.
    /// </summary>
    public static bool IsKnownEventType(string eventTypeName)
    {
        return _eventNameToCommandNames.ContainsKey(eventTypeName);
    }
}