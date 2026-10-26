using System.Diagnostics;
using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Persistence;

/// <summary>
/// Prozessweite Profiling-Zähler des Group-Commit (nur Messung, vernachlässigbarer Overhead: ein
/// Stopwatch + Interlocked pro Commit). <see cref="Snapshot"/> liefert Commits/Events/echte Commit-Zeit.
/// </summary>
public static class BatchWriterStats
{
    private static long _batches;
    private static long _events;
    private static long _commitMicros;

    public static void Reset() { _batches = 0; _events = 0; _commitMicros = 0; }

    internal static void Record(int events, double commitMs)
    {
        Interlocked.Increment(ref _batches);
        Interlocked.Add(ref _events, events);
        Interlocked.Add(ref _commitMicros, (long)(commitMs * 1000));
    }

    public static (long Batches, long Events, double TotalCommitMs, double AvgBatch, double AvgCommitMs) Snapshot()
    {
        var b = Interlocked.Read(ref _batches);
        var e = Interlocked.Read(ref _events);
        var totalMs = Interlocked.Read(ref _commitMicros) / 1000.0;
        return (b, e, totalMs, b == 0 ? 0 : (double)e / b, b == 0 ? 0 : totalMs / b);
    }
}

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
/// - Atomarität: ein <c>SaveChanges</c> → alles-oder-nichts. Wirft es (z.B. OCC-Konflikt EINES Streams, eine Quittung, die die Inbox
///   schon kennt), rollt die ganze Transaktion zurück und die Exception propagiert → der Appender isoliert einzeln.
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
        await EventZeit.SicherAsync(_store, ct);   // den Zeitstempel setzt die Datenbank-Uhr (G-10)
        await using var session = _store.LightweightSession();

        var eventCount = 0;
        var beilagen = new BeilagenSchreiber();
        foreach (var item in batch)
        {
            Stage(session, item);
            eventCount += item.EventAnzahl;
            foreach (var teil in item.Teile) beilagen.Lege(session, item.StreamId, teil.Beilagen);
        }
        // Die Beilagen aller Appends in derselben Transaktion, die Quittungen in EINER Anweisung (Inbox, docs/04 §4.6). Kennt die Inbox eine
        //   schon, scheitert der ganze Batch am Schlüssel; der Appender isoliert dann einzeln, und nur der Wiederholte fällt.
        beilagen.Abschliessen(session, _store);

        var sw = Stopwatch.StartNew();
        await session.SaveChangesAsync(ct);
        sw.Stop();
        Infrastructure.Einblick.SpurSignal.Melde();
        BatchWriterStats.Record(eventCount, sw.Elapsed.TotalMilliseconds);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Group-Commit: {Count} Aggregat-Appends in einer Transaktion.", batch.Count);
    }

    /// <summary>
    /// Legt EINEN Append (alle Teile, ein Stream) in die Session (noch kein Commit) und stempelt die Metadaten PRO EVENT aus seinem Teil.
    /// Gleiche Versions-Arithmetik wie <see cref="MartenEventStore.AppendTeileAsync"/>: Version 0 → StartStream, sonst Ziel-Version.
    /// </summary>
    internal static void Stage(IDocumentSession session, in BatchAppend item)
    {
        var events = new object[item.EventAnzahl];
        var n = 0;
        foreach (var teil in item.Teile)
            foreach (var e in teil.Events) events[n++] = e;

        var action = item.ExpectedVersion == 0
            ? session.Events.StartStream(item.StreamId, events)
            : session.Events.Append(item.StreamId, item.ExpectedVersion + events.Length, events);

        // Per-Event-Metadaten auf die von Marten erzeugten Hüllen setzen (verifiziert: persistiert pro Event, überlebt den Readback in
        //   ReadStreamAsync) — Teil für Teil in der Reihenfolge der Events.
        var i = 0;
        foreach (var teil in item.Teile)
        {
            for (var k = 0; k < teil.Events.Count; k++, i++)
            {
                var ev = action.Events[i];
                if (teil.CausationId != null) ev.CausationId = teil.CausationId;
                if (teil.CorrelationId != null) ev.CorrelationId = teil.CorrelationId;
                if (!string.IsNullOrEmpty(item.AggregateType)) ev.SetHeader("aggregate_type", item.AggregateType);
                if (ImAuftrag.IstAkteur(teil.Akteur)) ev.SetHeader(ImAuftrag.Header, teil.Akteur!);
                SpurHeader.Setze(ev, teil.SpurKopf);
            }
        }
    }
}
