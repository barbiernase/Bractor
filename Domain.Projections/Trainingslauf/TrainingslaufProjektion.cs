using Abstractions;
using Domain.Trainingslauf;
using TrainingslaufAgg = Domain.Trainingslauf.Trainingslauf;

namespace Domain.Projections.Trainingslauf;

/// <summary>
/// Materialisiert den Trainingslauf-Zustand: ein <see cref="TrainingslaufReadModel"/>
/// pro Lauf, die MetrikHistorie wächst je Epoche → Live-Kurve fürs Dashboard.
///
/// Append-artig: TrainingFortschritt hängt einen Metrik-Punkt an — eine doppelte Wirkung wäre sichtbar; genau einmal, weil der
/// <c>TrainingslaufStore</c> ein Store der Klasse 1 ist.
/// </summary>
public sealed class TrainingslaufProjektion : IProjektion
{
    public Task Auf(TrainingAngefordert e, Herkunft h, IUpsertTrainingslauf upsertTrainingslauf)
        => upsertTrainingslauf.UpsertAsync(new TrainingslaufReadModel
        {
            Id = h.Id<TrainingslaufAgg>(),
            DatensatzId = e.DatensatzId,
            DatensatzVersion = e.DatensatzVersion,
            Status = TrainingsStatus.Angefordert,
            Hyperparameter = e.Hyperparameter,
            GesamtEpochen = e.Hyperparameter.Epochen,
            LetzteAktualisierung = h.Zeit
        });

    public Task Auf(TrainingBegonnen e, Herkunft h, ISetTrainingslaufBegonnen setTrainingslaufBegonnen)
        => setTrainingslaufBegonnen.SetBegonnenAsync(h.Id<TrainingslaufAgg>(), h.Zeit);

    public Task Auf(TrainingFortschritt e, Herkunft h, IAppendMetrik appendMetrik)
        => appendMetrik.AppendMetrikAsync(h.Id<TrainingslaufAgg>(), e.Metrik, h.Zeit);

    public Task Auf(TrainingAbgeschlossen e, Herkunft h, ISetTrainingslaufAbgeschlossen setTrainingslaufAbgeschlossen)
        => setTrainingslaufAbgeschlossen.SetAbgeschlossenAsync(
            h.Id<TrainingslaufAgg>(), e.ModellPfad, e.Endmetriken, h.Zeit);

    public Task Auf(TrainingGescheitert e, Herkunft h, ISetTrainingslaufGescheitert setTrainingslaufGescheitert)
        => setTrainingslaufGescheitert.SetGescheitertAsync(h.Id<TrainingslaufAgg>(), e.Grund, h.Zeit);

    public Task Auf(TrainingAbgebrochen e, Herkunft h, ISetTrainingslaufStatus setTrainingslaufStatus)
        => setTrainingslaufStatus.SetStatusAsync(h.Id<TrainingslaufAgg>(), TrainingsStatus.Abgebrochen, h.Zeit);

    public Task Auf(TrainingHaengengeblieben e, Herkunft h, ISetTrainingslaufStatus setTrainingslaufStatus)
        => setTrainingslaufStatus.SetStatusAsync(h.Id<TrainingslaufAgg>(), TrainingsStatus.Haengengeblieben, h.Zeit);
}
