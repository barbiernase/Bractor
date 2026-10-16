using FluentAssertions;
using Infrastructure.Persistence;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Der Horizont der Vollständigkeit ohne Uhr (G-9, docs/11 §11.4): eine Probe (Zählerstand, Nummer der Probe-Transaktion) wird frei, sobald
/// keine kleinere Nummer mehr läuft — nie früher, egal wie lange es dauert. Die Abfragen dazu stellt der Store; hier nur die Regel.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class HorizontTests
{
    [Fact]
    public void Ohne_laufende_Transaktion_ist_die_Probe_sofort_frei()
    {
        var h = new Horizont();
        // Die älteste laufende ist die eigene Probe selbst (oder größer): nichts davor läuft.
        h.Probe(stand: 100, nummer: 50, aeltesteLaufende: 50).Should().Be(100);
        h.Frei.Should().Be(100);
        h.AeltesteOffen.Should().BeNull();
    }

    [Fact]
    public void Eine_laufende_aeltere_Transaktion_haelt_den_Horizont_an_bis_sie_endet()
    {
        var h = new Horizont();
        h.Probe(stand: 10, nummer: 5, aeltesteLaufende: 5);

        // Transaktion 7 hat eine Sequenz bis 20 gezogen und läuft noch; die Probe hat Nummer 9.
        h.Probe(stand: 20, nummer: 9, aeltesteLaufende: 7).Should().Be(10, "Nummer 7 < 9 läuft noch — ihr Event könnte bis 20 liegen");
        h.Wartet(20).Should().BeTrue();

        // Beliebig viel später, Transaktion 7 läuft immer noch: der Horizont bleibt stehen — warten statt überholen.
        h.Pruefe(aeltesteLaufende: 7).Should().Be(10);

        // Transaktion 7 ist beendet; die älteste laufende ist jetzt 12 (jünger als die Probe).
        h.Pruefe(aeltesteLaufende: 12).Should().Be(20);
        h.Wartet(20).Should().BeFalse();
    }

    [Fact]
    public void Eine_spaetere_Probe_kann_frei_werden_und_nimmt_die_aelteren_mit()
    {
        var h = new Horizont();
        h.Probe(stand: 10, nummer: 4, aeltesteLaufende: 3);   // 3 läuft
        h.Probe(stand: 15, nummer: 8, aeltesteLaufende: 3);   // 3 läuft noch
        h.Frei.Should().Be(0);

        h.Pruefe(aeltesteLaufende: 8).Should().Be(15, "alles unter 8 ist beendet — beide Proben sind frei, die höhere zählt");
        h.AeltesteOffen.Should().BeNull();
    }

    [Fact]
    public void Der_Horizont_geht_nie_zurueck()
    {
        var h = new Horizont();
        h.Probe(stand: 30, nummer: 10, aeltesteLaufende: 10).Should().Be(30);
        // Eine verspätet ankommende, ältere Probe (nebenläufige Leser) ändert nichts.
        h.Probe(stand: 25, nummer: 9, aeltesteLaufende: 9).Should().Be(30);
        h.Pruefe(aeltesteLaufende: 1).Should().Be(30);
    }

    [Fact]
    public void Zu_viele_offene_Proben_verlieren_die_aelteste_nur_auf_Kosten_der_Frische()
    {
        var h = new Horizont();
        for (var i = 1; i <= Horizont.HoechstensOffen + 10; i++)
            h.Probe(stand: i * 10, nummer: (ulong)(100 + i), aeltesteLaufende: 1);   // Nummer 1 läuft die ganze Zeit
        h.Frei.Should().Be(0, "solange Nummer 1 läuft, ist keine Probe frei");

        h.Pruefe(aeltesteLaufende: 1_000).Should().Be((Horizont.HoechstensOffen + 10) * 10, "die jüngste Probe ist erhalten geblieben");
    }

    [Fact]
    public async Task Proben_kommen_hoechstens_im_Abstand_und_die_hoechste_offene_ist_bekannt()
    {
        var h = new Horizont();
        h.ProbeFaellig().Should().BeTrue();
        h.ProbeFaellig().Should().BeFalse("eben erst geprobt");
        await Task.Delay(Horizont.ProbenAbstand + TimeSpan.FromMilliseconds(5));
        h.ProbeFaellig().Should().BeTrue();

        h.HoechsteOffene.Should().BeNull();
        h.Probe(stand: 10, nummer: 4, aeltesteLaufende: 3);
        h.Probe(stand: 20, nummer: 6, aeltesteLaufende: 3);
        h.HoechsteOffene.Should().Be(20);
        h.Pruefe(aeltesteLaufende: 6);
        h.HoechsteOffene.Should().BeNull();
    }

    [Fact]
    public void Jeder_Store_hat_genau_einen_Horizont()
    {
        var a = new object();
        var b = new object();
        Horizont.Fuer(a).Should().BeSameAs(Horizont.Fuer(a));
        Horizont.Fuer(a).Should().NotBeSameAs(Horizont.Fuer(b));
    }
}
