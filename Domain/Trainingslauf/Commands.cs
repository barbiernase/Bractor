using Abstractions;

namespace Domain.Trainingslauf;

// ═══════════════════════════════════════════════════
// GUI — Lebenszyklus anstoßen / abbrechen
// ═══════════════════════════════════════════════════

/// <summary>
/// Startet einen Trainingslauf gegen eine EINGEFRORENE Datensatz-Version (reproduzierbarer
/// Input, Konzept §4.1). Der Python-TrainingWorker abonniert das resultierende Event.
/// </summary>
public record StarteTraining(
    Guid AggregateId,
    Guid DatensatzId,
    int DatensatzVersion,
    Hyperparameter Hyperparameter
) : ICreationCommand;

/// <summary>GUI: laufendes Training kooperativ abbrechen.</summary>
public record BricheTrainingAb(
    Guid AggregateId
) : ICommand;

// ═══════════════════════════════════════════════════
// PYTHON — Fortschritt zurückmelden (Event→Command-Reaktor, Konzept §4.2)
// ═══════════════════════════════════════════════════

public record MeldeTrainingBegonnen(
    Guid AggregateId
) : ICommand;

public record MeldeFortschritt(
    Guid AggregateId,
    EpochenMetrik Metrik
) : ICommand;

public record MeldeTrainingAbgeschlossen(
    Guid AggregateId,
    string ModellPfad,
    Endmetriken Endmetriken
) : ICommand;

public record MeldeTrainingGescheitert(
    Guid AggregateId,
    string Grund
) : ICommand;

// ═══════════════════════════════════════════════════
// FRIST — Timeout (Konzept §4.3)
// ═══════════════════════════════════════════════════

/// <summary>
/// Vom Frist-Mechanismus ausgelöst, wenn ein begonnenes Training nicht rechtzeitig
/// abschließt. Auf ein bereits beendetes Training wirkungslos (idempotent).
/// </summary>
public record MarkiereAlsHaengengeblieben(
    Guid AggregateId
) : ICommand;
