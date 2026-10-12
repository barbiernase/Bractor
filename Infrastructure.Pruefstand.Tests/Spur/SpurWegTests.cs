using System.Collections.Generic;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.SpurPruefung;

/// <summary>
/// Werkbank P6.2: der Weg eines Log-Eintrags über die Karten — rein aus den Spur-Headern (ursache, wirker, ueber). So nummeriert die
/// Werkbank die gegangenen Kanten eines Vorgangs; der Editor schlägt die Ids nur nach.
/// </summary>
public class SpurWegTests
{
    private static readonly Dictionary<string, SpurWeg.Eintrag> Log = new()
    {
        ["q:1"] = new("DateiErkannt", "quelle:DateiErkannt", null),
        ["d:1"] = new("ImagePairDateiGedeutet", "funktion:IDateinameDeutung", "fluss:Bildeingang#deuten"),
    };

    private static SpurWeg.Eintrag? Nachschlagen(string u) => Log.GetValueOrDefault(u);

    private static string[] Paare(SpurWeg w) => w.Paare.Select(p => p[0] + " → " + p[1]).ToArray();

    [Fact]
    public void Aggregat_Event_laeuft_ueber_Absender_Command_und_Decider()
    {
        var w = SpurWeg.Aus("ImagePairErstellt", "decide:ImagePair/ErstelleImagePair", "fluss:Bildeingang#paar", "d:1", false, Nachschlagen);

        w.Karten.Should().Equal("fluss:Bildeingang#paar", "rec:ErstelleImagePair", "decide:ImagePair/ErstelleImagePair", "rec:ImagePairErstellt");
        Paare(w).Should().Equal(
            "rec:ImagePairDateiGedeutet → fluss:Bildeingang#paar",
            "fluss:Bildeingang#deuten → fluss:Bildeingang#paar",          // der Draht im Fluss: die Ursache kam vom Knoten „deuten“
            "fluss:Bildeingang#paar → rec:ErstelleImagePair",
            "rec:ErstelleImagePair → decide:ImagePair/ErstelleImagePair",
            "decide:ImagePair/ErstelleImagePair → rec:ImagePairErstellt");
    }

    [Fact]
    public void Erster_Knoten_eines_Flusses_haengt_am_Quell_Knoten_aus_dem_Code()
    {
        var w = SpurWeg.Aus("ImagePairDateiGedeutet", "funktion:IDateinameDeutung", "fluss:Bildeingang#deuten", "q:1", false, Nachschlagen,
            (pipeline, typ) => pipeline == "Bildeingang" && typ == "DateiErkannt" ? "fluss:Bildeingang#datei" : null);

        Paare(w).Should().Contain("fluss:Bildeingang#datei → fluss:Bildeingang#deuten")
            .And.Contain("funktion:IDateinameDeutung → rec:ImagePairDateiGedeutet");
    }

    [Fact]
    public void Abgelehnter_Command_von_aussen_endet_am_Decider_ohne_Event_Karte()
    {
        var w = SpurWeg.Aus("KommandoAbgelehnt", "decide:Datensatz/ErstelleDatensatz", "akteur:KIOperator", null, true, Nachschlagen);

        w.Karten.Should().Equal("akteur:KIOperator", "rec:ErstelleDatensatz", "decide:Datensatz/ErstelleDatensatz");
        Paare(w).Should().HaveCount(2, "ohne Ursache gibt es keine Kante davor");
    }

    [Fact]
    public void Quelle_ist_Wurzel_und_Drahte_fremder_Ablaeufe_zaehlen_nicht()
    {
        SpurWeg.Aus("DateiErkannt", "quelle:DateiErkannt", null, null, false, Nachschlagen).Karten
            .Should().Equal("quelle:DateiErkannt", "rec:DateiErkannt");

        // Eine Regel eines anderen Ablaufs, ausgelöst vom Ergebnis eines Fluss-Knotens: kein Draht zwischen beiden.
        var w = SpurWeg.Aus("Fertig", "decide:Bildakte/MeldeFertig", "prozess:Andere#X→MeldeFertig", "d:1", false, Nachschlagen);
        Paare(w).Should().NotContain(p => p.StartsWith("fluss:"));
    }
}
