using Abstractions;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.SpurPruefung;

/// <summary>Werkbank P4.1: das Wirker-Format an einer Stelle (<see cref="WirkerId"/>) — dieselbe Id wie die Karte im Editor.</summary>
public class WirkerIdTests
{
    [Fact]
    public void Die_Formen()
    {
        WirkerId.FuerDecide("Datensatz", "ErstelleDatensatz").Should().Be("decide:Datensatz/ErstelleDatensatz");
        WirkerId.FuerFunktion("IBildaufbereitung").Should().Be("funktion:IBildaufbereitung");
        WirkerId.FuerKnoten("linie1", "Aufbereitung.Vorschau").Should().Be("knoten:linie1/Aufbereitung.Vorschau");
        WirkerId.FuerProjektion("P").Should().Be("projektion:P");
        WirkerId.FuerAkteur("Disponent").Should().Be("akteur:Disponent");
        WirkerId.FuerEingang("Linienrechner", "linie1").Should().Be("eingang:Linienrechner/linie1", "ein Eingang eines Clients aus einer Quelle");
        WirkerId.Zerlege("eingang:Linienrechner/linie1", out var eArt, out var eRest).Should().BeTrue();
        (eArt, eRest).Should().Be(("eingang", "Linienrechner/linie1"));
        WirkerId.Zerlege("funktion:Bildeingang", out var art, out var rest).Should().BeTrue();
        (art, rest).Should().Be(("funktion", "Bildeingang"));
        WirkerId.Zerlege("rec:DateiErkannt", out _, out _).Should().BeFalse();
        WirkerId.Zerlege("fluss:Bildeingang#datei", out _, out _).Should().BeFalse("Flüsse gibt es nicht mehr");
    }
}
