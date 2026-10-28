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
        ["a:1"] = new("BildAufbereitet", "funktion:IBildaufbereitung", null),
    };

    private static SpurWeg.Eintrag? Nachschlagen(string u) => Log.GetValueOrDefault(u);

    private static string[] Paare(SpurWeg w) => w.Paare.Select(p => p[0] + " → " + p[1]).ToArray();

    [Fact]
    public void Aggregat_Event_laeuft_ueber_Absender_Command_und_Decider()
    {
        var w = SpurWeg.Aus("ImagePairErstellt", "decide:ImagePair/NimmBildAuf", "funktion:Bildeingang", "a:1", Nachschlagen);

        w.Karten.Should().Equal("funktion:Bildeingang", "rec:NimmBildAuf", "decide:ImagePair/NimmBildAuf", "rec:ImagePairErstellt");
        Paare(w).Should().Equal(
            "rec:BildAufbereitet → funktion:Bildeingang",
            "funktion:Bildeingang → rec:NimmBildAuf",
            "rec:NimmBildAuf → decide:ImagePair/NimmBildAuf",
            "decide:ImagePair/NimmBildAuf → rec:ImagePairErstellt");
    }

    [Fact]
    public void Ausgabe_einer_Funktion_haengt_an_ihrem_Eingang()
    {
        var w = SpurWeg.Aus("BildAufbereitet", "funktion:IBildaufbereitung", null, "q:1", Nachschlagen);

        Paare(w).Should().Equal("rec:DateiErkannt → funktion:IBildaufbereitung", "funktion:IBildaufbereitung → rec:BildAufbereitet");
    }

    [Fact]
    public void Quelle_ist_Wurzel()
        => SpurWeg.Aus("DateiErkannt", "quelle:DateiErkannt", null, null, Nachschlagen).Karten
            .Should().Equal("quelle:DateiErkannt", "rec:DateiErkannt");
}
