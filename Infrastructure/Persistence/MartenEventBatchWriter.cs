using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Persistence;

/// <summary>
/// Bündelt N Aggregat-Appends in EINE Marten-Session → EIN <c>SaveChangesAsync</c> = eine Postgres-
/// Transaktion (Group-Commit). Amortisiert den gemessenen Hotspot: die Postgres-CPU pro Command
/// (BEGIN/COMMIT, WAL-Record, mt_streams-Touch, Index) fällt einmal pro Batch statt pro Command.
///
/// Korrektheit:
/// - OCC pro Stream bleibt: jeder Append trägt seine eigene Zielversion (<c>expectedVersion + count</c>),
///   bzw. StartStream für neue Streams — exakt wie <see cref="MartenEventStore"/>.
/// - Metadaten PRO EVENT (nicht session-weit): Correlation/Causation/aggregate_type werden auf den von
///   Marten zurückgegebenen Event-Hüllen gesetzt. Damit dürfen im selben Batch Commands verschiedener
///   Korrelation/Kausalität stehen — Voraussetzung fürs Bündeln überhaupt. (Session-weite Metadaten
///   könnten das nicht, weil Causation = CommandId je Command verschieden ist.)
/// - Atomarität: ein <c>SaveChanges</c> → alles-oder-nichts. Wirft es (z.B. OCC-Konflikt EINES Streams),
///   rollt die ganze Transaktion zurück und die Exception propagiert → der Appender isoliert einzeln.
/// </summary>
public sealed class MartenEventBatchWriter : IEventBatchWriter
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenEventBatchWriter> _logger;

    public MartenEventBatchWriter(IDocumentStore store, ILogger<MartenEventBatchWriter> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteBatchAsync(IReadOnlyList<BatchAppend> batch, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        foreach (var item in batch)
            Stage(session, item);

        await session.SaveChangesAsync(ct);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Group-Commit: {Count} Aggregat-Appends in einer Transaktion.", batch.Count);
    }

    /// <summary>
    /// Legt EINEN Append in die Session (noch kein Commit) und stempelt die Metadaten PRO EVENT.
    /// Gleiche Versions-Arithmetik wie <see cref="MartenEventStore.AppendEventsAsync"/>.
    /// </summary>
    internal static void Stage(IDocumentSession session, in BatchAppend item)
    {
        var events = new object[item.Events.Count];
        for (var i = 0; i < item.Events.Count; i++) events[i] = item.Events[i];

        var action = item.ExpectedVersion == 0
            ? session.Events.StartStream(item.StreamId, events)
            : session.Events.Append(item.StreamId, item.ExpectedVersion + events.Length, events);

        // Per-Event-Metadaten auf die von Marten erzeugten Hüllen setzen (verifiziert: persistiert
        // pro Event, überlebt den Readback in ReadStreamAsync).
        foreach (var ev in action.Events)
        {
            if (item.CausationId != null) ev.CausationId = item.CausationId;
            if (item.CorrelationId != null) ev.CorrelationId = item.CorrelationId;
            if (!string.IsNullOrEmpty(item.AggregateType)) ev.SetHeader("aggregate_type", item.AggregateType);
        }
    }
}
