using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.Trainingslauf;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Serialization;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — die VERMITTLUNG der Funktionen draußen (Plan Pub/Sub-Neustrukturierung §2.2): Ausführer holen sich Eingänge (Pull),
/// jede Zuteilung hat eine Lease, Lebenszeichen verlängern sie, Abgelaufenes geht neu raus, Erledigtes verpufft bei erneutem
/// Angebot. Dazu der Vermittler-Actor (Long-Poll) auf einem lokalen ActorSystem und der Draht-Round-trip eines Auftrags.
/// </summary>
public class FunktionsVermittlungTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static EingangAnbieten Angebot(string _) =>
        new(Guid.NewGuid(), "ITraining", new TrainingAbgebrochen(), Guid.NewGuid(), 3, T0, null, "KIOperator");

    private static EingangAnbieten An(string adresse) =>
        new(Guid.NewGuid(), "ITraining", new TrainingAbgebrochen(), Guid.NewGuid(), 3, T0, null, "KIOperator", Adresse: adresse);

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void A8_alle_Eingaenge_einer_Adresse_rechnet_derselbe_Traeger_solange_er_lebt()
    {
        var v = new Vermittlung<EingangAnbieten>(TimeSpan.FromSeconds(30));
        v.Hole("w2", 4, T0).Should().BeEmpty("w2 lebt (er fragt), hat aber noch nichts");
        var a1 = An("1"); var b1 = An("2");
        v.Biete(a1); v.Biete(b1);
        v.Hole("w1", 4, T0).Should().Equal(new[] { a1 }, "w1 bindet Adresse 1 — Adresse 2 bleibt für w2 (gerecht verteilt)");
        v.Hole("w2", 4, T0).Should().Equal(b1);

        var a2 = An("1");
        v.Biete(a2);
        v.Hole("w2", 4, T0.AddSeconds(5)).Should().BeEmpty("Adresse 1 gehört w1, solange er lebt — dort lebt der Zustand des Workers");
        v.Hole("w1", 4, T0.AddSeconds(6)).Should().Equal(a2);
        v.GebundenAn("1", T0.AddSeconds(6)).Should().Be("w1");

        // w1 fragt nicht mehr und sendet kein Lebenszeichen: nach der Lease geht die Adresse an den nächsten Träger.
        var a3 = An("1");
        v.Biete(a3);
        v.Hole("w2", 4, T0.AddSeconds(40)).Should().Contain(a3);
        v.GebundenAn("1", T0.AddSeconds(40)).Should().Be("w2");
    }

    [Fact]
    public void Ein_einzelner_Traeger_nimmt_alle_Adressen()
    {
        var v = new Vermittlung<EingangAnbieten>(TimeSpan.FromSeconds(30));
        var x = Enumerable.Range(0, 5).Select(i => An(i.ToString())).ToList();
        foreach (var a in x) v.Biete(a);
        v.Hole("allein", 10, T0).Should().Equal(x);
    }

    [Fact]
    public void Doppelte_Angebote_werden_ignoriert_und_Hole_teilt_hoechstens_Max_zu()
    {
        var v = new Vermittlung<EingangAnbieten>(TimeSpan.FromSeconds(30));
        var a = Angebot("a"); var b = Angebot("b"); var c = Angebot("c");
        v.Biete(a).Should().BeTrue();
        v.Biete(a).Should().BeFalse("derselbe Vorgang zweimal angeboten (zweite Weckung) ist kein neuer Auftrag");
        v.Biete(b); v.Biete(c);

        v.Hole("w1", 2, T0).Should().Equal(a, b);
        v.Hole("w2", 5, T0).Should().Equal(c);
        v.Wartend.Should().Be(0);
        v.Vergeben.Should().Be(3);
        v.Biete(a).Should().BeFalse("ein vergebener Auftrag wird nicht erneut aufgenommen");
    }

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Abgelaufene_Lease_geht_an_den_naechsten_Ausfuehrer_Lebenszeichen_haelt_sie()
    {
        var v = new Vermittlung<EingangAnbieten>(TimeSpan.FromSeconds(30));
        var lang = Angebot("training"); var kurz = Angebot("bild");
        v.Biete(lang); v.Biete(kurz);
        v.Hole("gpu", 2, T0).Should().HaveCount(2);

        v.Lebenszeichen(lang.Vorgang, "gpu", T0.AddSeconds(25));
        v.Lebenszeichen(kurz.Vorgang, "fremd", T0.AddSeconds(25));   // nur der Besitzer verlängert

        var neu = v.Hole("cpu", 5, T0.AddSeconds(31));
        neu.Should().Equal(kurz);   // die ohne gültiges Lebenszeichen ist abgelaufen
        v.Hole("cpu", 5, T0.AddSeconds(56)).Should().Equal(lang);
    }

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Erledigtes_verpufft_bei_erneutem_Angebot()
    {
        var v = new Vermittlung<EingangAnbieten>(TimeSpan.FromSeconds(30), erinnerung: 2);
        var a = Angebot("a");
        v.Biete(a); v.Hole("w", 1, T0); v.Erledigt(a.Vorgang);
        v.Biete(a).Should().BeFalse();
        v.Wartend.Should().Be(0);

        // die Erinnerung ist begrenzt: nach zwei weiteren Erledigten ist a vergessen (der Ausführer prüft dann das Log)
        foreach (var x in new[] { Angebot("x"), Angebot("y") }) { v.Biete(x); v.Hole("w", 1, T0); v.Erledigt(x.Vorgang); }
        v.Biete(a).Should().BeTrue();
    }

    [Fact]
    public void Auftrag_reist_polymorph_ueber_den_Wire()
    {
        var a = Angebot("/roh/a.tiff");
        var zugeteilt = new EingaengeZugeteilt(new[] { a });

        var back = (EingaengeZugeteilt)GeneratedWire.Deserialize(GeneratedWire.Serialize(zugeteilt), GeneratedWire.TypeName(zugeteilt));

        back.Eingaenge.Should().ContainSingle();
        back.Eingaenge[0].Vorgang.Should().Be(a.Vorgang);
        back.Eingaenge[0].Akteur.Should().Be("KIOperator");
        back.Eingaenge[0].Eingang.Should().Be(new TrainingAbgebrochen());
        (back.Eingaenge[0].Stream, back.Eingaenge[0].Version).Should().Be((a.Stream, 3));
    }

    [Fact]
    public async Task Der_Vermittler_parkt_eine_Anfrage_ohne_Arbeit_und_bedient_sie_beim_naechsten_Angebot()
    {
        var system = new ActorSystem();
        try
        {
            var pid = system.Root.Spawn(Props.FromProducer(() => new FunktionsVermittlerActor(TimeSpan.FromSeconds(30))));

            var wartend = system.Root.RequestAsync<EingaengeZugeteilt>(pid, new HoleEingaenge("w1", 2, 5_000), TimeSpan.FromSeconds(10));
            await Task.Delay(100);
            wartend.IsCompleted.Should().BeFalse("ohne Arbeit wartet die Anfrage (Long-Poll)");

            var a = Angebot("a");
            await system.Root.RequestAsync<object>(pid, a, TimeSpan.FromSeconds(5));
            (await wartend).Eingaenge.Should().ContainSingle().Which.Vorgang.Should().Be(a.Vorgang);

            // ohne Arbeit und mit kurzer Wartezeit kommt eine leere Antwort (spätestens beim nächsten Takt)
            var leer = await system.Root.RequestAsync<EingaengeZugeteilt>(pid, new HoleEingaenge("w2", 1, 200), TimeSpan.FromSeconds(5));
            leer.Eingaenge.Should().BeEmpty();
        }
        finally { await system.ShutdownAsync(); }
    }

    [Fact]
    public async Task Eine_neuere_Anfrage_desselben_Arbeiters_ersetzt_die_geparkte()
    {
        // Der Cluster wiederholt eine Anfrage nach seinem Request-Timeout — die erste ist dann tot. Bekäme sie die Arbeit, hinge der
        // Auftrag bis zum Ablauf der Lease (gefunden mit dem Einblick der Werkbank: „vergeben“, aber nie gerechnet).
        var system = new ActorSystem();
        try
        {
            var pid = system.Root.Spawn(Props.FromProducer(() => new FunktionsVermittlerActor(TimeSpan.FromSeconds(30))));
            var alt = system.Root.RequestAsync<EingaengeZugeteilt>(pid, new HoleEingaenge("w1", 1, 5_000), TimeSpan.FromSeconds(10));
            await Task.Delay(50);
            var neu = system.Root.RequestAsync<EingaengeZugeteilt>(pid, new HoleEingaenge("w1", 1, 5_000), TimeSpan.FromSeconds(10));
            await Task.Delay(50);

            var a = Angebot("a");
            await system.Root.RequestAsync<object>(pid, a, TimeSpan.FromSeconds(5));
            (await neu).Eingaenge.Should().ContainSingle().Which.Vorgang.Should().Be(a.Vorgang);
            alt.IsCompleted.Should().BeFalse("die ersetzte Anfrage bekommt nichts");

            var offen = await system.Root.RequestAsync<OffeneAuftraege>(pid, new ListeOffene(), TimeSpan.FromSeconds(5));
            offen.Auftraege.Should().ContainSingle().Which.Arbeiter.Should().Be("w1");
        }
        finally { await system.ShutdownAsync(); }
    }
}
