using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Infrastructure;   // generierte AggregateHandlerFactory

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Das Ziel-Aggregat des Überweisungs-Prozesses (Konto) — REIN, mengenbasiert, ohne Prozess-Wissen.
/// In-memory über die generierte <see cref="AggregateHandlerFactory"/> (Decider+Applier), ohne Cluster/Store.
///
/// Bewiesen: die Schritte bewegen Saldo/Reserviert korrekt (Happy Path + Kompensation), und der fachliche
/// Fehlerfall liefert eine Ablehnung (ITransientEvent). Idempotenz/Dedup ist NICHT Sache der Domäne mehr —
/// das sichert die Framework-Inbox (separat getestet).
/// </summary>
public class KontoAggregatTests
{
    private sealed class Fixture
    {
        public IAggregateHandler Handler { get; }
        public Konto Konto { get; }
        public List<IEvent> LetzteAblehnungen { get; } = new();

        public Fixture(decimal startSaldo, bool gesperrt = false)
        {
            Konto = new Konto();
            Handler = new AggregateHandlerFactory().CreateHandler(Konto);
            Wende(new EroeffneKonto(Guid.NewGuid(), startSaldo, gesperrt));
        }

        public IReadOnlyList<IEvent> Wende(ICommand cmd)
        {
            LetzteAblehnungen.Clear();
            var events = Handler.HandleCommand(cmd).ToList();
            foreach (var e in events)
            {
                if (e is ITransientEvent) { LetzteAblehnungen.Add(e); continue; }
                Handler.ApplyEvent(e);
                Konto.Version++;   // das macht im Betrieb der AggregateActorBase
            }
            return events;
        }
    }

    [Fact]
    public void Reservieren_dann_Buchen_bewegt_den_Saldo()
    {
        var f = new Fixture(startSaldo: 100);
        var id = f.Konto.Id;

        f.Wende(new ReserviereBetrag(id, 30));
        f.Konto.Reserviert.Should().Be(30);
        f.Konto.Verfuegbar.Should().Be(70);
        f.Konto.Saldo.Should().Be(100, "reservieren bewegt den Saldo noch nicht");

        f.Wende(new BucheReservierung(id, 30));
        f.Konto.Reserviert.Should().Be(0);
        f.Konto.Saldo.Should().Be(70, "gebucht");
    }

    [Fact]
    public void Zu_wenig_Deckung_wird_abgelehnt_ohne_Wirkung()
    {
        var f = new Fixture(startSaldo: 20);

        f.Wende(new ReserviereBetrag(f.Konto.Id, 30));

        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<DeckungReichtNicht>();
        f.Konto.Reserviert.Should().Be(0, "die Ablehnung wirkt nicht");
        f.Konto.Saldo.Should().Be(20);
    }

    [Fact]
    public void Kompensation_gibt_die_Reservierung_frei_und_radiert_nicht()
    {
        var f = new Fixture(startSaldo: 100);
        var id = f.Konto.Id;

        f.Wende(new ReserviereBetrag(id, 30));
        f.Wende(new GebeReservierungFrei(id, 30));

        f.Konto.Reserviert.Should().Be(0, "die Reservierung wurde ausgeglichen");
        f.Konto.Saldo.Should().Be(100, "nichts gebucht — der Saldo ist unberührt");
        f.Konto.Version.Should().Be(3, "Eröffnen + Reservieren + Freigeben — die Geschichte wächst, nichts radiert");
    }

    [Fact]
    public void Gesperrtes_Konto_lehnt_das_Gutschreiben_ab()
    {
        var f = new Fixture(startSaldo: 0, gesperrt: true);

        f.Wende(new SchreibeGut(f.Konto.Id, 30));

        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<KontoGesperrt>();
        f.Konto.Saldo.Should().Be(0);
    }
}
