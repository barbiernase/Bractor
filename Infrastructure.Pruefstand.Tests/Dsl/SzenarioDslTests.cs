using Abstractions;
using Cqrs.Testing;
using Domain.Konto;
using FluentAssertions;
using Infrastructure; // generierte AggregateHandlerFactory

namespace Infrastructure.Pruefstand.Dsl;

/// <summary>
/// Dogfooding der Test-/Debug-DSL gegen das Konto-Aggregat — dieselben Fälle wie
/// <c>Phase5/KontoAggregatTests</c>, aber durch <c>Gegeben → Wenn → Dann</c> statt den
/// handgeschriebenen Fixture-Loop. Plus DSL-eigene Beweise: die zwei Given-Formen sind
/// äquivalent, ein abgelehntes Vorab-Command wird laut, und die Trace ist inspizierbar.
///
/// Store-frei über die generierte <see cref="AggregateHandlerFactory"/> — kein Cluster, kein Marten.
/// </summary>
public class SzenarioDslTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new AggregateHandlerFactory();

    private static SzenarioBauer<Konto> EinKonto(Guid id) => Szenario.Für<Konto>(Fabrik, id);

    [Fact]
    public void Reservieren_dann_Buchen_bewegt_den_Saldo()
    {
        var id = Guid.NewGuid();

        EinKonto(id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Vorab(new ReserviereBetrag(id, 30))
            .Wenn(new BucheReservierung(id, 30))
            .Dann<ReservierungGebucht>()
            .UndZustand(k => k.Reserviert == 0, "gebucht → nichts mehr reserviert")
            .UndZustand(k => k.Saldo == 70, "der Saldo ist jetzt bewegt");
    }

    [Fact]
    public void Zu_wenig_Deckung_wird_abgelehnt_ohne_Wirkung()
    {
        var id = Guid.NewGuid();

        EinKonto(id)
            .Gegeben(new KontoEroeffnet(20, Gesperrt: false))
            .Wenn(new ReserviereBetrag(id, 30))
            .DannAbgelehnt<DeckungReichtNicht>()
            .UndZustand(k => k.Reserviert == 0, "die Ablehnung wirkt nicht")
            .UndZustand(k => k.Saldo == 20);
    }

    [Fact]
    public void Gesperrtes_Konto_lehnt_das_Gutschreiben_ab()
    {
        var id = Guid.NewGuid();

        EinKonto(id)
            .Gegeben(new KontoEroeffnet(0, Gesperrt: true))
            .Wenn(new SchreibeGut(id, 30))
            .DannAbgelehnt<KontoGesperrt>()
            .UndZustand(k => k.Saldo == 0);
    }

    [Fact]
    public void Kompensation_gibt_die_Reservierung_frei_und_radiert_nicht()
    {
        var id = Guid.NewGuid();

        var ergebnis = EinKonto(id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Vorab(new ReserviereBetrag(id, 30))
            .Wenn(new GebeReservierungFrei(id, 30))
            .Dann<ReservierungFreigegeben>()
            .UndZustand(k => k.Reserviert == 0)
            .UndZustand(k => k.Saldo == 100, "nichts gebucht — der Saldo ist unberührt");

        // Eröffnen + Reservieren + Freigeben — die Geschichte wächst, nichts radiert.
        ergebnis.Zustand.Version.Should().Be(3);
    }

    [Fact]
    public void Dann_prueft_den_persistenten_Ausgang_exakt_auf_den_Wert()
    {
        var id = Guid.NewGuid();

        EinKonto(id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Wenn(new ReserviereBetrag(id, 30))
            .Dann(new BetragReserviert(30));
    }

    [Fact]
    public void Given_Events_und_Vorab_Commands_erzeugen_denselben_Zustand()
    {
        var id = Guid.NewGuid();

        // Weg A: Historie als EVENTS geseedet (kanonisch, Log = Wahrheit).
        var überEvents = EinKonto(id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false), new BetragReserviert(30))
            .Wenn(new BucheReservierung(id, 30))
            .Dann<ReservierungGebucht>();

        // Weg B: dieselbe Historie als COMMANDS gefahren (board-nah).
        var überCommands = EinKonto(id)
            .Vorab(new EroeffneKonto(id, 100), new ReserviereBetrag(id, 30))
            .Wenn(new BucheReservierung(id, 30))
            .Dann<ReservierungGebucht>();

        // Beide Wege landen im selben Endzustand.
        überEvents.Zustand.Saldo.Should().Be(überCommands.Zustand.Saldo);
        überEvents.Zustand.Reserviert.Should().Be(überCommands.Zustand.Reserviert);
        überEvents.Zustand.Saldo.Should().Be(70);
    }

    [Fact]
    public void Ein_abgelehntes_Vorab_Command_wird_laut()
    {
        var id = Guid.NewGuid();

        Action act = () => EinKonto(id)
            .Gegeben(new KontoEroeffnet(20, Gesperrt: false))
            .Vorab(new ReserviereBetrag(id, 30))   // 30 auf 20 → DeckungReichtNicht im Arrange
            .Wenn(new BucheReservierung(id, 30));

        act.Should().Throw<SzenarioFehler>()
            .WithMessage("*abgelehnt*")
            .WithMessage("*DeckungReichtNicht*");
    }

    [Fact]
    public void Die_Trace_haelt_den_Entscheidungs_Pfad_inspizierbar_fest()
    {
        var id = Guid.NewGuid();

        var trace = EinKonto(id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Vorab(new ReserviereBetrag(id, 30))
            .Wenn(new ReserviereBetrag(id, 200))   // 200 auf verfügbare 70 → Ablehnung
            .DannAbgelehnt<DeckungReichtNicht>()
            .Trace;

        trace.Schritte.Should().HaveCount(2);
        trace.Schritte[0].Herkunft.Should().Be(SchrittHerkunft.Vorab);
        trace.Letzter.Herkunft.Should().Be(SchrittHerkunft.Wenn);

        // Der Wenn-Schritt: der Zustand-vorher trägt die Begründung (Verfuegbar 70 < 200).
        trace.Letzter.ZustandVorher["Verfuegbar"].Should().Be(70m);
        trace.Letzter.Ablehnungen.Should().ContainSingle();
        trace.Letzter.ZustandNachher["Reserviert"].Should().Be(30m, "eine Ablehnung wirkt nicht");

        // Der Bericht ist menschenlesbar und nennt Zweig + Begründung.
        trace.AlsText().Should().Contain("ABGELEHNT").And.Contain("DeckungReichtNicht");
    }
}
