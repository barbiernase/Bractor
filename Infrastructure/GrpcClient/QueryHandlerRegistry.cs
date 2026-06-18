// REPO-PFAD: Infrastructure/GrpcClient/QueryHandlerRegistry.cs  (NEU)
using System.Collections.Concurrent;
using Proto;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Registriert welche Client-Sessions welche Query-Typen beantworten.
///
/// 1:1-Mapping: Pro Query-Typ ein Handler.
/// Semantik: Eine Query erwartet genau eine Antwort von genau einem Handler.
///
/// Thread-safe über ConcurrentDictionary.
/// Cleanup über UnregisterAll beim Disconnect.
/// </summary>
public class QueryHandlerRegistry
{
    private readonly ConcurrentDictionary<string, PID> _handlers = new();

    /// <summary>
    /// Registriert einen Client-Actor als Handler für einen Query-Typ.
    /// Überschreibt einen bestehenden Handler für denselben Typ.
    /// </summary>
    public void Register(string queryTypeName, PID handlerPid)
    {
        ArgumentNullException.ThrowIfNull(queryTypeName);
        ArgumentNullException.ThrowIfNull(handlerPid);

        var previous = _handlers.TryGetValue(queryTypeName, out var old) ? old : null;
        _handlers[queryTypeName] = handlerPid;

        if (previous != null && !previous.Equals(handlerPid))
        {
            Console.WriteLine(
                $"[QueryHandlerRegistry] {queryTypeName}: replaced {previous} → {handlerPid}");
        }
        else
        {
            Console.WriteLine(
                $"[QueryHandlerRegistry] Registered {queryTypeName} → {handlerPid}");
        }
    }

    /// <summary>
    /// Entfernt die Registrierung eines Query-Typs (nur wenn die PID übereinstimmt).
    /// </summary>
    public void Unregister(string queryTypeName, PID handlerPid)
    {
        _handlers.TryRemove(
            new KeyValuePair<string, PID>(queryTypeName, handlerPid));
    }

    /// <summary>
    /// Gibt den Handler für einen Query-Typ zurück, oder null.
    /// </summary>
    public PID? GetHandler(string queryTypeName)
    {
        return _handlers.TryGetValue(queryTypeName, out var pid)
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
                $"[QueryHandlerRegistry] Unregistered {toRemove.Count} queries for {handlerPid}");
        }
    }

    /// <summary>
    /// Gibt alle registrierten Query-Typ-Namen zurück.
    /// Für Diagnostik und Monitoring.
    /// </summary>
    public IReadOnlyList<string> GetRegisteredTypes()
        => _handlers.Keys.ToList();
}
