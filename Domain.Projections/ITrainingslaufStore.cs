using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Projections;

// FÄHIGKEITEN des Trainingslauf-Stores — je Funktion ein Interface (CQRS051).

// ── Schreiben (TrainingslaufProjektion) ──
public interface IUpsertTrainingslauf : IWriteStore { Task UpsertAsync(TrainingslaufReadModel model); }

/// <summary>Beginn: Status → Läuft, Startzeit festhalten.</summary>
public interface ISetTrainingslaufBegonnen : IWriteStore { Task SetBegonnenAsync(Guid id, DateTimeOffset startzeit); }

/// <summary>Fortschritt: einen Metrik-Punkt anhängen (append) + aktuelle Epoche.</summary>
public interface IAppendMetrik : IWriteStore { Task AppendMetrikAsync(Guid id, EpochenMetrik metrik, DateTimeOffset aktualisierung); }

public interface ISetTrainingslaufAbgeschlossen : IWriteStore
{
    Task SetAbgeschlossenAsync(Guid id, string modellPfad, Endmetriken endmetriken, DateTimeOffset aktualisierung);
}

public interface ISetTrainingslaufGescheitert : IWriteStore { Task SetGescheitertAsync(Guid id, string grund, DateTimeOffset aktualisierung); }

/// <summary>Terminaler Status ohne weitere Nutzdaten (Abgebrochen / Hängengeblieben).</summary>
public interface ISetTrainingslaufStatus : IWriteStore { Task SetStatusAsync(Guid id, TrainingsStatus status, DateTimeOffset aktualisierung); }

// ── Lesen (TrainingslaufReader, Dashboard-Live-Kurve) ──
public interface IFindTrainingslauf : IReadStore { Task<TrainingslaufReadModel?> FindByIdAsync(Guid id); }

/// <summary>Alle Läufe (Sidebar-Liste), neueste zuerst.</summary>
public interface IGetAlleTrainingslaeufe : IReadStore { Task<IReadOnlyList<TrainingslaufReadModel>> GetAlleAsync(); }

/// <summary>Der Trainingslauf-Store — Bündel seiner Fähigkeiten, Transaktionsgrenze.</summary>
public interface ITrainingslaufStore : IStore,
    IUpsertTrainingslauf, ISetTrainingslaufBegonnen, IAppendMetrik,
    ISetTrainingslaufAbgeschlossen, ISetTrainingslaufGescheitert, ISetTrainingslaufStatus,
    IFindTrainingslauf, IGetAlleTrainingslaeufe
{ }
