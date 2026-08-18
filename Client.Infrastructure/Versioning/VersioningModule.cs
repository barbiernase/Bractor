using Client.Infrastructure.Abstractions;

namespace Client.Infrastructure.Versioning;

/// <summary>
/// Tracked Aggregate-Versionen — zwei Notionen, weil OCC und Read-Your-Writes verschiedene
/// Versionen brauchen:
///
///   • <b>OCC-Version</b> (<see cref="GetVersion"/>): der Stream-Head NACH dem Commit, inklusive
///     der co-committeten <c>KommandoVerarbeitet</c>-Marke (aus <see cref="MessageContext.StreamHeadVersion"/>).
///     Das ist die <c>ExpectedVersion</c>, die der nächste Command tragen muss.
///
///   • <b>Read-Ziel</b> (<see cref="GetReadTarget"/>): die höchste DOMAIN-Event-Version
///     (<see cref="MessageContext.AggregateVersion"/>). Die Marke materialisiert nichts, also
///     erreicht die Projektion (asynchroner Pull-Consumer) genau die Domain-Version — bis dorthin
///     muss ein Read aufgeschlossen haben, um „read your writes" zu erfüllen.
///
/// Quellen: Server-Events (MessageContext) und Query-Deps (nur OCC). Kein Reflection.
/// Thread-Safety: Writes kommen vom UI-Thread (Bus-Dispatch), Reads teils vom Query-Task
/// (QueryBridge prüft die Deps gegen das Read-Ziel) — daher ein Lock um beide Maps.
/// </summary>
public class VersioningModule : IVersioningModule
{
    private const int RecentWritesCap = 64;   // genug für eine Label-/Edit-Sitzung, hält das Set klein

    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _occVersions = new();   // Stream-Head (inkl. Marke) → ExpectedVersion
    private readonly Dictionary<Guid, int> _readTargets = new();   // Domain-Event-Version → Read-Your-Writes-Ziel
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

        // OCC: Stream-Head bevorzugen (zählt die Marke mit), sonst Event-Version.
        var occ = ctx.StreamHeadVersion > 0 ? ctx.StreamHeadVersion : ctx.AggregateVersion;

        lock (_gate)
        {
            if (occ > 0) Bump(_occVersions, ctx.AggregateId, occ);
            // Read-Ziel: IMMER die reine Domain-Event-Version (nie den Marker-Head).
            if (ctx.AggregateVersion > 0) Bump(_readTargets, ctx.AggregateId, ctx.AggregateVersion);
        }
    }

    // ═══════════════════════════════════════════════════
    // IVersioningModule
    // ═══════════════════════════════════════════════════

    public int? GetVersion(Guid aggregateId)
    {
        lock (_gate)
            return _occVersions.TryGetValue(aggregateId, out var v) ? v : null;
    }

    public int? GetReadTarget(Guid aggregateId)
    {
        lock (_gate)
            return _readTargets.TryGetValue(aggregateId, out var v) ? v : null;
    }

    public void TrackFromDeps(IEnumerable<AggregateDep> deps)
    {
        // BEWUSST KEIN OCC-Tracking aus Query-Deps: die Deps tragen die DOMAIN-Event-Version des
        // Read-Models, NICHT die marker-inklusive Stream-Head-Version. Als OCC-ExpectedVersion wäre
        // sie zu niedrig (es fehlen die co-committeten KommandoVerarbeitet-Marken) → der erste
        // Command auf einem nur per Query geladenen Aggregat schlüge an der OCC fehl ("erster Klick
        // verpufft"). Die OCC-Version kommt AUSSCHLIESSLICH aus Event-Pushes (StreamHeadVersion,
        // siehe TrackFromContext). Kennt der Client noch keine Head-Version, sendet ConnectionModule
        // den Command mit Sentinel -1 → der Server fährt Emittiert (kein OCC, Inbox-Dedup).
        // Die Deps werden weiterhin für die Read-Your-Writes-Prüfung der QueryBridge gebraucht
        // (dep.Version vs. GetReadTarget) — dafür ist hier aber nichts zu speichern.
        _ = deps;
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
            _occVersions.Clear();
            _readTargets.Clear();
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
    private static void Bump(Dictionary<Guid, int> map, Guid id, int version)
    {
        if (!map.TryGetValue(id, out var existing) || version > existing)
            map[id] = version;
    }
}
