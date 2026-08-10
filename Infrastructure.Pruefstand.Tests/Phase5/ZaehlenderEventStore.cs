using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Aggregate;   // EventEnvelopeFactory, AggregateRehydrator

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// In-memory <see cref="IEventStoreRepository"/> mit READ-ZÄHLER (P5(b), Handoff §6): faithful genug, um den
/// echten <see cref="Infrastructure.Prozess.ProzessManager"/> zu treiben (Append mit OCC + Version-pro-Event
/// + Metadaten, Read ab <c>fromVersion</c>, globaler Änderungs-Scan). <see cref="GeleseneEvents"/> misst die
/// eigentliche O(N²)-Größe (Summe gelesener Event-Hüllen) — der Beleg, dass der Cursor sie auf O(N) senkt.
/// </summary>
public sealed class ZaehlenderEventStore : IEventStoreRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, List<EventEnvelope>> _streams = new();
    private readonly Dictionary<Guid, long> _letzteSeq = new();
    private long _seq;
    private readonly IAggregateHandlerFactory _factory;

    public long LeseAufrufe;      // Anzahl ReadStreamAsync-Aufrufe
    public long GeleseneEvents;   // Summe der zurückgegebenen Event-Hüllen (das O(N²)-Maß)

    public ZaehlenderEventStore(IAggregateHandlerFactory factory) => _factory = factory;

    /// <summary>Setzt die Zähler zurück (z.B. nach dem Aufbau, um nur die Treibe-Phase zu messen).</summary>
    public void ResetZaehler() { LeseAufrufe = 0; GeleseneEvents = 0; }

    public Task AppendEventsAsync(
        Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events,
        string? correlationId = null, string? causationId = null, string? aggregateType = null)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(aggregateId, out var list)) { list = new List<EventEnvelope>(); _streams[aggregateId] = list; }
            if (list.Count != expectedVersion)
                throw new ConcurrencyException($"expected {expectedVersion}, actual {list.Count} (stream {aggregateId})");

            var envs = EventEnvelopeFactory.BuildPerEvent(
                events, aggregateId, aggregateType ?? "", baseVersion: list.Count,
                correlationId ?? Guid.NewGuid().ToString(), causationId ?? Guid.NewGuid().ToString(), "system");
            list.AddRange(envs);
            _letzteSeq[aggregateId] = ++_seq;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
    {
        lock (_lock)
        {
            LeseAufrufe++;
            if (!_streams.TryGetValue(streamId, out var list))
                return Task.FromResult<IReadOnlyList<EventEnvelope>>(Array.Empty<EventEnvelope>());
            var res = list.Where(e => e.AggregateVersion >= fromVersion).OrderBy(e => e.AggregateVersion).ToList();
            GeleseneEvents += res.Count;
            return Task.FromResult<IReadOnlyList<EventEnvelope>>(res);
        }
    }

    public Task<StreamChanges> ReadChangedStreamsAsync(long afterGlobalSequence, CancellationToken ct)
    {
        lock (_lock)
        {
            var ids = _letzteSeq.Where(kv => kv.Value > afterGlobalSequence).Select(kv => kv.Key).ToList();
            return Task.FromResult(new StreamChanges(ids, _seq));
        }
    }

    public async Task<TState?> LoadStateAsync<TState>(Guid aggregateId) where TState : class, IState, new()
    {
        lock (_lock) { if (!_streams.ContainsKey(aggregateId)) return null; }
        var reh = await AggregateRehydrator.LoadAsync<TState>(
            aggregateId, snapshots: null, this, _factory, expectedSchemaVersion: null, CancellationToken.None);
        return reh.State;
    }
}
