namespace Domain.Trainingslauf;

/// <summary>
/// Hyperparameter eines Trainingslaufs — Teil des reproduzierbaren Inputs.
/// </summary>
public record Hyperparameter(
    int Epochen,
    double LernRate,
    int BatchGroesse,
    string Architektur,
    int Seed
);

/// <summary>
/// Ein Fortschritts-Punkt je Epoche (Live-Kurve). Loss + Genauigkeit
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
