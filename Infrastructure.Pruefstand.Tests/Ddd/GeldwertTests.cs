using System.Diagnostics;
using Domain.Verkauf;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Baustein VALUE OBJECT (<see cref="Geldwert"/>) direkt: unveränderlich, gleich per
/// Wert, normalisiert, Verhalten auf dem Typ (Arithmetik mit Währungs-Guard). Plus Durchsatz.
/// </summary>
public class GeldwertTests
{
    [Fact]
    public void Gleich_per_Wert_nicht_per_Referenz()
    {
        Geldwert.Euro(10).Should().Be(Geldwert.Euro(10));
        Geldwert.Euro(10).Should().NotBe(Geldwert.Von(10, Waehrung.USD));
    }

    [Fact]
    public void Normalisiert_auf_zwei_Stellen_kaufmaennisch()
    {
        Geldwert.Euro(10.005m).Betrag.Should().Be(10.00m); // ToEven
        Geldwert.Euro(10.015m).Betrag.Should().Be(10.02m);
        Geldwert.Von(10.006m, Waehrung.USD).Betrag.Should().Be(10.01m); // jede Fabrik normalisiert
    }

    [Fact]
    public void Verhalten_rechnet_korrekt()
    {
        Geldwert.Euro(20).Mal(3).Should().Be(Geldwert.Euro(60));
        Geldwert.Euro(20).Plus(Geldwert.Euro(5)).Should().Be(Geldwert.Euro(25));
        Geldwert.Euro(20).GroesserAls(Geldwert.Euro(5)).Should().BeTrue();
        Geldwert.Euro(5).KleinerAls(Geldwert.Euro(20)).Should().BeTrue();
    }

    [Fact]
    public void Waehrungsmix_ist_ein_Programmierfehler()
    {
        var akt = () => Geldwert.Euro(10).Plus(Geldwert.Von(10, Waehrung.USD));
        akt.Should().Throw<InvalidOperationException>().WithMessage("*gemischt*");
        Geldwert.Euro(10).GleicheWaehrung(Geldwert.Von(10, Waehrung.USD)).Should().BeFalse();
    }

    [Fact]
    public void Arithmetik_ist_millionenfach_billig()
    {
        const int N = 1_000_000;
        var sw = Stopwatch.StartNew();
        var summe = Geldwert.Null(Waehrung.EUR);
        var eins = Geldwert.Euro(1);
        for (var i = 0; i < N; i++) summe = summe.Plus(eins);
        sw.Stop();

        summe.Should().Be(Geldwert.Euro(N));
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3),
            $"1M Additionen waren {sw.Elapsed.TotalMilliseconds:N0} ms");
    }
}
