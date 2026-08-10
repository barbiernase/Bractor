using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Core;
using FluentAssertions;
using Infrastructure.Projections;
using Xunit;

namespace Infrastructure.Pruefstand.Phase4;

/// <summary>
/// Feature: der Projektions-Rebuild-Runner. Bewiesen store-frei, dass „Replay = Adapter ab Marke -1"
/// gilt: leeren → ResetAll → alle Streams ab 0 re-dispatchen baut das (append-artige) Read-Model KORREKT
/// neu auf — NICHT verdoppelt (das Leeren trägt), Marken werden neu fortgeschrieben, und ein zweiter
/// Rebuild ist idempotent. Gegenprobe: ohne das Leeren würde ein append-artiges Ziel verdoppeln.
/// </summary>
public class ProjectionRebuilderTests
{
    private const string ProjId = "test-projektion";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed record DummyEvent(Guid AggregateId) : IEvent;

    private sealed class FakeEventStore : IEventStoreRepository
    {
        private readonly List<EventEnvelope> _events;
        public FakeEventStore(List<EventEnvelope> events) => _events = events;

        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EventEnvelope>>(
                _events.Where(e => e.AggregateId == streamId && e.AggregateVersion >= fromVersion)
                       .OrderBy(e => e.AggregateVersion).ToList());

        public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct)
            => Task.FromResult(new StreamChanges(
                _events.Select(e => e.AggregateId).Distinct().ToList(), _events.Count));

        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
            => throw new NotSupportedException();
    }

    // Append-artiges Ziel + Tracker + Clearable in einem (wie die realen Co-Commit-Stores).
    private sealed class InMemoryAppendTarget : IProjectionTracker, IRebuildableProjection
    {
        public readonly List<string> Eintraege = new();
        private readonly Dictionary<Guid, int> _marks = new();

        public void Append(Guid stream, int version) => Eintraege.Add($"{stream}:{version}");

        public Task<int> LastProcessedVersionAsync(string p, Guid s, CancellationToken ct)
            => Task.FromResult(_marks.TryGetValue(s, out var v) ? v : -1);
        public Task MarkProcessedAsync(string p, Guid s, int version, CancellationToken ct)
        { _marks[s] = version; return Task.CompletedTask; }
        public Task ResetAsync(string p, Guid s, CancellationToken ct) { _marks.Remove(s); return Task.CompletedTask; }
        public Task ResetAllAsync(string p, CancellationToken ct) { _marks.Clear(); return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken ct) { Eintraege.Clear(); return Task.CompletedTask; }
        public Task ClearAsync(Guid streamId, CancellationToken ct)
        { Eintraege.RemoveAll(e => e.StartsWith(streamId + ":")); return Task.CompletedTask; }
    }

    private static EventEnvelope Evt(Guid stream, int version)
        => new() { AggregateId = stream, AggregateVersion = version, Payload = new DummyEvent(stream) };

    [Fact]
    public async Task Rebuild_baut_das_Read_Model_korrekt_neu_auf_ohne_zu_verdoppeln()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(s1, 1), Evt(s1, 2), Evt(s2, 1) };
        var store = new FakeEventStore(log);
        var target = new InMemoryAppendTarget();
        Func<EventEnvelope, ProjectionWriter, Task> dispatch =
            (e, _) => { target.Append(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; };

        // Initiale Materialisierung über den normalen Adapter-Pfad.
        var adapter = new ProjectionAdapter(store, target, null, ProjId, dispatch);
        await adapter.WakeAsync(s1, Ct);
        await adapter.WakeAsync(s2, Ct);
        target.Eintraege.Should().HaveCount(3);

        // Rebuild: leeren + ResetAll + ab 0 re-dispatchen → wieder GENAU 3 (nicht 6).
        var rebuilder = new ProjectionRebuilder(store);
        await rebuilder.RebuildAsync(ProjId, target, target, dispatch, Ct);

        target.Eintraege.Should().HaveCount(3);
        target.Eintraege.Should().BeEquivalentTo(new[] { $"{s1}:1", $"{s1}:2", $"{s2}:1" });

        // Idempotent: nochmal rebuilden → weiterhin 3.
        await rebuilder.RebuildAsync(ProjId, target, target, dispatch, Ct);
        target.Eintraege.Should().HaveCount(3);
    }

    [Fact]
    public async Task Gegenprobe_ohne_Leeren_wuerde_ein_append_Ziel_verdoppeln()
    {
        var s1 = Guid.NewGuid();
        var store = new FakeEventStore(new List<EventEnvelope> { Evt(s1, 1), Evt(s1, 2) });
        var target = new InMemoryAppendTarget();
        Func<EventEnvelope, ProjectionWriter, Task> dispatch =
            (e, _) => { target.Append(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; };

        // Materialisieren, dann NUR die Marken zurücksetzen (ohne Clear) und erneut ab 0 dispatchen.
        var adapter = new ProjectionAdapter(store, target, null, ProjId, dispatch);
        await adapter.WakeAsync(s1, Ct);
        target.Eintraege.Should().HaveCount(2);

        await target.ResetAllAsync(ProjId, Ct);          // Reset OHNE Clear
        await adapter.WakeAsync(s1, Ct);
        target.Eintraege.Should().HaveCount(4);           // verdoppelt → deshalb IST Clear nötig
    }

    [Fact]
    public async Task RebuildStream_baut_nur_einen_Stream_neu()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var store = new FakeEventStore(new List<EventEnvelope> { Evt(s1, 1), Evt(s2, 1), Evt(s2, 2) });
        var target = new InMemoryAppendTarget();
        Func<EventEnvelope, ProjectionWriter, Task> dispatch =
            (e, _) => { target.Append(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; };

        var adapter = new ProjectionAdapter(store, target, null, ProjId, dispatch);
        await adapter.WakeAsync(s1, Ct);
        await adapter.WakeAsync(s2, Ct);
        target.Eintraege.Should().HaveCount(3);

        var rebuilder = new ProjectionRebuilder(store);
        await rebuilder.RebuildStreamAsync(ProjId, s2, target, target, dispatch, Ct);

        // s1 unangetastet (1 Eintrag), s2 neu aufgebaut (2 Einträge) → 3.
        target.Eintraege.Should().HaveCount(3);
        target.Eintraege.Count(e => e.StartsWith(s2 + ":")).Should().Be(2);
        target.Eintraege.Count(e => e.StartsWith(s1 + ":")).Should().Be(1);
    }
}
