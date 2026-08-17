using Abstractions;
using Domain.Trainingslauf;
using FluentAssertions;
using Infrastructure;     // generierte AggregateHandlerFactory
using TrainingslaufAgg = Domain.Trainingslauf.Trainingslauf;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// Das Aggregat <see cref="TrainingslaufAgg"/> (Meilenstein 6) durch die ECHTE generierte
/// <see cref="AggregateHandlerFactory"/> (Decider + Applier), store-frei. Deckt den
/// Lebenszyklus + den Fortschritts-Fold (mehrere MeldeFortschritt → MetrikHistorie) und die
/// terminalen Übergänge ab (Konzept §4).
/// </summary>
public class TrainingslaufAggregatTests
{
    private static Hyperparameter StdHp => new(Epochen: 10, LernRate: 0.001, BatchGroesse: 32, Architektur: "resnet18", Seed: 42);

    private sealed class Fixture
    {
        public IAggregateHandler Handler { get; }
        public TrainingslaufAgg Lauf { get; }
        public Guid Id { get; }
        public List<IEvent> LetzteAblehnungen { get; } = new();

        public Fixture(bool starten = true)
        {
            Lauf = new TrainingslaufAgg();
            Handler = new AggregateHandlerFactory().CreateHandler(Lauf);
            Id = Guid.NewGuid();
            if (starten)
                Wende(new StarteTraining(Id, Guid.NewGuid(), 1, StdHp));
        }

        public IReadOnlyList<IEvent> Wende(ICommand cmd)
        {
            LetzteAblehnungen.Clear();
            var events = Handler.HandleCommand(cmd).ToList();
            foreach (var e in events)
            {
                if (e is ITransientEvent) { LetzteAblehnungen.Add(e); continue; }
                Handler.ApplyEvent(e);
                Lauf.Version++;   // das macht im Betrieb die AggregateActorBase
            }
            return events;
        }

        public void Begonnen() => Wende(new MeldeTrainingBegonnen(Id));
    }

    // ═══════════════════════════════════════════════════
    // START
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Starten_setzt_Angefordert_und_haelt_den_Input()
    {
        var datensatzId = Guid.NewGuid();
        var f = new Fixture(starten: false);
        f.Wende(new StarteTraining(f.Id, datensatzId, 3, StdHp));

        f.Lauf.Status.Should().Be(TrainingsStatus.Angefordert);
        f.Lauf.DatensatzId.Should().Be(datensatzId);
        f.Lauf.DatensatzVersion.Should().Be(3);
        f.Lauf.Hyperparameter.Should().Be(StdHp);
    }

    [Fact]
    public void Zweimal_Starten_wird_abgelehnt()
    {
        var f = new Fixture();
        f.Wende(new StarteTraining(f.Id, Guid.NewGuid(), 1, StdHp));
        f.LetzteAblehnungen.Should().ContainSingle()
            .Which.Should().BeOfType<TrainingslaufExistiertBereits>();
    }

    // ═══════════════════════════════════════════════════
    // BEGINN
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Begonnen_setzt_Laeuft_und_ist_idempotent()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Lauf.Status.Should().Be(TrainingsStatus.Laeuft);

