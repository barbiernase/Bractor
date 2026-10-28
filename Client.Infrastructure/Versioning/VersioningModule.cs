using Client.Infrastructure.Abstractions;

namespace Client.Infrastructure.Versioning;

/// <summary>
/// Die Version je Aggregat, wie der Client sie kennt — EINE Version (Plan Pub/Sub-Neustrukturierung Phase 5): ohne Marken im Log ist die Version
/// eines Streams die Zahl seiner Events. Sie ist die <c>ExpectedVersion</c> des nächsten Commands (OCC) und das Ziel, bis zu dem eine Antwort
/// aufgeschlossen haben muss (Lesen nach Schreiben, <c>QueryBridge</c>).
///
/// Quellen, monoton (nie senken): Event-Pushes (der Kopf nach dem Commit, <see cref="MessageContext.StreamHeadVersion"/>, sonst die Version
/// des Events) und Antworten (die Versionen der gezeigten Aggregate, <see cref="TrackFromDeps"/>) — so prüft der Server auch einen Command auf
/// ein Aggregat, das der Client nur aus einer Antwort kennt, gegen dessen Stand. Kein Reflection.
/// Thread-Safety: Writes kommen vom UI-Thread (Bus-Dispatch), Reads teils vom Query-Task — daher ein Lock.
/// </summary>
public class VersioningModule : IVersioningModule
{
    private const int RecentWritesCap = 64;   // genug für eine Label-/Edit-Sitzung, hält das Set klein

    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _versionen = new();
    private readonly LinkedList<Guid> _recentWrites = new();       // MRU-Ordnung: neueste hinten, Cap RecentWritesCap
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
        if (ctx.AggregateId == Guid.Empty) return;
        // Der Kopf nach dem Commit, wenn gesetzt: schrieb der Commit nach diesem Event weitere, die der Client nicht hört, zählen sie mit.
        var version = Math.Max(ctx.StreamHeadVersion, ctx.AggregateVersion);
        if (version <= 0) return;
        lock (_gate) Bump(ctx.AggregateId, version);
    }

    // ═══════════════════════════════════════════════════
    // IVersioningModule
    // ═══════════════════════════════════════════════════

    public int? GetVersion(Guid aggregateId)
    {
        lock (_gate)
            return _versionen.TryGetValue(aggregateId, out var v) ? v : null;
    }

    public void TrackFromDeps(IEnumerable<AggregateDep> deps)
    {
        lock (_gate)
            foreach (var dep in deps)
                if (dep.Id != Guid.Empty && dep.Version > 0) Bump(dep.Id, dep.Version);
    }

    public void MarkWritten(Guid aggregateId)
    {
        if (aggregateId == Guid.Empty) return;
        lock (_gate)
        {
            _recentWrites.Remove(aggregateId);      // Dedup: falls schon vorhanden, ans Ende (MRU)
            _recentWrites.AddLast(aggregateId);
            while (_recentWrites.Count > RecentWritesCap)
                _recentWrites.RemoveFirst();        // ältestes fällt heraus
        }
    }

    public IReadOnlyList<string> RecentWrites
    {
        get
        {
            lock (_gate)
                return _recentWrites.Select(id => id.ToString()).ToList();
        }
    }

    /// <summary>Entfernt alle gecachten Versionen. Nützlich bei Reconnect.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _versionen.Clear();
            _recentWrites.Clear();
        }
    }

    public void Deactivate()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
    }

    // Monoton erhöhen (nie senken) — der Aufrufer hält das Lock.
    private void Bump(Guid id, int version)
    {
        if (!_versionen.TryGetValue(id, out var existing) || version > existing)
            _versionen[id] = version;
    }
}
