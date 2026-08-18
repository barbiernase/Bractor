using Client.Infrastructure.Abstractions;

namespace Client.Infrastructure.Versioning;

/// <summary>
/// Tracked Aggregate-Versionen für Optimistic Concurrency.
///
/// Zwei Quellen:
///   1. Server-Events: MessageContext.AggregateVersion → höchste Version pro AggregateId
///   2. Query-Deps: QueryBridge ruft TrackFromDeps() mit den Deps aus der Response
///
/// Kein Reflection — subscribt via bus.Subscribe(Type, handler).
/// Thread-Safety: Nicht nötig — wird nur vom UI-Thread aufgerufen.
/// </summary>
public class VersioningModule : IVersioningModule
{
    private readonly Dictionary<Guid, int> _versions = new();
    private readonly List<IDisposable> _subscriptions = new();

    /// <summary>
    /// Aktiviert das Modul: subscribt sync auf alle Server-Event-Typen.
    /// serverEventTypes kommt aus GeneratedWiring.ServerEventTypes.
    /// </summary>
    public void Activate(IBus bus, IReadOnlyList<Type> serverEventTypes)
    {
        foreach (var eventType in serverEventTypes)
        {
            // Nicht-generisch: Handler bekommt object, braucht nur den Context
            var sub = bus.Subscribe(eventType, (_, ctx) => TrackFromContext(ctx));
            _subscriptions.Add(sub);
        }
    }

    private void TrackFromContext(MessageContext ctx)
    {
        // Für die OCC-ExpectedVersion zählt der Stream-Head NACH dem Commit (inkl. der
        // co-committeten KommandoVerarbeitet-Marke), nicht die Position des Domain-Events.
        // StreamHeadVersion trägt genau das; nur wenn sie ungesetzt ist (0, z. B. ältere Events
        // oder lokale Nachrichten) fällt es auf die Event-Version zurück.
        var version = ctx.StreamHeadVersion > 0 ? ctx.StreamHeadVersion : ctx.AggregateVersion;

        if (ctx.AggregateId != Guid.Empty && version > 0)
        {
            if (!_versions.TryGetValue(ctx.AggregateId, out var existing) ||
                version > existing)
            {
                _versions[ctx.AggregateId] = version;
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // IVersioningModule
    // ═══════════════════════════════════════════════════

    public int? GetVersion(Guid aggregateId)
        => _versions.TryGetValue(aggregateId, out var v) ? v : null;

    public void TrackFromDeps(IEnumerable<AggregateDep> deps)
    {
        foreach (var dep in deps)
        {
            if (!_versions.TryGetValue(dep.Id, out var existing) ||
                dep.Version > existing)
            {
                _versions[dep.Id] = dep.Version;
            }
        }
    }

    /// <summary>Entfernt alle gecachten Versionen. Nützlich bei Reconnect.</summary>
    public void Reset() => _versions.Clear();

    public void Deactivate()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
    }
}