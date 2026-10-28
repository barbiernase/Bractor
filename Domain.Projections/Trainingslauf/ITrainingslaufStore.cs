using Abstractions;
using Domain.Trainingslauf;
using TrainingslaufAgg = Domain.Trainingslauf.Trainingslauf;

namespace Domain.Projections.Trainingslauf;

// FÄHIGKEITEN des Trainingslauf-Stores — je Funktion ein Interface (CQRS051).

// ── Schreiben (TrainingslaufProjektion) ──
public interface IUpsertTrainingslauf : IWriteStore { Task UpsertAsync(TrainingslaufReadModel model); }

/// <summary>Beginn: Status → Läuft, Startzeit festhalten.</summary>
public interface ISetTrainingslaufBegonnen : IWriteStore { Task SetBegonnenAsync(Id<TrainingslaufAgg> id, DateTimeOffset startzeit); }

/// <summary>Fortschritt: einen Metrik-Punkt anhängen (append) + aktuelle Epoche.</summary>
public interface IAppendMetrik : IWriteStore { Task AppendMetrikAsync(Id<TrainingslaufAgg> id, EpochenMetrik metrik, DateTimeOffset aktualisierung); }

public interface ISetTrainingslaufAbgeschlossen : IWriteStore
{
    Task SetAbgeschlossenAsync(Id<TrainingslaufAgg> id, string modellPfad, Endmetriken endmetriken, DateTimeOffset aktualisierung);
}

public interface ISetTrainingslaufGescheitert : IWriteStore { Task SetGescheitertAsync(Id<TrainingslaufAgg> id, string grund, DateTimeOffset aktualisierung); }

/// <summary>Terminaler Status ohne weitere Nutzdaten (Abgebrochen / Hängengeblieben).</summary>
public interface ISetTrainingslaufStatus : IWriteStore { Task SetStatusAsync(Id<TrainingslaufAgg> id, TrainingsStatus status, DateTimeOffset aktualisierung); }

// ── Lesen (TrainingslaufReader, Dashboard-Live-Kurve) ──
public interface IFindTrainingslauf : IReadStore { Task<TrainingslaufReadModel?> FindByIdAsync(Id<TrainingslaufAgg> id); }

/// <summary>Alle Läufe (Sidebar-Liste), neueste zuerst.</summary>
public interface IGetAlleTrainingslaeufe : IReadStore { Task<IReadOnlyList<TrainingslaufReadModel>> GetAlleAsync(); }

/// <summary>Der Trainingslauf-Store — Bündel seiner Fähigkeiten, Transaktionsgrenze.</summary>
public interface ITrainingslaufStore : IStore,
    IUpsertTrainingslauf, ISetTrainingslaufBegonnen, IAppendMetrik,
    ISetTrainingslaufAbgeschlossen, ISetTrainingslaufGescheitert, ISetTrainingslaufStatus,
    IFindTrainingslauf, IGetAlleTrainingslaeufe
{ }
