using System.Diagnostics;
using Domain.Spezifikation;
using Domain.Verkauf;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Baustein SPECIFICATION — atomare Regeln komponieren zu einer zusammengesetzten Regel
/// (Und/Oder/Nicht), die dieselbe Wahrheit liefert wie die ausgeschriebene Bedingung.
/// Plus Durchsatz über eine große Menge.
/// </summary>
public class SpezifikationTests
{
    private static readonly Geldwert Schwelle = Geldwert.Euro(10_000);

    private static Kunde Kunde(Kundenstufe stufe, decimal umsatz, int reklamationen = 0) =>
        new(Guid.NewGuid(), stufe, Geldwert.Euro(umsatz), reklamationen);

    [Fact]
    public void Und_Oder_Nicht_kombinieren_korrekt()
    {
        var premium = new IstPremium();
        var reklamationsfrei = new OhneOffeneReklamationen();

        premium.Und(reklamationsfrei).IstErfuellt(Kunde(Kundenstufe.Premium, 0)).Should().BeTrue();
        premium.Und(reklamationsfrei).IstErfuellt(Kunde(Kundenstufe.Premium, 0, reklamationen: 2)).Should().BeFalse();
        premium.Oder(reklamationsfrei).IstErfuellt(Kunde(Kundenstufe.Neu, 0)).Should().BeTrue();
        premium.Nicht().IstErfuellt(Kunde(Kundenstufe.Neu, 0)).Should().BeTrue();
    }

    [Theory]
    [InlineData(Kundenstufe.Premium, 0, 0, true)]
    [InlineData(Kundenstufe.Neu, 12_000, 0, true)]
    [InlineData(Kundenstufe.Neu, 9_000, 0, false)]
    [InlineData(Kundenstufe.Premium, 0, 1, false)]
    public void Zusammengesetzte_Regel_entscheidet_wie_die_ausgeschriebene_Bedingung(
        Kundenstufe stufe, decimal umsatz, int reklamationen, bool erwartet)
    {
        var kunde = Kunde(stufe, umsatz, reklamationen);
        var regel = KundenRegeln.Rabattberechtigt(Schwelle);
        var ausgeschrieben = (stufe == Kundenstufe.Premium || umsatz >= 10_000m) && reklamationen == 0;

        regel.IstErfuellt(kunde).Should().Be(erwartet);
        regel.IstErfuellt(kunde).Should().Be(ausgeschrieben, "die Spezifikation muss die Referenzbedingung spiegeln");
    }

    [Fact]
    public void Filtert_eine_grosse_Menge_performant()
    {
        const int N = 500_000;
        var regel = KundenRegeln.Rabattberechtigt(Schwelle);
        var kunden = new Kunde[N];
        for (var i = 0; i < N; i++)
            kunden[i] = Kunde((Kundenstufe)(i % 3), umsatz: i % 20_000, reklamationen: i % 7 == 0 ? 1 : 0);

        var sw = Stopwatch.StartNew();
        var treffer = 0;
        foreach (var k in kunden) if (regel.IstErfuellt(k)) treffer++;
        sw.Stop();

        treffer.Should().BeGreaterThan(0).And.BeLessThan(N);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3),
            $"{N:N0} komponierte Auswertungen waren {sw.Elapsed.TotalMilliseconds:N0} ms");
    }
}
