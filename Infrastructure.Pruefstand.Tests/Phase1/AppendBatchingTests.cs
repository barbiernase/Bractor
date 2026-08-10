using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Beweist die Batch-Zustandsmaschine des <see cref="BatchingEventAppender"/> store-agnostisch
/// (in-memory, Fake-Writer + Fake-Inner), OHNE Marten/Postgres:
///   (1) Bündeln: mehrere gleichzeitige Appends landen in EINEM Group-Commit (Batch &gt; 1), und ALLE
///       werden durabel; die zurückgegebenen Tasks lösen erst nach dem Commit (Durabilitäts-Grenze).
///   (2) Isolation bei Batch-Fehler: wirft der Group-Commit, wiederholt der Appender jeden Auftrag
///       einzeln über den inneren Store → nichts geht verloren (append-only, kein Verwerfen).
///   (3) Isolation des Schuldigen: im Fehlerfall fällt NUR der Auftrag mit OCC-Konflikt; die
///       unschuldigen Mit-Gebündelten committen trotzdem.
/// </summary>
public class AppendBatchingTests
{
    private sealed record Evt(Guid AggregateId) : IEvent;

    // ── Fake-Inner: hält Versionen pro Stream, erzwingt OCC (wie der echte Store), zählt Einzel-Appends ──
    private sealed class RecordingInner : IEventStoreRepository
    {
        private readonly object _lock = new();
        public readonly Dictionary<Guid, int> Versions = new();
        public int EinzelAppends;

        public Task AppendEventsAsync(Guid id, int expV, IReadOnlyList<IEvent> events,
            string? c = null, string? ca = null, string? at = null)
        {
            lock (_lock)
            {
                EinzelAppends++;
                var cur = Versions.TryGetValue(id, out var v) ? v : 0;
                if (expV != cur)
                    throw new InvalidOperationException($"OCC-Konflikt: erwartet {expV}, aktuell {cur}");
                Versions[id] = cur + events.Count;
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid s, int f, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long a, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
            => throw new NotSupportedException();
    }

    // ── Fake-Writer: schreibt den ganzen Batch atomar in den Inner (Happy) ODER wirft (kein Teil-Write) ──
    private sealed class FakeWriter : IEventBatchWriter
    {
        private readonly RecordingInner _inner;
        private readonly bool _throwAlways;
        public readonly List<int> GeseheneBatchGrößen = new();

        public FakeWriter(RecordingInner inner, bool throwAlways)
        {
            _inner = inner;
            _throwAlways = throwAlways;
        }

        public async Task WriteBatchAsync(IReadOnlyList<BatchAppend> batch, CancellationToken ct)
        {
            GeseheneBatchGrößen.Add(batch.Count);
            if (_throwAlways)
                throw new InvalidOperationException("Group-Commit simuliert fehlgeschlagen");

            // Atomar: erst prüfen (würde irgendein Item scheitern → gar nichts schreiben, dann werfen).
            foreach (var b in batch)
            {
                var cur = _inner.Versions.TryGetValue(b.StreamId, out var v) ? v : 0;
                if (b.ExpectedVersion != cur)
                    throw new InvalidOperationException("Batch-OCC-Konflikt");
            }
            foreach (var b in batch)
                await _inner.AppendEventsAsync(b.StreamId, b.ExpectedVersion, b.Events, b.CorrelationId, b.CausationId, b.AggregateType);
        }
    }

    [Fact]
    public async Task Bündelt_mehrere_Appends_in_einen_Commit_und_alle_werden_durabel()
    {
        var inner = new RecordingInner();
        var writer = new FakeWriter(inner, throwAlways: false);
        // Linger-Fenster → deterministisch: alle im Fenster eingereihten Appends landen in EINEM Batch.
        await using var appender = new BatchingEventAppender(
            inner, writer, NullLogger<BatchingEventAppender>.Instance, maxBatch: 256, lingerMs: 120);

        const int n = 50;
        var streams = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToArray();
        var tasks = streams.Select(s =>
            appender.AppendEventsAsync(s, 0, new IEvent[] { new Evt(s) })).ToArray();

        await Task.WhenAll(tasks);

        inner.Versions.Count.Should().Be(n, "alle Appends müssen durabel sein");
        inner.Versions.Values.Should().OnlyContain(v => v == 1);
        writer.GeseheneBatchGrößen.Max().Should().BeGreaterThan(1, "es muss tatsächlich gebündelt worden sein");
    }

    [Fact]
    public async Task Isolation_bei_Batch_Fehler_schreibt_jeden_Auftrag_einzeln_nichts_geht_verloren()
    {
        var inner = new RecordingInner();
        var writer = new FakeWriter(inner, throwAlways: true); // Group-Commit scheitert IMMER
        await using var appender = new BatchingEventAppender(
            inner, writer, NullLogger<BatchingEventAppender>.Instance, maxBatch: 256, lingerMs: 120);

        const int n = 20;
        var streams = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToArray();
        var tasks = streams.Select(s =>
            appender.AppendEventsAsync(s, 0, new IEvent[] { new Evt(s) })).ToArray();

        await Task.WhenAll(tasks);

        inner.Versions.Count.Should().Be(n, "der Isolations-Retry muss jeden Auftrag einzeln durabel machen");
        inner.EinzelAppends.Should().Be(n, "genau ein Einzel-Append pro Auftrag");
    }

    [Fact]
    public async Task Isolation_isoliert_den_Schuldigen_Unschuldige_committen_trotzdem()
    {
        var inner = new RecordingInner();
        var writer = new FakeWriter(inner, throwAlways: true); // erzwingt den Isolationspfad
        await using var appender = new BatchingEventAppender(
            inner, writer, NullLogger<BatchingEventAppender>.Instance, maxBatch: 256, lingerMs: 120);

        var gut = Guid.NewGuid();
        var schuldig = Guid.NewGuid();

        // gut: neuer Stream, expectedVersion 0 (passt) — muss committen.
        var tGut = appender.AppendEventsAsync(gut, 0, new IEvent[] { new Evt(gut) });
        // schuldig: neuer Stream, aber expectedVersion 5 → OCC-Konflikt im Inner — muss allein fallen.
        var tSchuldig = appender.AppendEventsAsync(schuldig, 5, new IEvent[] { new Evt(schuldig) });

        await tGut; // committet
        var akt = async () => await tSchuldig;

        tGut.IsCompletedSuccessfully.Should().BeTrue();
        await akt.Should().ThrowAsync<InvalidOperationException>("nur der OCC-Konflikt-Auftrag darf fallen");
        inner.Versions.Should().ContainKey(gut).And.NotContainKey(schuldig);
        inner.Versions[gut].Should().Be(1);
    }
}
