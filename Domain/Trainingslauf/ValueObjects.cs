namespace Domain.Trainingslauf;

/// <summary>
/// Hyperparameter eines Trainingslaufs — Teil des reproduzierbaren Inputs (Konzept §4.1, §10).
/// </summary>
public record Hyperparameter(
    int Epochen,
    double LernRate,
    int BatchGroesse,
    string Architektur,
    int Seed
);

/// <summary>
/// Ein Fortschritts-Punkt je Epoche (Live-Kurve, Konzept §4.5 / §8.1). Loss + Genauigkeit
/// sind die Standard-Metriken; die Historie dieser Punkte streamt event-sourced ins Dashboard.
/// </summary>
public record EpochenMetrik(
    int Epoche,
    double Loss,
    double Genauigkeit
);

/// <summary>Endmetriken eines abgeschlossenen Laufs.</summary>
public record Endmetriken(
    double Loss,
    double Genauigkeit
);
