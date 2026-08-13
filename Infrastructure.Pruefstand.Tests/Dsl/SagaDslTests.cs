using Abstractions;
using Cqrs.Testing;
using Domain.Auftrag;
using Domain.Konto;
using Domain.Ueberweisung;
using Domain.Willkommensbonus;
using FluentAssertions;
using Infrastructure; // generierte AggregateHandlerFactory

namespace Infrastructure.Pruefstand.Dsl;

/// <summary>
/// Dogfooding der Saga-/Prozess-DSL am Überweisungs-Diamant (reservieren → gutschreiben →
/// buchen, mit Join). Store-frei über die echten <see cref="UeberweisungsProzess"/>-Regeln +
/// generierte Aggregat-Handler — dieselbe Kaskade wie SimEngine, nur mit Assertions.
///
/// Bewiesen: der Happy-Pfad feuert die drei Commands in Join-korrekter Reihenfolge und bewegt
/// beide Konten; und bei zu wenig Deckung bleibt die Saga am Join hängen (halbgefülltes Join) —
/// genau die Sicht, die den #1-Saga-Bug sichtbar macht.
/// </summary>
public class SagaDslTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new AggregateHandlerFactory();

    private static SagaBauer EineUeberweisung() =>
        SagaSzenario.Mit(Fabrik, new UeberweisungsProzess());

    [Fact]
    public void Diamant_happy_feuert_join_korrekt_und_bewegt_beide_Konten()
    {
        var quelle = Guid.NewGuid();
        var ziel = Guid.NewGuid();
        var auftrag = Guid.NewGuid();

        EineUeberweisung()
            .Gegeben<Konto>(quelle, new KontoEroeffnet(100, Gesperrt: false))
            .Gegeben<Konto>(ziel, new KontoEroeffnet(0, Gesperrt: false))
            .Wenn(new BeauftrageUeberweisung(auftrag, quelle, ziel, 50))
            .FeuertInReihenfolge<ReserviereBetrag, SchreibeGut, BucheReservierung>()
            .KeinUnroutedCommand()
            .KeineOffeneSaga()
            .EndzustandVon<Konto>(quelle, k => k.Saldo == 50 && k.Reserviert == 0, "quelle: 100 − 50 gebucht")
            .EndzustandVon<Konto>(ziel, k => k.Saldo == 50, "ziel: 0 + 50 gutgeschrieben");
    }

    [Fact]
    public void Zu_wenig_Deckung_laesst_die_Saga_am_Join_haengen()
    {
        var quelle = Guid.NewGuid();
        var ziel = Guid.NewGuid();
        var auftrag = Guid.NewGuid();

        EineUeberweisung()
            .Gegeben<Konto>(quelle, new KontoEroeffnet(20, Gesperrt: false)) // deckt die 50 nicht
            .Gegeben<Konto>(ziel, new KontoEroeffnet(0, Gesperrt: false))
            .Wenn(new BeauftrageUeberweisung(auftrag, quelle, ziel, 50))
            .Feuert<ReserviereBetrag>()             // die Saga versucht zu reservieren …
            .FeuertNicht<SchreibeGut>()             // … scheitert (DeckungReichtNicht) → kein BetragReserviert
            .FeuertNicht<BucheReservierung>()
            .SagaWartetAuf<BetragReserviert>("UeberweisungsProzess")  // halbgefülltes Join
            .EndzustandVon<Konto>(quelle, k => k.Saldo == 20, "nichts wurde bewegt");
    }

    [Fact]
    public void Willkommensbonus_wird_vom_Promo_Konto_ueberwiesen()
    {
        var neu = Guid.NewGuid();
        var promo = Guid.NewGuid();
        var auftrag = Guid.NewGuid();

        SagaSzenario.Mit(Fabrik, new WillkommensbonusProzess())
            .Gegeben<Konto>(promo, new KontoEroeffnet(1000, Gesperrt: false)) // die Bank-Kasse
            .Gegeben<Konto>(neu, new KontoEroeffnet(0, Gesperrt: false))
            .Wenn(new BeauftrageWillkommensbonus(auftrag, neu, promo, 50))
            .FeuertInReihenfolge<ReserviereBetrag, SchreibeGut, BucheReservierung>()
            .KeineOffeneSaga()
            .EndzustandVon<Konto>(promo, k => k.Saldo == 950, "Promo-Konto zahlt den Bonus")
            .EndzustandVon<Konto>(neu, k => k.Saldo == 50, "neues Konto bekommt den Bonus");
    }

    [Fact]
    public void Auslöser_Event_direkt_startet_dieselbe_Kaskade()
    {
        var quelle = Guid.NewGuid();
        var ziel = Guid.NewGuid();

        EineUeberweisung()
            .Gegeben<Konto>(quelle, new KontoEroeffnet(100, Gesperrt: false))
            .Gegeben<Konto>(ziel, new KontoEroeffnet(0, Gesperrt: false))
            .WennAusgelöst(new UeberweisungBeauftragt(quelle, ziel, 50), korrelation: Guid.NewGuid())
            .FeuertInReihenfolge<ReserviereBetrag, SchreibeGut, BucheReservierung>()
            .KeineOffeneSaga();
    }

    [Fact]
    public void Die_Saga_Trace_haelt_Feuerungen_und_offene_Joins_fest()
    {
        var quelle = Guid.NewGuid();
        var ziel = Guid.NewGuid();

        var trace = EineUeberweisung()
            .Gegeben<Konto>(quelle, new KontoEroeffnet(20, Gesperrt: false))
            .Gegeben<Konto>(ziel, new KontoEroeffnet(0, Gesperrt: false))
            .Wenn(new BeauftrageUeberweisung(Guid.NewGuid(), quelle, ziel, 50))
            .Trace;

        trace.Feuerungen.Should().ContainSingle(c => c is ReserviereBetrag);
        trace.Markierungen.Should().ContainSingle();
        trace.Markierungen[0].Wartend.Should().NotBeEmpty("das Join blieb offen");
        trace.AlsText().Should().Contain("halbgefülltes Join").And.Contain("BetragReserviert");
    }
}
