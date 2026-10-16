using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Core;
using FluentAssertions;
using Infrastructure.Projections;
using Infrastructure.Pruefstand.Konsumenten;
using Xunit;

namespace Infrastructure.Pruefstand.Konsumenten;

/// <summary>
/// Der Neuaufbau einer Projektion, store-frei: „Neuaufbau = Batch-Konsum ab Position 0“ — Ziel leeren, Partitionen und Guards zurücksetzen,
/// jede Partition seitenweise ab 0 anwenden baut das (append-artige) Read-Model KORREKT neu auf: NICHT verdoppelt (das Leeren trägt), über
/// alle Partitionen, als Strom über mehrere Seiten; ein zweiter Neuaufbau ist idempotent. Gegenprobe: ohne das Leeren verdoppelt ein
/// append-artiges Ziel.
/// </summary>
public class ProjectionRebuilderTests
{
    private const string ProjId = "test-projektion";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Konsum Klein = new(Partitionen: 4, BatchGroesse: 2);

    private static (PartitionsLog Log, Guid[] Streams) Log()
    {
        var log = new PartitionsLog();
        var streams = Enumerable.Range(0, 4).Select(p => PartitionsLog.StreamIn(p, Klein.Partitionen)).ToArray();
        foreach (var s in streams) { log.Haenge(s, 1); log.Haenge(s, 2); log.Haenge(s, 3); }
        return (log, streams);
    }

    private static async Task Materialisiere(PartitionsLog log, SpeicherTracker ziel)
    {
        for (var p = 0; p < Klein.Partitionen; p++)
            await new BatchLeser(log, ziel, ProjId, p, Klein, null, (e, _) => { ziel.Wende(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; })
                .LaufAsync(Ct);
    }

    [Fact]
    public async Task Rebuild_baut_das_Read_Model_ueber_alle_Partitionen_seitenweise_neu_auf_ohne_zu_verdoppeln()
    {
        var (log, streams) = Log();
        var db = new SpeicherKonsum();
        var ziel = new SpeicherTracker(db);
        await Materialisiere(log, ziel);
        db.Angewandt.Should().HaveCount(12);

        var rebuilder = new ProjectionRebuilder(log, Klein);
        var gelesenVorher = log.Lesungen;
        await rebuilder.RebuildAsync(ProjId, ziel, ziel, null, (e, _) => { ziel.Wende(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; }, Ct);

        db.Angewandt.Should().HaveCount(12, "geleert und genau einmal neu angewandt");
        foreach (var s in streams) db.VersionenVon(s).Should().Equal(1, 2, 3);
        (log.Lesungen - gelesenVorher).Should().BeGreaterThan(Klein.Partitionen, "als Strom: mehrere Seiten je Partition, nie der ganze Log auf einmal");

        await rebuilder.RebuildAsync(ProjId, ziel, ziel, null, (e, _) => { ziel.Wende(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; }, Ct);
        db.Angewandt.Should().HaveCount(12, "ein zweiter Neuaufbau ist idempotent");
    }

    [Fact]
    public async Task Gegenprobe_ohne_Leeren_wuerde_ein_append_Ziel_verdoppeln()
    {
        var (log, _) = Log();
        var db = new SpeicherKonsum();
        var ziel = new SpeicherTracker(db);
        await Materialisiere(log, ziel);

        await ziel.ResetAllAsync(ProjId, Ct);                    // Zurücksetzen OHNE Leeren
        await Materialisiere(log, ziel);
        db.Angewandt.Should().HaveCount(24, "verdoppelt → deshalb IST das Leeren nötig");
    }
}
