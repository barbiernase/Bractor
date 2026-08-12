using System.Diagnostics;
using Domain.DddPatterns.Gemeinsam;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Baustein VALUE OBJECT — belegt die drei tragenden Eigenschaften (unveränderlich,
/// selbstvalidierend, gleich per Wert) und dass das Verhalten IM Wertobjekt lebt.
/// Plus ein Durchsatz-Test: Wertobjekte müssen millionenfach billig sein.
/// </summary>
public class ValueObjectTests
{
    // ── Gültigkeit ──

    [Fact]
    public void Geld_ist_gleich_per_Wert_nicht_per_Referenz()
    {
        Geld.Von(10.00m, Waehrung.EUR).Should().Be(Geld.Von(10.00m, Waehrung.EUR));
        Geld.Von(10.00m, Waehrung.EUR).Should().NotBe(Geld.Von(10.00m, Waehrung.USD));
    }

    [Fact]
    public void Geld_rundet_kaufmaennisch_auf_zwei_Stellen()
    {
        Geld.Von(10.005m, Waehrung.EUR).Betrag.Should().Be(10.00m); // ToEven
        Geld.Von(10.015m, Waehrung.EUR).Betrag.Should().Be(10.02m);
    }

    [Fact]
    public void Geld_verweigert_negative_Betraege()
    {
        var akt = () => Geld.Von(-1m, Waehrung.EUR);
        akt.Should().Throw<DomaeneException>().WithMessage("*nicht negativ*");
    }

    [Fact]
    public void Geld_verweigert_das_Mischen_von_Waehrungen()
    {
        var akt = () => Geld.Von(10m, Waehrung.EUR).Plus(Geld.Von(10m, Waehrung.USD));
        akt.Should().Throw<DomaeneException>().WithMessage("*gemischt*");
    }

    [Fact]
    public void Geld_Minus_darf_nicht_unter_null_fallen()
    {
        var akt = () => Geld.Von(5m, Waehrung.EUR).Minus(Geld.Von(6m, Waehrung.EUR));
        akt.Should().Throw<DomaeneException>();
    }

    [Fact]
    public void Geld_Verhalten_rechnet_korrekt()
    {
        var preis = Geld.Von(20m, Waehrung.EUR);
        preis.Mal(3).Should().Be(Geld.Von(60m, Waehrung.EUR));
        preis.Anteil(Prozent.Von(19m)).Should().Be(Geld.Von(3.80m, Waehrung.EUR));
        preis.Plus(Geld.Von(5m, Waehrung.EUR)).Should().Be(Geld.Von(25m, Waehrung.EUR));
    }

    [Fact]
    public void Prozent_haelt_seine_Grenzen_ein()
    {
        Prozent.Von(0m).AlsFaktor.Should().Be(0m);
        Prozent.Von(100m).AlsFaktor.Should().Be(1m);
        Assert.Throws<DomaeneException>(() => Prozent.Von(-1m));
        Assert.Throws<DomaeneException>(() => Prozent.Von(101m));
    }

    [Fact]
    public void Emailadresse_normalisiert_und_validiert()
    {
        Emailadresse.Von("  Tobias.Arndt@Example.COM ").Wert.Should().Be("tobias.arndt@example.com");
        Emailadresse.Von("a@b.de").Should().Be(Emailadresse.Von("A@B.DE")); // gleich per Wert nach Normalisierung
        Assert.Throws<DomaeneException>(() => Emailadresse.Von("kein-at-zeichen"));
        Assert.Throws<DomaeneException>(() => Emailadresse.Von("zwei@@punkte.de"));
        Assert.Throws<DomaeneException>(() => Emailadresse.Von("ohne@punkt"));
    }

    // ── Performance ──

    [Fact]
    public void Geld_Arithmetik_ist_millionenfach_billig()
    {
        const int N = 1_000_000;
        var sw = Stopwatch.StartNew();

        var summe = Geld.Null(Waehrung.EUR);
        var eins = Geld.Von(1m, Waehrung.EUR);
        for (var i = 0; i < N; i++)
            summe = summe.Plus(eins);

        sw.Stop();

        summe.Should().Be(Geld.Von(N, Waehrung.EUR), "korrekt bei voller Skalierung — kein Überlauf, keine Rundungsdrift");
        // Großzügiges Wall-Clock-Budget (flackert nicht unter paralleler Test-Last, fängt aber
        // katastrophale Regressionen). Gemessen liegt es weit darunter (~1M Ops in << 1 s).
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3),
            $"1M Value-Object-Additionen waren {sw.Elapsed.TotalMilliseconds:N0} ms ({N / sw.Elapsed.TotalSeconds:N0}/s)");
    }
}
