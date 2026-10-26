using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Die Buchführung des Funktions-Actors je Partition (Plan Pub/Sub-Neustrukturierung §2.2), store-frei: Slots, Eingänge eines Streams nacheinander,
/// Position beim ältesten Unfertigen (nie über die gesetzte Sequenz oder das Ende einer vollen Seite), der Deckel für „dahinter fertig“ und das Parken.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsLaufTests
{
    private sealed record Ereignis(int N) : IEvent;

    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private static PartitionsEvent E(long seq, Guid stream, int version) =>
        new(seq, new EventEnvelope { AggregateId = stream, AggregateVersion = version, Payload = new Ereignis(version) });

    private static FunktionsLauf Lauf(int slots, int deckel = FunktionsLauf.ErledigtHoechstens)
    {
        var l = new FunktionsLauf(slots, deckel);
        l.Setze(PartitionsCheckpoint.Neu("funktion:Probe", 0));
        return l;
    }

    [Fact]
    public void Nicht_mehr_Eingaenge_als_Slots_und_der_Rest_haelt_die_Position()
    {
        var l = Lauf(slots: 2);
        var gestartet = l.Nimm(new PartitionsSeite(new[] { E(1, A, 1), E(2, B, 1), E(3, C, 1) }, Gesetzt: 3, Voll: false));

        gestartet.Select(e => e.Sequenz).Should().Equal(1, 2);
        l.FreieSlots.Should().Be(0);
        l.Position.Should().Be(0, "der älteste Eingang ist noch offen");
        l.Fertig(1);
        l.Fertig(2);
        l.Position.Should().Be(2, "der dritte konnte nicht starten — die Position bleibt vor ihm, er liegt weiter im Log");
        l.Nimm(new PartitionsSeite(new[] { E(3, C, 1) }, 3, false)).Select(e => e.Sequenz).Should().Equal(3);
        l.Fertig(3);
        l.Position.Should().Be(3);
    }

    [Fact]
    public void Eingaenge_eines_Streams_laufen_nacheinander_verschiedene_Streams_parallel()
    {
        var l = Lauf(slots: 4);
        var gestartet = l.Nimm(new PartitionsSeite(new[] { E(1, A, 1), E(2, A, 2), E(3, B, 1) }, 3, false));

        gestartet.Select(e => e.Sequenz).Should().Equal(1, 3);
        l.Ausser.Should().BeEquivalentTo(new long[] { 1, 3 }, "die laufenden schließt das nächste Lesen aus, der wartende kommt wieder");
        l.Fertig(3);
        l.Position.Should().Be(0);
        l.Nimm(new PartitionsSeite(new[] { E(2, A, 2) }, 3, false)).Should().BeEmpty("A ist noch belegt");
        l.Fertig(1);
        l.Nimm(new PartitionsSeite(new[] { E(2, A, 2) }, 3, false)).Select(e => e.Sequenz).Should().Equal(2);
        l.Fertig(2);
        l.Position.Should().Be(3);
    }

    [Fact]
    public void Die_Position_ueberholt_weder_die_gesetzte_Sequenz_noch_das_Ende_einer_vollen_Seite()
    {
        var l = Lauf(slots: 8);
        foreach (var e in l.Nimm(new PartitionsSeite(new[] { E(5, A, 1), E(6, B, 1) }, Gesetzt: 5, Voll: false))) l.Fertig(e.Sequenz);
        l.Position.Should().Be(5, "Sequenz 6 ist erledigt, aber nicht gesetzt — eine kleinere könnte noch sichtbar werden");
        l.Erledigt.Should().Contain(6);

        var v = Lauf(slots: 8);
        foreach (var e in v.Nimm(new PartitionsSeite(new[] { E(1, A, 1), E(2, B, 1) }, Gesetzt: 10, Voll: true))) v.Fertig(e.Sequenz);
        v.Position.Should().Be(2, "eine volle Seite reicht nur bis zu ihrem letzten Eingang");
    }

    [Fact]
    public void Ist_die_Liste_der_Erledigten_voll_startet_nur_noch_was_bis_zur_gesetzten_Sequenz_reicht()
    {
        var l = Lauf(slots: 8, deckel: 2);
        var lang = l.Nimm(new PartitionsSeite(new[] { E(1, A, 1), E(2, B, 1), E(3, C, 1) }, Gesetzt: 3, Voll: false));
        l.Fertig(2);
        l.Fertig(3);   // Sequenz 1 läuft lange — 2 und 3 sind dahinter fertig
        l.Erledigt.Should().HaveCount(2);

        var d = Guid.NewGuid();
        l.Nimm(new PartitionsSeite(new[] { E(4, d, 1) }, Gesetzt: 3, Voll: false)).Should().BeEmpty("die Liste ist voll und 4 liegt hinter der gesetzten Sequenz");
        l.Fertig(1);
        l.Position.Should().Be(3);
        l.Erledigt.Should().BeEmpty("hinter der Position wird nichts mehr gemerkt");
        lang.Should().HaveCount(3);
    }

    [Fact]
    public void Ein_geparkter_Stream_haelt_niemanden_auf_und_laeuft_nach_einem_Einzelversuch_wieder()
    {
        var l = Lauf(slots: 4);
        l.Nimm(new PartitionsSeite(new[] { E(1, A, 1), E(2, B, 1) }, 2, false));
        var g = l.Parke(1, "wirft", DateTimeOffset.UnixEpoch);
        g.Should().NotBeNull();
        g!.Version.Should().Be(0, "geparkt ab der Version vor dem werfenden Eingang");
        l.Fertig(2);
        l.Position.Should().Be(2, "der geparkte Eingang zählt als erledigt — der Einzelversuch holt ihn nach");
        l.ZuSchreiben(auchGeparkt: true)!.Geparkt.Should().ContainSingle(x => x.StreamId == A);

        l.Nimm(new PartitionsSeite(new[] { E(3, A, 2), E(4, C, 1) }, 4, false)).Select(e => e.Sequenz).Should().Equal(4);
        l.Position.Should().Be(3, "spätere Eingänge des geparkten Streams werden übersprungen, sie halten die Position nicht auf");

        l.BeginneParkVersuch(A).Should().BeTrue();
        l.Nimm(new PartitionsSeite(new[] { E(5, A, 3) }, 5, false)).Should().BeEmpty("während des Einzelversuchs wartet der Stream im Log");
        l.EndeParkVersuch(A, durch: true);
        l.Geparkt.Should().BeEmpty();
        l.Nimm(new PartitionsSeite(new[] { E(5, A, 3) }, 5, false)).Select(e => e.Sequenz).Should().Equal(5);
    }

    [Fact]
    public void Der_Stand_wird_nur_geschrieben_wenn_er_sich_aenderte()
    {
        var l = Lauf(slots: 2);
        l.ZuSchreiben(false).Should().BeNull();
        foreach (var e in l.Nimm(new PartitionsSeite(new[] { E(1, A, 1) }, 1, false))) l.Fertig(e.Sequenz);
        var neu = l.ZuSchreiben(false);
        neu!.Position.Should().Be(1);
        neu.Version.Should().Be(0, "erwartet wird die geladene Version");
        l.Geschrieben(neu, 1);
        l.ZuSchreiben(false).Should().BeNull();
        l.Stand!.Version.Should().Be(1);
    }

    [Fact]
    public void Nach_einem_Konflikt_wird_neu_geladen_und_laufende_Arbeit_doppelt_nicht()
    {
        var l = Lauf(slots: 2);
        l.Nimm(new PartitionsSeite(new[] { E(1, A, 1) }, 1, false));
        l.Verloren();
        l.Stand.Should().BeNull();
        l.Setze(new PartitionsCheckpoint { Id = "funktion:Probe:0", ProjektionsId = "funktion:Probe", Position = 0, Version = 3 });
        l.Nimm(new PartitionsSeite(new[] { E(1, A, 1), E(2, B, 1) }, 2, false)).Select(e => e.Sequenz).Should().Equal(2);
    }
}
