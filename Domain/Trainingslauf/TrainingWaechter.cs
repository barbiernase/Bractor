using Abstractions;

namespace Domain.Trainingslauf;

/// <summary>
/// Timeout-Wächter für Trainingsläufe (Konzept §4.3) als Pipeline-Fluss (docs/konzept-editor-pipelines.md §14): ein RENNEN statt
/// Frist + Storno. Nach <see cref="TrainingBegonnen"/> wartet der Fluss im Strom desselben Laufs auf sein Ende (abgeschlossen,
/// gescheitert oder abgebrochen); kommt binnen 6 h keines, nimmt er den ⏳-Port und markiert den Lauf als hängengeblieben. Ein Ende
/// nach der Frist zählt nicht (wer zuerst im Log steht, gewinnt). Die Id des Laufs kommt aus dem Strom der Quelle.
/// </summary>
public sealed class TrainingWaechter : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var begonnen = p.Auf<TrainingBegonnen>();
        var ende = begonnen.Strom().Warte<TrainingAbgeschlossen, TrainingGescheitert, TrainingAbgebrochen>()
            .Zeitlimit(TimeSpan.FromHours(6));
        var haengt = p.Alle(ende.BeiZeitlimit(), begonnen.Strom()).Sende<MarkiereAlsHaengengeblieben>((endeZeit, begonnenStrom) => new MarkiereAlsHaengengeblieben(begonnenStrom.Id));
    });
}