        var wieder = f.Wende(new MeldeTrainingBegonnen(f.Id));
        wieder.Should().BeEmpty("doppelt zugestelltes Begonnen erzeugt kein zweites Event");
    }

    // ═══════════════════════════════════════════════════
    // FORTSCHRITT — der Fold (Akzeptanz §5)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Fortschritt_foldet_in_die_MetrikHistorie()
    {
        var f = new Fixture();
        f.Begonnen();

        for (var epoche = 1; epoche <= 5; epoche++)
            f.Wende(new MeldeFortschritt(f.Id, new EpochenMetrik(epoche, Loss: 1.0 / epoche, Genauigkeit: 0.1 * epoche)));

        f.Lauf.MetrikHistorie.Should().HaveCount(5);
        f.Lauf.MetrikHistorie.Select(m => m.Epoche).Should().Equal(1, 2, 3, 4, 5);
        f.Lauf.AktuelleEpoche.Should().Be(5);
        f.Lauf.MetrikHistorie.Last().Genauigkeit.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Fortschritt_vor_Beginn_wird_abgelehnt()
    {
        var f = new Fixture();   // Angefordert, noch nicht begonnen
        f.Wende(new MeldeFortschritt(f.Id, new EpochenMetrik(1, 1.0, 0.1)));
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<TrainingNichtAktiv>();
        f.Lauf.MetrikHistorie.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════
    // TERMINALE ÜBERGÄNGE
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Abschliessen_setzt_Modellpfad_und_Endmetriken()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Wende(new MeldeTrainingAbgeschlossen(f.Id, "modelle/m1.pt", new Endmetriken(Loss: 0.05, Genauigkeit: 0.97)));

        f.Lauf.Status.Should().Be(TrainingsStatus.Abgeschlossen);
        f.Lauf.ModellPfad.Should().Be("modelle/m1.pt");
        f.Lauf.Endmetriken.Should().Be(new Endmetriken(0.05, 0.97));
    }

    [Fact]
    public void Nach_Abschluss_wird_jede_weitere_Meldung_abgelehnt()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Wende(new MeldeTrainingAbgeschlossen(f.Id, "m.pt", new Endmetriken(0.05, 0.97)));

        f.Wende(new MeldeFortschritt(f.Id, new EpochenMetrik(6, 0.9, 0.2)));
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<TrainingBereitsBeendet>();
    }

    [Fact]
    public void Scheitern_haelt_den_Grund()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Wende(new MeldeTrainingGescheitert(f.Id, "CUDA out of memory"));

        f.Lauf.Status.Should().Be(TrainingsStatus.Gescheitert);
        f.Lauf.Fehlergrund.Should().Be("CUDA out of memory");
    }

    [Fact]
    public void Abbrechen_ist_idempotent()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Wende(new BricheTrainingAb(f.Id));
        f.Lauf.Status.Should().Be(TrainingsStatus.Abgebrochen);

        var wieder = f.Wende(new BricheTrainingAb(f.Id));
        wieder.Should().BeEmpty("zweiter Abbruch ist ein Noop");
    }

    // ═══════════════════════════════════════════════════
    // FRIST — späte Frist überschreibt kein beendetes Training
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Haengengeblieben_markiert_ein_laufendes_Training()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Wende(new MarkiereAlsHaengengeblieben(f.Id));
        f.Lauf.Status.Should().Be(TrainingsStatus.Haengengeblieben);
    }

    [Fact]
    public void Spaete_Frist_ueberschreibt_ein_abgeschlossenes_Training_nicht()
    {
        var f = new Fixture();
        f.Begonnen();
        f.Wende(new MeldeTrainingAbgeschlossen(f.Id, "m.pt", new Endmetriken(0.05, 0.97)));

        var events = f.Wende(new MarkiereAlsHaengengeblieben(f.Id));
        events.Should().BeEmpty("eine späte Frist ist auf ein beendetes Training wirkungslos");
        f.Lauf.Status.Should().Be(TrainingsStatus.Abgeschlossen);
    }

    // ═══════════════════════════════════════════════════
    // NICHT GEFUNDEN
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Meldung_auf_unbekannten_Lauf_wird_abgelehnt()
    {
        var f = new Fixture(starten: false);
        f.Wende(new MeldeTrainingBegonnen(f.Id));
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<TrainingslaufNichtGefunden>();
    }

    [Fact]
    public void Voller_Lebenszyklus_laeuft_durch()
    {
        var f = new Fixture();
        f.Begonnen();
        for (var e = 1; e <= 10; e++)
            f.Wende(new MeldeFortschritt(f.Id, new EpochenMetrik(e, 1.0 / e, 0.09 * e)));
        f.Wende(new MeldeTrainingAbgeschlossen(f.Id, "modelle/final.pt", new Endmetriken(0.03, 0.98)));

        f.Lauf.Status.Should().Be(TrainingsStatus.Abgeschlossen);
        f.Lauf.MetrikHistorie.Should().HaveCount(10);
        f.Lauf.AktuelleEpoche.Should().Be(10);
        f.Lauf.ModellPfad.Should().Be("modelle/final.pt");
    }
}
