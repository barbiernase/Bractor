using Abstractions;
using Core;
using Domain.Trainingslauf;
using TrainingslaufAgg = Domain.Trainingslauf.Trainingslauf;

namespace Domain.Projections;

/// <summary>
/// Materialisiert den Trainingslauf-Zustand (Konzept §4.5): ein <see cref="TrainingslaufReadModel"/>
/// pro Lauf, die MetrikHistorie wächst je Epoche → Live-Kurve fürs Dashboard.
///
/// APPEND-ARTIG (<see cref="IAppendProjektion"/>): TrainingFortschritt hängt einen Metrik-Punkt an —
/// Doppelverarbeitung wäre sichtbar/korrumpierend, daher erzwingt der GA-1-Check den Co-Commit-Store
/// (<c>TrainingslaufStore</c> ist zugleich <see cref="ICoCommitTracker"/>).
/// </summary>
public partial class TrainingslaufProjektion : ISubscriber, IPullSubscriber, IAppendProjektion
{
    public string SubscriberId => "trainingslauf-projection";

    public async Task Handle(TrainingAngefordert evt, IAggregateEnvelope envelope, ProjectionWriter writer, IUpsertTrainingslauf upsertTrainingslauf)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await upsertTrainingslauf.UpsertAsync(new TrainingslaufReadModel
            {
                Id = envelope.AggregateId,
                DatensatzId = evt.DatensatzId,
                DatensatzVersion = evt.DatensatzVersion,
                Status = TrainingsStatus.Angefordert,
                Hyperparameter = evt.Hyperparameter,
                GesamtEpochen = evt.Hyperparameter.Epochen,
                LetzteAktualisierung = envelope.CreatedAtUtc
            });
        });
    }

    public async Task Handle(TrainingBegonnen evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetTrainingslaufBegonnen setTrainingslaufBegonnen)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await setTrainingslaufBegonnen.SetBegonnenAsync(envelope.AggregateId, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(TrainingFortschritt evt, IAggregateEnvelope envelope, ProjectionWriter writer, IAppendMetrik appendMetrik)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await appendMetrik.AppendMetrikAsync(envelope.AggregateId, evt.Metrik, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(TrainingAbgeschlossen evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetTrainingslaufAbgeschlossen setTrainingslaufAbgeschlossen)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await setTrainingslaufAbgeschlossen.SetAbgeschlossenAsync(
                envelope.AggregateId, evt.ModellPfad, evt.Endmetriken, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(TrainingGescheitert evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetTrainingslaufGescheitert setTrainingslaufGescheitert)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await setTrainingslaufGescheitert.SetGescheitertAsync(envelope.AggregateId, evt.Grund, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(TrainingAbgebrochen evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetTrainingslaufStatus setTrainingslaufStatus)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await setTrainingslaufStatus.SetStatusAsync(envelope.AggregateId, TrainingsStatus.Abgebrochen, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(TrainingHaengengeblieben evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetTrainingslaufStatus setTrainingslaufStatus)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TrainingslaufAgg>(envelope.AggregateId);
            await setTrainingslaufStatus.SetStatusAsync(envelope.AggregateId, TrainingsStatus.Haengengeblieben, envelope.CreatedAtUtc);
        });
    }
}
