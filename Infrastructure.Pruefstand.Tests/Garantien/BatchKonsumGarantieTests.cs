using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Core;
using FluentAssertions;
using Infrastructure.Projections;
using Infrastructure.Pruefstand.Konsumenten;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Garantien des Batch-Konsums (docs/11 §11.4, §11.5), die reine Leser-Logik sind — store-frei über die Seams des <see cref="BatchLeser"/>:
/// doppelte und ungeordnete Seiten mit Nachzüglern, volle Seiten, ein dauerhafter Konflikt. Was an Marten hängt (Absturz mitten im Batch, zwei
/// Aktivierungen, Poison-Stream, Konflikt an geteilten Dokumenten), prüft allein die Integration (<c>CoCommitPostgresTests</c>,
/// <c>GarantiePostgresTests</c>, <c>SchreibRegelPostgresTests</c>).
/// </summary>
public class BatchKonsumGarantieTests
{
    private const string Projektion = "projektion";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Konsum Klein = new(Partitionen: 2, BatchGroesse: 3);

    private static BatchLeser Leser(PartitionsLog log, SpeicherTracker tracker, int partition = 0,
        Func<EventEnvelope, Task>? vorher = null, Func<Task>? absturz = null, Func<DateTimeOffset>? jetzt = null)
        => new(log, tracker, Projektion, partition, Klein, typen: null, async (e, _) =>
        {
            if (vorher is not null) await vorher(e);
            tracker.Wende(e.AggregateId, e.AggregateVersion);
        }, absturzVorCommit: absturz, jetzt: jetzt);

    [Fact]
    public async Task Doppelte_und_ungeordnete_Seiten_und_Nachzuegler_wenden_jedes_Event_genau_einmal_in_Versionsreihenfolge_an()
    {
        var log = new PartitionsLog { DoppeltUndUngeordnet = true };
        var db = new SpeicherKonsum();
        var (a, b) = (PartitionsLog.StreamIn(0, 2), PartitionsLog.StreamIn(0, 2));
        log.Haenge(a, 1);
        log.Haenge(a, 2);
        var nachzuegler = log.Haenge(b, 1, sichtbar: false, gesetzt: false);   // kleinere Sequenz, noch nicht committet
        log.Haenge(a, 3, gesetzt: false);                                        // jung: angewandt, aber die Position hält davor
        log.Haenge(a, 4, gesetzt: false);
        log.Haenge(a, 5, gesetzt: false);

        var leser = Leser(log, new SpeicherTracker(db));
        await leser.LaufAsync(Ct);
        await leser.LaufAsync(Ct);                                               // doppelte Weckung
        leser.Position.Should().Be(2, "die Position überholt den unsichtbaren Nachzügler (Sequenz 3) nicht");

        nachzuegler.Sichtbar = true;                                             // der Nachzügler wird sichtbar
        await leser.LaufAsync(Ct);
        nachzuegler.Gesetzt = true;
        await Leser(log, new SpeicherTracker(db)).LaufAsync(Ct);                 // Neuaktivierung: liest ab der Position, der Guard fängt ab

        db.VersionenVon(a).Should().Equal(1, 2, 3, 4, 5);
        db.VersionenVon(b).Should().Equal(1);
    }

    [Fact]
    public async Task Eine_volle_Seite_liest_weiter_und_die_Position_rueckt_bis_zur_gesetzten_Sequenz()
    {
        var log = new PartitionsLog();
        var db = new SpeicherKonsum();
        var streams = Enumerable.Range(0, 4).Select(_ => PartitionsLog.StreamIn(1, 2)).ToList();
        foreach (var s in streams) { log.Haenge(s, 1); log.Haenge(s, 2); }
        var fremd = log.Haenge(PartitionsLog.StreamIn(0, 2), 1);                // andere Partition, jüngste gesetzte Sequenz

        var leser = Leser(log, new SpeicherTracker(db), partition: 1);
        (await leser.LaufAsync(Ct)).Should().Be(8);

        db.Angewandt.Should().HaveCount(8);
        leser.Position.Should().Be(fremd.Seq, "nach der letzten (nicht vollen) Seite läuft die Position über fremde Events bis zur gesetzten Sequenz");
    }

    [Fact]
    public async Task Ein_dauerhafter_Konflikt_stellt_den_Stream_zurueck_die_Position_haelt_vor_ihm()
    {
        var log = new PartitionsLog();
        var db = new SpeicherKonsum();
        var (umkaempft, frei) = (PartitionsLog.StreamIn(0, 2), PartitionsLog.StreamIn(0, 2));
        var erster = log.Haenge(umkaempft, 1);
        log.Haenge(frei, 1);

        db.CommitFehler = guards => guards.ContainsKey(umkaempft) ? new SchreibKonfliktException("geteiltes Dokument", new Exception()) : null;
        var leser = Leser(log, new SpeicherTracker(db));
        await leser.LaufAsync(Ct);

        db.VersionenVon(frei).Should().Equal(1);
        leser.Position.Should().Be(erster.Seq - 1, "ein Konflikt ist vorübergehend: der Stream wird nicht geparkt, die Position hält vor ihm");
        leser.Geparkt.Should().BeEmpty();

        db.CommitFehler = null;                                                  // der Konkurrent ist fertig
        await leser.LaufAsync(Ct);
        db.VersionenVon(umkaempft).Should().Equal(1);
        db.VersionenVon(frei).Should().Equal(new[] { 1 }, "der Guard fängt die Wiederholung ab");
    }
}
