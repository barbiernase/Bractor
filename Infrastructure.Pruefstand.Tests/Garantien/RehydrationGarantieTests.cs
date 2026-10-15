using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Sammelvorgang;
using FluentAssertions;
using Infrastructure.Aggregate;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Garantien der Rehydration (docs/11 §11.2, §11.3) — was ein Aggregat-Actor nach einem Neustart (Host, Passivierung,
/// Rehydration nach einem Fehler) aus dem Log wiederherstellt. Store-frei über den Lese-Seam
/// <see cref="IEventStoreRepository.ReadStreamAsync"/>: die Envelopes sind so, wie Marten sie materialisiert (Position,
/// SubIndex); Store-Semantik selbst prüfen die Integrations-Suiten.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class RehydrationGarantieTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Domain.AggregateHandlerFactory();

    private sealed class LeseSeam(IReadOnlyList<EventEnvelope> log) : IEventStoreRepository
    {
        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EventEnvelope>>(log.Where(e => e.AggregateVersion >= fromVersion).ToList());
        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null, string? ak = null)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct) => throw new NotSupportedException();
        public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => throw new NotSupportedException();
    }

    private static EventEnvelope Env(Guid id, int version, IEvent payload, int subIndex = 0)
        => new() { AggregateId = id, AggregateVersion = version, SubIndex = subIndex, Payload = payload };

    [Fact]
    public async Task Neustart_stellt_die_Inbox_aus_dem_Log_wieder_her()
    {
        // Ein Command, der vor dem Neustart gewirkt hat (Event + Marke), und einer, der abgelehnt wurde (nur Marke):
        // nach der Rehydration erkennt der Actor beide Wiederholungen, bevor er entscheidet.
        var id = Guid.NewGuid();
        var verarbeitet = Guid.NewGuid();
        var abgelehnt = Guid.NewGuid();
        var log = new[]
        {
            Env(id, 1, new SammelvorgangGestartet(2)),
            Env(id, 2, new KommandoVerarbeitet(verarbeitet)),
            Env(id, 3, new KommandoAbgelehnt(abgelehnt, "SammelvorgangExistiertBereits")),
        };

        var r = await AggregateRehydrator.LoadAsync<Sammelvorgang>(id, null, new LeseSeam(log), Fabrik, null, default);

        r.State.Version.Should().Be(3, "jede Hülle zählt, auch die Marken");
        r.State.Erwartet.Should().Be(2);
        r.ProcessedCommandIds.Contains(verarbeitet).Should().BeTrue();
        r.RejectedCommandIds.Contains(abgelehnt).Should().BeTrue();
    }

    [Fact(Skip = "G-5 offen (docs/13)")]
    public async Task Split_Upcast_verschiebt_die_Version_nicht()
    {
        // Ein Upcast teilt die gespeicherte Position 2 in zwei Events (SubIndex 0 und 1). Die Version des Aggregats ist
        // die Zahl der GESPEICHERTEN Positionen — sonst läuft sie dem Log davon, und jeder Append scheitert an der OCC.
        var id = Guid.NewGuid();
        var log = new[]
        {
            Env(id, 1, new SammelvorgangGestartet(3)),
            Env(id, 2, new TeilVerbucht(1, 3), subIndex: 0),
            Env(id, 2, new TeilVerbucht(2, 3), subIndex: 1),
        };

        var r = await AggregateRehydrator.LoadAsync<Sammelvorgang>(id, null, new LeseSeam(log), Fabrik, null, default);

        r.State.Fertig.Should().Be(2, "beide materialisierten Events wirken");
        r.State.Version.Should().Be(2, "die Version zählt gespeicherte Positionen, nicht materialisierte Events");
    }
}
