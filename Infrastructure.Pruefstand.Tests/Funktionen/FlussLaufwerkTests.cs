using System;
using System.Linq;
using Abstractions;
using Cqrs.Testing;
using Domain.Bildaufbereitung;
using Domain.ImagePair;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — das DURCHSPIELEN eines Flusses ohne Implementierung (<see cref="FlussLaufwerk"/>, Grundlage der Editor-Simulation):
/// derselbe Belegungs-Kern wie der Dirigent (<see cref="FlussBelegung"/>), die Funktionen antworten per Wahl, die Commands laufen
/// gegen das ECHTE ImagePair-Aggregat (generierte Fabrik). Bewiesen am Bildeingang der Domäne: erste Datei → anlegen + melden;
/// zweite Datei desselben Paars → ✕-Port des abgelehnten Anlegens → ∨ melden; ⏳ an einer Funktion ohne verdrahteten Port scheitert.
/// </summary>
public class FlussLaufwerkTests
{
    private static readonly Guid Paar = Guid.Parse("00000000-0000-0000-0000-0000000000b7");

    private static FlussLaufwerk Laufwerk()
        => new(new SagaLaufwerk(new Infrastructure.AggregateHandlerFactory(), Array.Empty<(string, ProzessRegeln)>()),
            new[] { (nameof(Bildeingang), new Bildeingang().Fluss) });

    private static FlussAntwort Gedeutet(FlussAufruf a) => a.Auftrag switch
    {
        DeuteDateiname d => new FlussAntwort.Ergebnis(new ImagePairDateiGedeutet(Paar, "P1",
            d.Dateiname.Contains("_2") ? BildVersion.Dc2 : BildVersion.Dc0, DateTimeOffset.UnixEpoch, d.Pfad)),
        VerkleinereBild v => new FlussAntwort.Ergebnis(new BildVerkleinert(v.QuellPfad + "_klein.png", 683, v.Hoehe)),
        GleicheHistogrammAus h => new FlussAntwort.Ergebnis(new HistogrammAusgeglichen(h.QuellPfad + "_aus.png", 683, 512, DateTimeOffset.UnixEpoch)),
        _ => throw new NotSupportedException(a.Auftrag.GetType().Name),
    };

    private static DateiErkannt Datei(string name) => new("/share/" + name, name, 42, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Erste_Datei_legt_das_Paar_an_und_meldet_das_Bild()
    {
        var lauf = Laufwerk().Starte(nameof(Bildeingang), Datei("a_1.tiff"), Gedeutet);

        lauf.Vorgänge.Should().ContainSingle().Which.Erfolg.Should().BeTrue();
        var commands = lauf.Schritte.Where(s => s.Art == PipelineKnotenArt.Command).ToList();
        commands.Select(s => s.Nachricht.GetType()).Should().Equal(typeof(ErstelleImagePair), typeof(MeldeBildVerfuegbar));
        commands[0].Ergebnis.Should().BeOfType<ImagePairErstellt>();
        commands.Should().OnlyContain(s => s.Kaskade != null && s.Ausgang == FlussAusgang.Fall);
        // Parallel aus EINEM Port: Anlegen und Verkleinern liegen in derselben Welle (nach dem Deuten).
        lauf.Schritte.Select(s => s.Typ.Name).Should().ContainInOrder(nameof(DeuteDateiname), nameof(ErstelleImagePair), nameof(VerkleinereBild));
    }

    [Fact]
    public void Zweite_Datei_nimmt_den_Ablehnungs_Port_und_meldet_ueber_den_Oder_Weg()
    {
        var w = Laufwerk();
        w.Starte(nameof(Bildeingang), Datei("a_1.tiff"), Gedeutet);
        var lauf = w.Starte(nameof(Bildeingang), Datei("a_2.tiff"), Gedeutet);

        lauf.Vorgänge.Should().ContainSingle().Which.Erfolg.Should().BeTrue();
        var anlegen = lauf.Schritte.Single(s => s.Nachricht is ErstelleImagePair);
        anlegen.Ausgang.Should().Be(FlussAusgang.Abgelehnt);
        anlegen.Ergebnis.Should().BeOfType<SchrittAbgelehnt>();
        var melden = lauf.Schritte.Single(s => s.Nachricht is MeldeBildVerfuegbar);
        melden.Ein.Should().Contain(d => d.Port == FlussAusgang.Abgelehnt);
    }

    [Fact]
    public void Dieselbe_Version_zweimal_scheitert_am_freien_Ablehnungs_Port_der_Meldung()
    {
        var w = Laufwerk();
        w.Starte(nameof(Bildeingang), Datei("a_1.tiff"), Gedeutet);
        var lauf = w.Starte(nameof(Bildeingang), Datei("a_1b.tiff"), Gedeutet);

        var v = lauf.Vorgänge.Should().ContainSingle().Subject;
        v.Erfolg.Should().BeFalse("das Aggregat lehnt die Meldung ab (BildVersionBereitsVerfuegbar) und der ✕-Port der Meldung ist frei");
        lauf.Schritte.Last().Grund.Should().Contain(nameof(BildVersionBereitsVerfuegbar));
    }

    [Fact]
    public void Zeitlimit_ohne_verdrahteten_Port_laesst_den_Vorgang_scheitern()
    {
        var lauf = Laufwerk().Starte(nameof(Bildeingang), Datei("b_1.tiff"),
            a => a.Auftrag is VerkleinereBild ? new FlussAntwort.Zeitlimit() : Gedeutet(a));

        var v = lauf.Vorgänge.Should().ContainSingle().Subject;
        v.Erfolg.Should().BeFalse();
        v.Grund.Should().StartWith("⏳");
        lauf.Schritte.Should().NotContain(s => s.Nachricht is MeldeBildVerfuegbar);
    }

    [Fact]
    public void Unbekannter_Name_nimmt_den_anderen_Fall_und_endet_ohne_Command()
    {
        var lauf = Laufwerk().Starte(nameof(Bildeingang), Datei("notiz.txt"),
            a => a.Auftrag is DeuteDateiname d ? new FlussAntwort.Ergebnis(new DateinameUnbekannt(d.Dateiname)) : Gedeutet(a));

        lauf.Vorgänge.Should().ContainSingle().Which.Erfolg.Should().BeTrue();
        lauf.Schritte.Should().NotContain(s => s.Art == PipelineKnotenArt.Command);
    }
}
