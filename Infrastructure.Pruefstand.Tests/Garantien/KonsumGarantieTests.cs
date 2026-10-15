using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Core;
using Domain.Sammelvorgang;
using FluentAssertions;
using Infrastructure.Projections;
using Infrastructure.PubSub;
using Infrastructure.Testing;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Garantien der Konsum-Schleife (docs/11 §11.4–§11.6) unter Störung, store-frei über die Seams des <see cref="ProjectionAdapter"/>: doppelte
/// und ungeordnete Weckungen, Absturz zwischen Emit und Cursor, ein Emit, der nicht ankommt (G-2), parallele Emits eines Tails. Der Lese-Seam liefert den Log so, wie Marten
/// ihn liefert (geordnet ab Version); Marke und Cursor sind Fakes — ob Effekt und Marke gemeinsam gültig werden, prüft die Integration
/// (<c>CoCommitPostgresTests</c>).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class KonsumGarantieTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class LeseSeam(List<EventEnvelope> log) : IEventStoreRepository
    {
        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
        {
            lock (log) return Task.FromResult<IReadOnlyList<EventEnvelope>>(log.Where(e => e.AggregateVersion >= fromVersion).OrderBy(e => e.AggregateVersion).ToList());
        }
        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null, string? ak = null)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct) => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => throw new NotSupportedException();
    }

    /// <summary>Eine Marke wie die des Co-Commit-Stores: Stapel-Beginn lesen, am Ende vorrücken.</summary>
    private sealed class Marke : IProjectionTracker
    {
        private readonly Dictionary<(string, Guid), int> _stand = new();
        public Task<int> LastProcessedVersionAsync(string p, Guid s, CancellationToken ct) => Task.FromResult(_stand.TryGetValue((p, s), out var v) ? v : -1);
        public Task MarkProcessedAsync(string p, Guid s, int v, CancellationToken ct) { _stand[(p, s)] = v; return Task.CompletedTask; }
        public Task ResetAsync(string p, Guid s, CancellationToken ct) { _stand.Remove((p, s)); return Task.CompletedTask; }
        public Task ResetAllAsync(string p, CancellationToken ct) { _stand.Clear(); return Task.CompletedTask; }
    }

    /// <summary>Ein Cursor, der beim ersten Schreiben „abstürzt“ — der Prozess endet zwischen Emit und Cursor.</summary>
    private sealed class AbsturzCursor : IEmittentenCursor
    {
        public readonly InMemoryEmittentenCursor Innen = new();
        private bool _scharf = true;
        public Task<long> LadeAsync(string partition, CancellationToken ct) => Innen.LadeAsync(partition, ct);
        public Task SchreibeAsync(string partition, long bis, CancellationToken ct)
        {
            if (_scharf) { _scharf = false; throw new InvalidOperationException("Absturz zwischen Emit und Cursor"); }
            return Innen.SchreibeAsync(partition, bis, ct);
        }
    }

    private static EventEnvelope Evt(Guid stream, int version)
        => new() { AggregateId = stream, AggregateVersion = version, CorrelationId = Guid.Empty.ToString(), Payload = new TeilVerbucht(version, 10) };

    [Fact]
    public async Task Doppelte_und_ungeordnete_Weckungen_wenden_jedes_Event_genau_einmal_in_Versionsreihenfolge_an()
    {
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1), Evt(stream, 2) };
        var angewandt = new List<int>();
        var adapter = new ProjectionAdapter(new LeseSeam(log), new Marke(), null, "projektion",
            (e, _, _) => { angewandt.Add(e.AggregateVersion); return Task.CompletedTask; });

        await adapter.WakeAsync(stream, Ct);                     // Signal v1
        await adapter.WakeAsync(stream, Ct);                     // dasselbe Signal doppelt
        lock (log) log.AddRange(new[] { Evt(stream, 3), Evt(stream, 4), Evt(stream, 5) });
        await adapter.WakeAsync(stream, Ct);                     // Poll-Heilung vor den Signalen
        await adapter.WakeAsync(stream, Ct);                     // Signal v5 vor v3 und v4 (ungeordnet)
        await adapter.WakeAsync(stream, Ct);
        await adapter.WakeAsync(stream, Ct);

        angewandt.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Absturz_zwischen_Emit_und_Cursor_gibt_denselben_Command_mit_derselben_CommandId_erneut_aus()
    {
        // Die Reaktion gibt je Event einen Command aus — über das echte Ausgabe-Routing und das eine Emit-Primitiv (Send-Seam).
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1), Evt(stream, 2) };
        var ids = new List<Guid>();
        var emitter = new CommandEmitter((_, env, _) => { lock (ids) ids.Add(env.CommandId); return Task.FromResult<CommandResult?>(new CommandResult { Success = true }); });
        var router = new HandlerOutputRouter(emitter, null, "reaktion");
        var cursor = new AbsturzCursor();
        var adapter = new ProjectionAdapter(new LeseSeam(log), null, cursor, "reaktion",
            (e, _, a) => router.EmitFor(e, a)(new MeldeTeilFertig(Guid.Parse("00000000-0000-0000-0000-0000000000aa"))));

        var erster = () => adapter.WakeAsync(stream, Ct);
        await erster.Should().ThrowAsync<InvalidOperationException>();
        var vorDemAbsturz = ids.ToList();
        ids.Clear();

        await adapter.WakeAsync(stream, Ct);                     // die nächste Weckung nach dem Neustart

        vorDemAbsturz.Should().HaveCount(2);
        ids.Should().Equal(vorDemAbsturz, "dieselbe Ursache ergibt dieselbe CommandId — die Inbox des Ziels verwirft die Wiederholung");
        (await cursor.Innen.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(3);
    }

    [Fact]
    public async Task Ein_nicht_zugestellter_Emit_laesst_den_Cursor_stehen()
    {
        // Wie im generierten Pull-Pfad: Ausgabe-Routing mit den Ausgängen der Weckung. Der Empfänger antwortet zuerst nie (Sendefrist läuft
        // ab) — der Command ist nicht zugestellt, also darf der Cursor nicht über sein Event rücken, und die Weckung meldet „nicht aufgeholt“
        // (der Poll hält seine Marke). Die nächste Weckung sendet ihn mit derselben CommandId erneut.
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1) };
        var antwortet = false;
        var ids = new List<Guid>();
        var emitter = new CommandEmitter(async (_, env, ct) =>
        {
            lock (ids) ids.Add(env.CommandId);
            if (!antwortet) await Task.Delay(Timeout.Infinite, ct);
            return new CommandResult { Success = true };
        }, sendeFrist: TimeSpan.FromMilliseconds(50));
        var router = new HandlerOutputRouter(emitter, null, "reaktion");
        var cursor = new InMemoryEmittentenCursor();
        var adapter = new ProjectionAdapter(new LeseSeam(log), null, cursor, "reaktion",
            (e, _, a) => router.EmitFor(e, a)(new MeldeTeilFertig(Guid.Parse("00000000-0000-0000-0000-0000000000bb"))));

        (await adapter.WakeAsync(stream, Ct)).Should().BeFalse("ein unbekannter Ausgang ist kein Aufholen");
        (await cursor.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(0, "der Command des Events 1 ist nicht zugestellt");

        antwortet = true;
        (await adapter.WakeAsync(stream, Ct)).Should().BeTrue();
        (await cursor.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(2);
        ids.Should().HaveCount(2).And.OnlyContain(id => id == ids[0], "die Wiederholung trägt dieselbe CommandId — die Inbox verwirft sie, falls sie schon wirkte");
    }

    [Fact]
    public async Task Ein_abgelehnter_Command_ist_entschieden_der_Cursor_rueckt_vor()
    {
        // Eine fachliche Ablehnung ist durabel (Marke KommandoAbgelehnt); eine Wiederholung brächte dieselbe — nichts mehr zu tun.
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1) };
        var emitter = new CommandEmitter((_, _, _) => Task.FromResult<CommandResult?>(new CommandResult { Success = false, Abgelehnt = true }));
        var router = new HandlerOutputRouter(emitter, null, "reaktion");
        var cursor = new InMemoryEmittentenCursor();
        var adapter = new ProjectionAdapter(new LeseSeam(log), null, cursor, "reaktion",
            (e, _, a) => router.EmitFor(e, a)(new MeldeTeilFertig(Guid.Parse("00000000-0000-0000-0000-0000000000cc"))));

        (await adapter.WakeAsync(stream, Ct)).Should().BeTrue();
        (await cursor.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(2);
    }

    [Fact]
    public async Task Die_Commands_eines_Tails_gehen_parallel_hinaus()
    {
        // Drei Events, je ein Command an verschiedene Ziele. Der Empfänger antwortet erst, wenn alle drei gleichzeitig unterwegs sind — wären
        // die Sends nacheinander abgewartet, liefe der erste in seine Frist und der Tail bliebe „nicht aufgeholt“.
        var stream = Guid.NewGuid();
        var log = new List<EventEnvelope> { Evt(stream, 1), Evt(stream, 2), Evt(stream, 3) };
        var alleDa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unterwegs = 0;
        var emitter = new CommandEmitter(async (_, _, ct) =>
        {
            if (Interlocked.Increment(ref unterwegs) == 3) alleDa.TrySetResult();
            await alleDa.Task.WaitAsync(ct);
            return new CommandResult { Success = true };
        }, sendeFrist: TimeSpan.FromSeconds(2));
        var router = new HandlerOutputRouter(emitter, null, "reaktion");
        var cursor = new InMemoryEmittentenCursor();
        var adapter = new ProjectionAdapter(new LeseSeam(log), null, cursor, "reaktion",
            (e, _, a) => router.EmitFor(e, a)(new MeldeTeilFertig(Guid.NewGuid())));

        (await adapter.WakeAsync(stream, Ct)).Should().BeTrue();
        (await cursor.LadeAsync($"reaktion:{stream}", Ct)).Should().Be(4);
    }
}
