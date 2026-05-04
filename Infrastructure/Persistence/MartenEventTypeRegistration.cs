using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Infrastructure.Mapping;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Registriert Domain-Event-Typen bei Marten mit snake_case Mapping.
/// 
/// ★ Schritt 4: Nutzt GeneratedTypeRegistry.PersistableEvents — keine Reflection mehr!
///   Snake_case-Namen sind vom Generator vorberechnet.
///   API bleibt identisch — kein Umbau der Aufrufer nötig.
/// 
/// Regeln:
/// - Nur konkrete Klassen die IEvent implementieren
/// - ITransientEvent-Typen werden AUSGESCHLOSSEN (z.B. CommandFailed)
/// - Event-Name wird per snake_case gemappt (vom Generator vorberechnet)
///   → "LagerartikelErstellt" wird zu "lagerartikel_erstellt"
///   → "NameGeändert" wird zu "name_geaendert" (Umlaut-Handling)
/// </summary>
public static class MartenEventTypeRegistration
{
    /// <summary>
    /// Registriert alle persistierbaren Event-Typen bei Marten.
    /// ★ Nutzt GeneratedTypeRegistry — keine Assembly-Scans!
    /// </summary>
    public static void RegisterEventTypes(StoreOptions options)
    {
        foreach (var (eventType, snakeCaseName) in GeneratedTypeRegistry.PersistableEvents)
        {
            options.Events.MapEventType(eventType, snakeCaseName);
        }

        Console.WriteLine($"[Marten] {GeneratedTypeRegistry.PersistableEvents.Count} Event-Typen registriert (from GeneratedTypeRegistry):");
        foreach (var (eventType, snakeCaseName) in GeneratedTypeRegistry.PersistableEvents)
        {
            Console.WriteLine($"  → {eventType.Name} → {snakeCaseName}");
        }
    }

    /// <summary>
    /// Gibt alle persistierbaren Event-Typen zurück.
    /// ★ Delegiert an GeneratedTypeRegistry — keine Reflection!
    /// </summary>
    public static IReadOnlyList<Type> DiscoverEventTypes()
    {
        return GeneratedTypeRegistry.PersistableEventTypes;
    }
}


/*using System.Reflection;
using Abstractions;
using Core.SourceGeneration;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Persistence;

/// <summary>
/// Registriert Domain-Event-Typen bei Marten mit snake_case Mapping.
/// 
/// Regeln:
/// - Nur konkrete Klassen die IEvent implementieren
/// - ITransientEvent-Typen werden AUSGESCHLOSSEN (z.B. CommandFailed)
/// - Event-Name wird per NameSanitizer.ToSnakeCase() gemappt
///   → "LagerartikelErstellt" wird zu "lagerartikel_erstellt"
///   → "NameGeändert" wird zu "name_geaendert" (Umlaut-Handling)
/// 
/// Die snake_case-Konvention stellt sicher:
/// - Lesbare Event-Typen in PostgreSQL (SELECT type FROM es.mt_events)
/// - Stabile Deserialisierung auch nach Refactoring (Name bleibt gleich)
/// - Keine .NET-Namespace-Abhängigkeit in der Datenbank
/// </summary>
public static class MartenEventTypeRegistration
{
    /// <summary>
    /// Scannt alle geladenen Assemblies nach IEvent-Typen und registriert sie bei Marten.
    /// </summary>
    public static void RegisterEventTypes(Marten.StoreOptions options)
    {
        var eventTypes = DiscoverEventTypes();
        
        foreach (var eventType in eventTypes)
        {
            var snakeCaseName = NameSanitizer.ToSnakeCase(eventType.Name);
            options.Events.MapEventType(eventType, snakeCaseName);
        }

        Console.WriteLine($"[Marten] {eventTypes.Count} Event-Typen registriert:");
        foreach (var eventType in eventTypes)
        {
            Console.WriteLine($"  → {eventType.Name} → {NameSanitizer.ToSnakeCase(eventType.Name)}");
        }
    }

    /// <summary>
    /// Findet alle konkreten IEvent-Typen, die keine ITransientEvent sind.
    /// </summary>
    public static IReadOnlyList<Type> DiscoverEventTypes()
    {
        var eventTypes = new List<Type>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsInterface || type.IsAbstract)
                        continue;

                    // Muss IEvent sein
                    if (!typeof(IEvent).IsAssignableFrom(type))
                        continue;

                    // Darf NICHT ITransientEvent sein
                    if (typeof(ITransientEvent).IsAssignableFrom(type))
                        continue;

                    eventTypes.Add(type);
                }
            }
            
            catch (ReflectionTypeLoadException)
            {
                // Assembly konnte nicht geladen werden – ignorieren
            }
        }

        return eventTypes;
    }
}*/