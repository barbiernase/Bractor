using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Core;
using FluentAssertions;
using Infrastructure.Projections;
using Infrastructure.Testing;
using Xunit;

namespace Infrastructure.Pruefstand.Phase4;

/// <summary>
/// P4.2 — der Emittenten-Pfad der Konsumenten-Maschine. Bewiesen wird das Profil des <see cref="EmittentenAdapter"/>
/// (<see cref="IEmittentenCursor"/>, kein Tracker, kein Reset): jede Weckung — Signal wie Poll — faltet ab dem Cursor; es wird nie erneut ab 0
/// emittiert, nur der neue Tail; der Cursor rückt nach bestätigtem Emit vor (G-2). Projektionen liegen nie auf diesem Pfad (Batch-Konsum).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class EmittentenPfadAdapterTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ── minimaler Fake-Event-Store: nur ReadStreamAsync trägt, der Rest ist im Test nicht berührt ──
    private sealed class FakeEventStore : IEventStoreRepository
    {
        private readonly List<EventEnvelope> _events;
        public FakeEventStore(List<EventEnvelope> events) => _events = events;

        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EventEnvelope>>(
                _events.Where(e => e.AggregateVersion >= fromVersion).OrderBy(e => e.AggregateVersion).ToList());

        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null, string? ak = null, Quittung? q = null)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
            => throw new NotSupportedException();
    }

    private sealed record DummyEvent(Guid AggregateId) : IEvent;

    private static EventEnvelope Evt(Guid stream, int version)
        => new() { AggregateId = stream, AggregateVersion = version, Payload = new DummyEvent(stream) };

    [Fact]
    public async Task Jede_Weckung_faltet_ab_dem_Cursor()
    {
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1), Evt(stream, 2), Evt(stream, 3) };
        var store = new FakeEventStore(log);
        var cursor = new InMemoryEmittentenCursor();

        var emittiert = new List<int>();
        Func<EventEnvelope, ProjectionWriter, Infrastructure.PubSub.EmitAusgaenge, Task> dispatch = (e, _, _) =>
        {
            emittiert.Add(e.AggregateVersion);
            return Task.CompletedTask;
        };

        var adapter = new EmittentenAdapter(store, cursor, "reaktion", dispatch);

        // (1) erste Weckung: alle drei emittiert, Cursor rückt auf 3+1 vor
        await adapter.WakeAsync(stream, Ct);
        emittiert.Should().Equal(1, 2, 3);
        (await cursor.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(4);

        // Tail wächst
        log.Add(Evt(stream, 4));

        // (2) zweite Weckung: NUR der neue Tail (kein Re-Emit ab 0)
        emittiert.Clear();
        await adapter.WakeAsync(stream, Ct);
        emittiert.Should().Equal(4);

        // (3) eine weitere Weckung (etwa vom Poll) ohne neuen Tail: nichts erneut
        emittiert.Clear();
        await adapter.WakeAsync(stream, Ct);
        emittiert.Should().BeEmpty();
    }
}
