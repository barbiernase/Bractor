using Domain.Projections;
using Domain.Trainingslauf;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Trainingslauf-Projektion. Exactly-once-Naht in
/// <see cref="MartenCoCommitStoreBase"/>; hier nur die fachlichen Write-Effekte. Die Projektion ist
/// <see cref="Abstractions.IAppendProjektion"/> (MetrikHistorie wächst je Epoche). TRANSIENT registriert.
/// </summary>
public sealed class TrainingslaufStore : MartenCoCommitStoreBase, ITrainingslaufWriteStore
{
    public TrainingslaufStore(IDocumentStore store) : base(store) { }

    public Task UpsertAsync(TrainingslaufReadModel model) => EnqueueStore(model);

    public Task SetBegonnenAsync(Guid id, DateTimeOffset startzeit)
        => EnqueueTransform<TrainingslaufReadModel>(id, existing => existing with
        {
            Status = TrainingsStatus.Laeuft,
            Startzeit = startzeit,
            LetzteAktualisierung = startzeit
        });

    public Task AppendMetrikAsync(Guid id, EpochenMetrik metrik, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id, existing => existing with
        {
            MetrikHistorie = new List<EpochenMetrik>(existing.MetrikHistorie) { metrik },
            AktuelleEpoche = metrik.Epoche,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetAbgeschlossenAsync(Guid id, string modellPfad, Endmetriken endmetriken, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id, existing => existing with
        {
            Status = TrainingsStatus.Abgeschlossen,
            ModellPfad = modellPfad,
            Endmetriken = endmetriken,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetGescheitertAsync(Guid id, string grund, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id, existing => existing with
        {
            Status = TrainingsStatus.Gescheitert,
            Fehlergrund = grund,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetStatusAsync(Guid id, TrainingsStatus status, DateTimeOffset aktualisierung)
        => EnqueueTransform<TrainingslaufReadModel>(id, existing => existing with
        {
            Status = status,
            LetzteAktualisierung = aktualisierung
        });
}
