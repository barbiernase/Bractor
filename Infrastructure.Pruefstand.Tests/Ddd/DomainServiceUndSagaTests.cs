using Domain.DddPatterns.Dienste;
using Domain.DddPatterns.Gemeinsam;
using Domain.DddPatterns.Prozess;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Bausteine DOMAIN SERVICE und SAGA / PROCESS MANAGER. Der Dienst rechnet Währungen
/// um (gehört zu keinem Aggregat); die Saga hält Konsistenz über zwei Aggregate und
/// kompensiert bei Fehlschlag des zweiten Schritts.
/// </summary>
public class DomainServiceUndSagaTests
{
    private static Geld Eur(decimal b) => Geld.Von(b, Waehrung.EUR);
    private static Geld Usd(decimal b) => Geld.Von(b, Waehrung.USD);

    // ── Domain Service ──

    private static WechselkursDienst Dienst() => new(new Dictionary<(Waehrung, Waehrung), decimal>
    {
        [(Waehrung.EUR, Waehrung.USD)] = 1.10m,
        [(Waehrung.USD, Waehrung.EUR)] = 0.90m,
    });

    [Fact]
    public void Wechselkurs_rechnet_um_und_bleibt_bei_gleicher_Waehrung_unveraendert()
    {
        var dienst = Dienst();
        dienst.Rechne(Eur(100), Waehrung.USD).Should().Be(Usd(110));
        dienst.Rechne(Eur(100), Waehrung.EUR).Should().Be(Eur(100));
    }

    [Fact]
    public void Wechselkurs_verlangt_einen_hinterlegten_Kurs()
    {
        var akt = () => Dienst().Rechne(Eur(100), Waehrung.CHF);
        akt.Should().Throw<DomaeneException>().WithMessage("*Wechselkurs*");
    }

    // ── Saga: Happy Path ──

    [Fact]
    public void Saga_schliesst_ab_wenn_beide_Schritte_gelingen()
    {
        var lager = new Lagerbestand(anfangsbestand: 10);
        var zahlung = new Zahlungsvorgang(Eur(1000));
        var saga = new BestellAbwicklungsSaga(lager, zahlung);

        var erfolg = saga.Wickle(menge: 3, preis: Eur(300));

        erfolg.Should().BeTrue();
        saga.Stand.Should().Be(SagaStand.Abgeschlossen);
        lager.Reserviert.Should().Be(3);
        lager.Verfuegbar.Should().Be(7);
        zahlung.Guthaben.Should().Be(Eur(700));
    }

    // ── Saga: Kompensation ──

    [Fact]
    public void Saga_kompensiert_die_Reservierung_wenn_die_Zahlung_scheitert()
    {
        var lager = new Lagerbestand(anfangsbestand: 10);
        var zahlung = new Zahlungsvorgang(Eur(100)); // zu wenig für 300
        var saga = new BestellAbwicklungsSaga(lager, zahlung);

        var erfolg = saga.Wickle(menge: 3, preis: Eur(300));

        erfolg.Should().BeFalse();
        saga.Stand.Should().Be(SagaStand.Kompensiert);
        lager.Reserviert.Should().Be(0, "die Reservierung wurde rückgängig gemacht");
        lager.Verfuegbar.Should().Be(10, "der Bestand ist wie vor der Saga");
        zahlung.Guthaben.Should().Be(Eur(100), "unbelastet");
        saga.Protokoll.Should().ContainMatch("*kompensiert*");
    }

    [Fact]
    public void Saga_wirft_wenn_schon_der_erste_Schritt_fachlich_scheitert()
    {
        var lager = new Lagerbestand(anfangsbestand: 1);
        var zahlung = new Zahlungsvorgang(Eur(1000));
        var saga = new BestellAbwicklungsSaga(lager, zahlung);

        var akt = () => saga.Wickle(menge: 5, preis: Eur(10)); // Bestand reicht nicht
        akt.Should().Throw<DomaeneException>().WithMessage("*Bestand*");
        saga.Stand.Should().Be(SagaStand.Gestartet, "nichts wurde reserviert, nichts zu kompensieren");
    }
}
