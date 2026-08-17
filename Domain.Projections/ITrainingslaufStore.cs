using Domain.Trainingslauf;

namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster der Trainingslauf-Projektion — je Event ein atomarer Effekt.
/// Wird von <c>TrainingslaufProjektion</c> verwendet.
/// </summary>
public interface ITrainingslaufWriteStore
{
    Task UpsertAsync(TrainingslaufReadModel model);

    /// <summary>Beginn: Status → Läuft, Startzeit festhalten.</summary>
    Task SetBegonnenAsync(Guid id, DateTimeOffset startzeit);

    /// <summary>Fortschritt: einen Metrik-Punkt anhängen (append) + aktuelle Epoche.</summary>
    Task AppendMetrikAsync(Guid id, EpochenMetrik metrik, DateTimeOffset aktualisierung);

    Task SetAbgeschlossenAsync(Guid id, string modellPfad, Endmetriken endmetriken, DateTimeOffset aktualisierung);

    Task SetGescheitertAsync(Guid id, string grund, DateTimeOffset aktualisierung);

    /// <summary>Terminaler Status ohne weitere Nutzdaten (Abgebrochen / Hängengeblieben).</summary>
    Task SetStatusAsync(Guid id, TrainingsStatus status, DateTimeOffset aktualisierung);
}

/// <summary>
/// Read-Zugriffsmuster der Trainingslauf-Projektion. Wird vom <c>TrainingslaufReader</c>
/// und vom Dashboard verwendet (Live-Kurve).
/// </summary>
public interface ITrainingslaufReadStore
{
    Task<TrainingslaufReadModel?> FindByIdAsync(Guid id);

    /// <summary>Alle Läufe (Sidebar-Liste), neueste zuerst.</summary>
    Task<IReadOnlyList<TrainingslaufReadModel>> GetAlleAsync();
}
