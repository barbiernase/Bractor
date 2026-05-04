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
        if (ctx.AggregateId != Guid.Empty && ctx.AggregateVersion > 0)
        {
            if (!_versions.TryGetValue(ctx.AggregateId, out var existing) ||
                ctx.AggregateVersion > existing)
            {
                _versions[ctx.AggregateId] = ctx.AggregateVersion;
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