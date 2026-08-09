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
/// P4.2 — der Achse-B-Schnitt in der Konsumenten-Maschine. Bewiesen wird das Emittenten-Profil des
/// <see cref="ProjectionAdapter"/> (best-effort <see cref="IEmittentenCursor"/> statt co-committem Tracker):
///   (1) Signal-Weckung faltet ab dem Cursor → beim zweiten Signal wird NICHT erneut ab 0 emittiert,
///       nur der neue Tail; der Cursor rückt vor.
///   (2) Poll-Weckung (<c>VomPoll: true</c>) heilt bewusst AB 0 → alle Events erneut (at-least-once,
///       ein verlorener detached-Emit wird so re-emittiert; der Empfänger dedupliziert).
/// Ein replaybarer Tracker UND ein Emittenten-Cursor zugleich ist ein Konstruktionsfehler → Guard.
/// </summary>
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

        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
            => throw new NotSupportedException();
    }

    private sealed record DummyEvent(Guid AggregateId) : IEvent;

    private static EventEnvelope Evt(Guid stream, int version)
        => new() { AggregateId = stream, AggregateVersion = version, Payload = new DummyEvent(stream) };

    [Fact]
    public async Task Signal_faltet_ab_Cursor_Poll_heilt_ab_0()
    {
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1), Evt(stream, 2), Evt(stream, 3) };
        var store = new FakeEventStore(log);
        var cursor = new InMemoryEmittentenCursor();

        var emittiert = new List<int>();
        Func<EventEnvelope, ProjectionWriter, Task> dispatch = (e, _) =>
        {
            emittiert.Add(e.AggregateVersion);
            return Task.CompletedTask;
        };

        var adapter = new ProjectionAdapter(store, tracker: null, emittentenCursor: cursor, "reaktion", dispatch);

        // (1) erste Signal-Weckung: alle drei emittiert, Cursor rückt auf 3+1 vor
        await adapter.WakeAsync(stream, vomPoll: false, Ct);
        emittiert.Should().Equal(1, 2, 3);
        (await cursor.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(4);

        // Tail wächst
        log.Add(Evt(stream, 4));

        // (2) zweite Signal-Weckung: NUR der neue Tail (kein Re-Emit ab 0)
        emittiert.Clear();
        await adapter.WakeAsync(stream, vomPoll: false, Ct);
        emittiert.Should().Equal(4);

        // (3) Poll-Weckung heilt bewusst ab 0: alle vier erneut
        emittiert.Clear();
        await adapter.WakeAsync(stream, vomPoll: true, Ct);
        emittiert.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Tracker_und_Cursor_zugleich_ist_ein_Konstruktionsfehler()
    {
        var store = new FakeEventStore(new List<EventEnvelope>());
        Func<EventEnvelope, ProjectionWriter, Task> dispatch = (_, _) => Task.CompletedTask;

        var akt = () => new ProjectionAdapter(
            store,
            tracker: new InMemoryProjectionTrackerStub(),
            emittentenCursor: new InMemoryEmittentenCursor(),
            "konsument", dispatch);

        akt.Should().Throw<InvalidOperationException>();
    }

    // Minimaler IProjectionTracker-Stub nur für den Guard-Test (die Methoden werden nie aufgerufen).
    private sealed class InMemoryProjectionTrackerStub : IProjectionTracker
    {
        public Task<int> LastProcessedVersionAsync(string p, Guid s, CancellationToken ct) => Task.FromResult(-1);
        public Task MarkProcessedAsync(string p, Guid s, int v, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAsync(string p, Guid s, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAllAsync(string p, CancellationToken ct) => Task.CompletedTask;
    }
}
