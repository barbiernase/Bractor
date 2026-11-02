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
    // Was im Log steht: Aggregat-Events (Wirker = Decide) — die Commands zwischen Knoten und die Elemente einer Quelle stehen nicht darin.
    private static readonly Dictionary<string, SpurWeg.Eintrag> Log = new()
    {
        ["p:1"] = new("ImagePairErstellt", "decide:ImagePair/NimmBildAuf", "knoten:linienimport/Aufbereitung.Eingang"),
    };

    private static SpurWeg.Eintrag? Nachschlagen(string u) => Log.GetValueOrDefault(u);

    private static string[] Paare(SpurWeg w) => w.Paare.Select(p => p[0] + " → " + p[1]).ToArray();

    [Fact]
    public void Aggregat_Event_laeuft_ueber_den_Knoten_der_den_Command_gab_und_den_Decider()
    {
        // Der Lauf einer Quelle hat keinen Log-Eintrag: der Knoten, der den Command gab, ist die Wurzel des Wegs.
        var w = SpurWeg.Aus("ImagePairErstellt", "decide:ImagePair/NimmBildAuf", "knoten:linienimport/Aufbereitung.Eingang", null, Nachschlagen);

        w.Karten.Should().Equal("knoten:linienimport/Aufbereitung.Eingang", "rec:NimmBildAuf", "decide:ImagePair/NimmBildAuf", "rec:ImagePairErstellt");
        Paare(w).Should().Equal(
            "knoten:linienimport/Aufbereitung.Eingang → rec:NimmBildAuf",
            "rec:NimmBildAuf → decide:ImagePair/NimmBildAuf",
            "decide:ImagePair/NimmBildAuf → rec:ImagePairErstellt");
    }

    [Fact]
    public void Ein_Knoten_der_auf_ein_Event_hin_einen_Command_gibt_haengt_an_dessen_Eintrag()
    {
        // Die Arbeit des Klassifizierers kam aus dem Decide des Paars (Ursache p:1); seine Antwort entscheidet das Paar.
        var w = SpurWeg.Aus("BildPaarDurchKiKlassifiziert", "decide:ImagePair/KlassifiziereBildPaarDurchKi", "knoten:Klassifizierer/Klassifizierer", "p:1", Nachschlagen);

        Paare(w).Should().Contain("rec:ImagePairErstellt → knoten:Klassifizierer/Klassifizierer")
            .And.Contain("knoten:Klassifizierer/Klassifizierer → rec:KlassifiziereBildPaarDurchKi");
    }
}
