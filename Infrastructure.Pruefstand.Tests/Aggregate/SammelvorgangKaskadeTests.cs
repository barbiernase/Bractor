using System;
using Abstractions;
using Cqrs.Testing;
using FluentAssertions;
using Domain.Pipeline.Sammelvorgang;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;
using Domain.Trainingslauf;
using Xunit;

namespace Infrastructure.Pruefstand.Aggregate;

/// <summary>
/// End-to-end (store-frei, echte generierte Logik über die Ablauf-DSL <see cref="Ablauf"/>): Fan-out → Fan-in.
///
/// So SETZT man die Barriere ein:
///   1. Fan-out (Client): <c>StarteSammelvorgang(N)</c> + N × <c>StarteTeilauftrag(…, sammelvorgang)</c>.
///   2. Jeder Teil wird fertig → die Funktion <see cref="TeilFertigMeldung"/> macht daraus <c>MeldeTeilFertig</c>.
///   3. Das Zähl-Aggregat schließt erst beim N-ten Teil ab (N = Laufzeitwert).
/// Dazu der Trainings-Wächter als Frist des Aggregats: der Beginn setzt sie, die Uhr feuert sie, ein Ende davor macht sie wirkungslos.
/// </summary>
public class SammelvorgangKaskadeTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Domain.AggregateHandlerFactory();

    private static AblaufBauer Welt() => Ablauf.Mit(Fabrik)
        .Aggregat<Sammelvorgang>().Aggregat<Teilauftrag>()
        .Funktion<TeilauftragAbgeschlossen>(nameof(TeilFertigMeldung), (e, h) => new TeilFertigMeldung().Auf(e));

    [Fact]
    public void Fanout_dann_Fanin_Barriere_feuert_erst_beim_letzten_Teil()
    {
        var sammel = Guid.NewGuid();
        var w1 = Guid.NewGuid();
        var w2 = Guid.NewGuid();
        var w3 = Guid.NewGuid();
        var welt = Welt();

        // 1) Fan-out — N=3 (Laufzeitwert) + drei Teilaufträge, alle an denselben Sammelvorgang gebunden.
        welt.Wenn(new StarteSammelvorgang(new(sammel), 3));
        welt.Wenn(new StarteTeilauftrag(new(w1), new(sammel)));
        welt.Wenn(new StarteTeilauftrag(new(w2), new(sammel)));
        welt.Wenn(new StarteTeilauftrag(new(w3), new(sammel)));

        // 2) Zwei Teile fertig → die Funktion meldet je MeldeTeilFertig; Barriere noch NICHT erreicht.
        welt.Wenn(new SchließeTeilauftragAb(new(w1)))
            .Feuert<MeldeTeilFertig>(1).AllesGeroutet()
            .EndzustandVon<Sammelvorgang>(sammel, s => !s.Abgeschlossen && s.Fertig == 1, "1 von 3");
        welt.Wenn(new SchließeTeilauftragAb(new(w2)))
            .EndzustandVon<Sammelvorgang>(sammel, s => !s.Abgeschlossen && s.Fertig == 2, "2 von 3");

        // 3) Der dritte Teil schließt die Barriere.
        welt.Wenn(new SchließeTeilauftragAb(new(w3)))
            .Feuert<MeldeTeilFertig>(1)
            .EndzustandVon<Sammelvorgang>(sammel, s => s.Abgeschlossen && s.Fertig == 3, "erst beim 3. (letzten) Teil ist die Barriere erreicht");
    }

    [Fact]
    public void Ein_stilles_Training_bleibt_nach_der_Frist_haengen_ein_beendetes_nie()
    {
        var still = Guid.NewGuid();
        var beendet = Guid.NewGuid();
        var welt = Ablauf.Mit(Fabrik).Aggregat<Trainingslauf>();
        foreach (var lauf in new[] { still, beendet })
        {
            welt.Wenn(new StarteTraining(new(lauf), new(Guid.NewGuid()), 1, new Hyperparameter(1, 0.1, 8, "klein", 1)));
            welt.Wenn(new MeldeTrainingBegonnen(new(lauf)));
        }
        welt.OffeneFristen.Should().HaveCount(2, "jeder Beginn setzt den Wächter");
        welt.Wenn(new MeldeTrainingGescheitert(new(beendet), "vorher zu Ende"));

        var uhr = welt.Fristen();
        uhr.Feuert<MarkiereAlsHaengengeblieben>(2)
            .EndzustandVon<Trainingslauf>(still, s => s.Status == TrainingsStatus.Haengengeblieben, "still → hängengeblieben")
            .EndzustandVon<Trainingslauf>(beendet, s => s.Status == TrainingsStatus.Gescheitert, "ein Ende vor der Frist bleibt");
        uhr.Events<Trainingslauf, TrainingHaengengeblieben>(beendet).Should().BeEmpty();
    }
}
