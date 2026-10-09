using System;
using System.Linq;
using Abstractions;
using Cqrs.Testing;
using Domain.Datensatz;
using Domain.Pipeline.Datensatz;
using Domain.Trainingslauf;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — die UMGEZOGENEN Handle-Pipelines als Flüsse (docs/konzept-editor-pipelines.md §14.9), durchgespielt gegen die ECHTEN
/// Aggregate (generierte Fabrik, <see cref="FlussLaufwerk"/>). Bewiesen: der Datensatz-Resolver nimmt die Id des Datensatzes aus dem
/// Strom der Quelle (das Event trägt sie nicht); der Trainings-Wächter ist ein Rennen — Ende gewinnt still, ⏳ markiert hängend.
/// </summary>
public class UmgezogeneFluesseTests
{
    private static FlussLaufwerk Laufwerk()
        => new(new SagaLaufwerk(new Infrastructure.AggregateHandlerFactory(), Array.Empty<(string, ProzessRegeln)>()),
            new (string, PipelineFluss)[]
            {
                (nameof(DatensatzRangeAufloesung), new DatensatzRangeAufloesung().Fluss),
                (nameof(DatensatzEinfrieren), new DatensatzEinfrieren().Fluss),
                (nameof(TrainingWaechter), new TrainingWaechter().Fluss),
            });

    private static readonly Guid PaarA = Guid.Parse("00000000-0000-0000-0000-00000000aa01");

    [Fact]
    public void Range_hinzufuegen_startet_den_Resolver_und_nimmt_die_Paare_in_DENSELBEN_Datensatz_auf()
    {
        var w = Laufwerk();
        var ds = Guid.NewGuid();
        w.Fahre(new ErstelleDatensatz(ds, "Probe"), _ => throw new InvalidOperationException("kein Fluss erwartet"));

        var lauf = w.Fahre(new FuegeRangeHinzu(ds, new RangeKriterien(NurKomplette: true)), a => a.Auftrag switch
        {
            SucheRange s => new FlussAntwort.Ergebnis(new RangeGefunden(new[] { PaarA }, new RangeHerkunft(s.Kriterien, 1))),
            _ => throw new NotSupportedException(),
        });

        lauf.Vorgänge.Should().ContainSingle().Which.Should().Match<FlussVorgang>(v => v.Pipeline == nameof(DatensatzRangeAufloesung) && v.Erfolg);
        var aufnehmen = lauf.Schritte.Single(s => s.Nachricht is NimmRangeAuf);
        ((NimmRangeAuf)aufnehmen.Nachricht).AggregateId.Should().Be(ds, "angefordert.Strom().Id ist der Datensatz, der die Range anforderte");
        aufnehmen.Ergebnis.Should().BeOfType<PaareAufgenommen>();
    }

    [Fact]
    public void Eine_Range_ohne_Treffer_endet_ohne_Command()
    {
        var w = Laufwerk();
        var ds = Guid.NewGuid();
        w.Fahre(new ErstelleDatensatz(ds, "Probe"), _ => throw new InvalidOperationException());
        var lauf = w.Fahre(new FuegeRangeHinzu(ds, new RangeKriterien()),
            a => new FlussAntwort.Ergebnis(new RangeOhneTreffer(((SucheRange)a.Auftrag!).Kriterien)));

        lauf.Vorgänge.Should().ContainSingle().Which.Erfolg.Should().BeTrue();
        lauf.Schritte.Should().NotContain(s => s.Art == PipelineKnotenArt.Command);
    }

    private static (FlussLaufwerk W, Guid Lauf) GestartetesTraining()
    {
        var w = Laufwerk();
        var lauf = Guid.NewGuid();
        w.Fahre(new StarteTraining(lauf, Guid.NewGuid(), 1, new Hyperparameter(1, 0.1, 8, "resnet", 1)), _ => throw new InvalidOperationException());
        return (w, lauf);
    }

    [Fact]
    public void Trainings_Waechter_Ende_vor_der_Frist_markiert_nichts()
    {
        var (w, lauf) = GestartetesTraining();
        var ergebnis = w.Fahre(new MeldeTrainingBegonnen(lauf),
            a => new FlussAntwort.Ergebnis(new TrainingAbgebrochen()));

        var schritt = ergebnis.Schritte.Single(s => s.Art == PipelineKnotenArt.Warte);
        schritt.Ergebnis.Should().BeOfType<TrainingAbgebrochen>();
        ergebnis.Schritte.Should().NotContain(s => s.Nachricht is MarkiereAlsHaengengeblieben);
        ergebnis.Vorgänge.Should().ContainSingle().Which.Erfolg.Should().BeTrue();
    }

    [Fact]
    public void Trainings_Waechter_nach_der_Frist_markiert_den_Lauf_als_haengengeblieben()
    {
        var (w, lauf) = GestartetesTraining();
        var ergebnis = w.Fahre(new MeldeTrainingBegonnen(lauf), a => new FlussAntwort.Zeitlimit());

        var markieren = ergebnis.Schritte.Single(s => s.Nachricht is MarkiereAlsHaengengeblieben);
        ((MarkiereAlsHaengengeblieben)markieren.Nachricht).AggregateId.Should().Be(lauf);
        markieren.Ergebnis.Should().BeOfType<TrainingHaengengeblieben>();
    }
}
