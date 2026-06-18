// REPO-PFAD: Infrastructure/GrpcClient/TriggerHandlerRegistry.cs  (NEU)
using System.Collections.Concurrent;
using Proto;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Registriert welche Client-Sessions welche Trigger-Typen verarbeiten.
///
/// 1:1-Mapping: Pro Trigger-Typ ein Handler (letzter Registrant gewinnt).
/// Semantik: Ein Trigger soll genau einmal verarbeitet werden.
/// Mehrere Clients mit demselben Trigger-Typ → der zuletzt verbundene gilt.
///
/// Thread-safe über ConcurrentDictionary.
/// Cleanup über UnregisterAll beim Disconnect.
/// </summary>
public class TriggerHandlerRegistry
{
    private readonly ConcurrentDictionary<string, PID> _handlers = new();

    /// <summary>
    /// Registriert einen Client-Actor als Handler für einen Trigger-Typ.
    /// Überschreibt einen bestehenden Handler für denselben Typ.
    /// </summary>
    public void Register(string triggerTypeName, PID handlerPid)
    {
        ArgumentNullException.ThrowIfNull(triggerTypeName);
        ArgumentNullException.ThrowIfNull(handlerPid);

        _handlers[triggerTypeName] = handlerPid;

        Console.WriteLine(
            $"[TriggerHandlerRegistry] Registered {triggerTypeName} → {handlerPid}");
    }

    /// <summary>
    /// Entfernt die Registrierung eines Trigger-Typs (nur wenn die PID übereinstimmt).
    /// </summary>
    public void Unregister(string triggerTypeName, PID handlerPid)
    {
        // Nur entfernen wenn die PID noch aktuell ist (kein versehentliches
        // Deregistrieren eines zwischenzeitlich registrierten neuen Handlers)
        _handlers.TryRemove(
            new KeyValuePair<string, PID>(triggerTypeName, handlerPid));
    }

    /// <summary>
    /// Gibt den Handler für einen Trigger-Typ zurück, oder null.
    /// </summary>
    public PID? GetHandler(string triggerTypeName)
    {
        return _handlers.TryGetValue(triggerTypeName, out var pid)
            ? pid
            : null;
    }

    /// <summary>
    /// Entfernt alle Registrierungen einer bestimmten PID.
    /// Wird beim Client-Disconnect aufgerufen.
    /// </summary>
    public void UnregisterAll(PID handlerPid)
    {
        var toRemove = _handlers
            .Where(kvp => kvp.Value.Equals(handlerPid))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _handlers.TryRemove(
                new KeyValuePair<string, PID>(key, handlerPid));
        }

        if (toRemove.Count > 0)
        {
            Console.WriteLine(
                $"[TriggerHandlerRegistry] Unregistered {toRemove.Count} triggers for {handlerPid}");
        }
    }

    /// <summary>
    /// Gibt alle registrierten Trigger-Typ-Namen zurück.
    /// Für Diagnostik und Monitoring.
    /// </summary>
    public IReadOnlyList<string> GetRegisteredTypes()
        => _handlers.Keys.ToList();
}
