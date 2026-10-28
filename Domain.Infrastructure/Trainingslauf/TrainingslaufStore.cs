using Abstractions;
using Core;
using Domain.Projections;
using Domain.Trainingslauf;
using Marten;
using Domain.Projections.Trainingslauf;

using TrainingslaufAgg = Domain.Trainingslauf.Trainingslauf;

namespace Domain.Infrastructure.Trainingslauf;

/// <summary>
/// Store der Trainingslauf-Projektion, Klasse 1 (<see cref="MartenStore"/>); hier nur die fachlichen Write-Effekte
/// (die MetrikHistorie wächst je Epoche — append-artig, genau einmal). SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed partial class TrainingslaufStore : MartenStore, ITrainingslaufStore
{
    public TrainingslaufStore(IDocumentStore store) : base(store, typeof(TrainingslaufReadModel)) { }

    public Task UpsertAsync(TrainingslaufReadModel model) => EnqueueStore(model.Id.Wert, model);

    public Task SetBegonnenAsync(Id<TrainingslaufAgg> id, DateTimeOffset startzeit)
        => EnqueueTransform<TrainingslaufReadModel>(id.Wert, existing => existing with
        {
            Status = TrainingsStatus.Laeuft,
            Startzeit = startzeit,
            LetzteAktualisierung = startzeit
        });

    public Task AppendMetrikAsync(Id<TrainingslaufAgg> id, EpochenMetrik metrik, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id.Wert, existing => existing with
        {
            MetrikHistorie = new List<EpochenMetrik>(existing.MetrikHistorie) { metrik },
            AktuelleEpoche = metrik.Epoche,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetAbgeschlossenAsync(Id<TrainingslaufAgg> id, string modellPfad, Endmetriken endmetriken, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id.Wert, existing => existing with
        {
            Status = TrainingsStatus.Abgeschlossen,
            ModellPfad = modellPfad,
            Endmetriken = endmetriken,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetGescheitertAsync(Id<TrainingslaufAgg> id, string grund, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id.Wert, existing => existing with
        {
            Status = TrainingsStatus.Gescheitert,
            Fehlergrund = grund,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetStatusAsync(Id<TrainingslaufAgg> id, TrainingsStatus status, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id.Wert, existing => existing with
        {
            Status = status,
            LetzteAktualisierung = aktualisierung
        });
}
