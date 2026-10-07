using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.Bildaufbereitung;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Serialization;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — die VERMITTLUNG der Katalog-Funktionen (docs/konzept-editor-pipelines.md §14.5): Ausführer holen sich Arbeit (Pull),
/// jede Zuteilung hat eine Lease, Lebenszeichen verlängern sie, Abgelaufenes geht neu raus, Erledigtes verpufft bei erneutem
/// Angebot. Dazu der Vermittler-Actor (Long-Poll) auf einem lokalen ActorSystem und der Draht-Round-trip eines Auftrags.
/// </summary>
public class FunktionsVermittlungTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static AuftragAnbieten Angebot(string pfad) => new(Guid.NewGuid(), Guid.NewGuid(), "KameraSystem", new VerkleinereBild(pfad, 512));

    [Fact]
    public void Doppelte_Angebote_werden_ignoriert_und_Hole_teilt_hoechstens_Max_zu()
    {
        var v = new FunktionsVermittlung(TimeSpan.FromSeconds(30));
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

    [Fact]
    public void Abgelaufene_Lease_geht_an_den_naechsten_Ausfuehrer_Lebenszeichen_haelt_sie()
    {
        var v = new FunktionsVermittlung(TimeSpan.FromSeconds(30));
        var lang = Angebot("training"); var kurz = Angebot("bild");
        v.Biete(lang); v.Biete(kurz);
        v.Hole("gpu", 2, T0).Should().HaveCount(2);

        v.Lebenszeichen(lang.Vorgang, "gpu", T0.AddSeconds(25));
        v.Lebenszeichen(kurz.Vorgang, "fremd", T0.AddSeconds(25));   // nur der Besitzer verlängert

        var neu = v.Hole("cpu", 5, T0.AddSeconds(31));
        neu.Should().Equal(kurz);   // die ohne gültiges Lebenszeichen ist abgelaufen
        v.Hole("cpu", 5, T0.AddSeconds(56)).Should().Equal(lang);
    }

    [Fact]
    public void Erledigtes_verpufft_bei_erneutem_Angebot()
    {
        var v = new FunktionsVermittlung(TimeSpan.FromSeconds(30), erinnerung: 2);
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
        var zugeteilt = new ArbeitZugeteilt(new[] { a });

        var back = (ArbeitZugeteilt)GeneratedWire.Deserialize(GeneratedWire.Serialize(zugeteilt), GeneratedWire.TypeName(zugeteilt));

        back.Auftraege.Should().ContainSingle();
        back.Auftraege[0].Vorgang.Should().Be(a.Vorgang);
        back.Auftraege[0].Akteur.Should().Be("KameraSystem");
        back.Auftraege[0].Auftrag.Should().Be(new VerkleinereBild("/roh/a.tiff", 512));
    }

    [Fact]
    public async Task Der_Vermittler_parkt_eine_Anfrage_ohne_Arbeit_und_bedient_sie_beim_naechsten_Angebot()
    {
        var system = new ActorSystem();
        try
        {
            var pid = system.Root.Spawn(Props.FromProducer(() => new FunktionsVermittlerActor(TimeSpan.FromSeconds(30))));

            var wartend = system.Root.RequestAsync<ArbeitZugeteilt>(pid, new HoleArbeit("w1", 2, 5_000), TimeSpan.FromSeconds(10));
            await Task.Delay(100);
            wartend.IsCompleted.Should().BeFalse("ohne Arbeit wartet die Anfrage (Long-Poll)");

            var a = Angebot("a");
            await system.Root.RequestAsync<object>(pid, a, TimeSpan.FromSeconds(5));
            (await wartend).Auftraege.Should().ContainSingle().Which.Vorgang.Should().Be(a.Vorgang);

            // ohne Arbeit und mit kurzer Wartezeit kommt eine leere Antwort (spätestens beim nächsten Takt)
            var leer = await system.Root.RequestAsync<ArbeitZugeteilt>(pid, new HoleArbeit("w2", 1, 200), TimeSpan.FromSeconds(5));
            leer.Auftraege.Should().BeEmpty();
        }
        finally { await system.ShutdownAsync(); }
    }
}
