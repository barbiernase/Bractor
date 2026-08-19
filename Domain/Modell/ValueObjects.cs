namespace Domain.Modell;

/// <summary>
/// Metriken eines registrierten Modells — der Stand am Ende seines Trainingslaufs. Bewusst
/// eine eigene, kleine Kopie (nicht die Trainingslauf-<c>Endmetriken</c>), damit das
/// Modell-Aggregat unabhängig vom Trainingslauf-Aggregat bleibt.
/// </summary>
public record ModellMetriken(
    double Loss,
    double Genauigkeit
);
