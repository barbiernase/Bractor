using Abstractions;

namespace Domain.Trainingslauf;

// ═══════════════════════════════════════════════════
// GUI — Lebenszyklus anstoßen / abbrechen
// ═══════════════════════════════════════════════════

/// <summary>
/// Startet einen Trainingslauf gegen eine EINGEFRORENE Datensatz-Version (reproduzierbarer
/// Input). Der Python-TrainingWorker abonniert das resultierende Event.
/// </summary>
public record StarteTraining(
    Id<Trainingslauf> AggregateId,
    Id<Datensatz.Datensatz> DatensatzId,
    int DatensatzVersion,
    Hyperparameter Hyperparameter
) : ICommand<Trainingslauf>, ICreationCommand;

/// <summary>GUI: laufendes Training kooperativ abbrechen.</summary>
public record BricheTrainingAb(
    Id<Trainingslauf> AggregateId
) : ICommand<Trainingslauf>;

// ═══════════════════════════════════════════════════
// PYTHON — Fortschritt zurückmelden (Event→Command-Reaktor)
// ═══════════════════════════════════════════════════

public record MeldeTrainingBegonnen(
    Id<Trainingslauf> AggregateId
) : ICommand<Trainingslauf>;

public record MeldeFortschritt(
    Id<Trainingslauf> AggregateId,
    EpochenMetrik Metrik
) : ICommand<Trainingslauf>;

public record MeldeTrainingAbgeschlossen(
    Id<Trainingslauf> AggregateId,
    string ModellPfad,
    Endmetriken Endmetriken
) : ICommand<Trainingslauf>;

public record MeldeTrainingGescheitert(
    Id<Trainingslauf> AggregateId,
    string Grund
) : ICommand<Trainingslauf>;

// ═══════════════════════════════════════════════════
// FRIST — Timeout
// ═══════════════════════════════════════════════════

/// <summary>
/// Vom Frist-Mechanismus ausgelöst, wenn ein begonnenes Training nicht rechtzeitig
/// abschließt. Auf ein bereits beendetes Training wirkungslos (idempotent).
/// </summary>
public record MarkiereAlsHaengengeblieben(
    Id<Trainingslauf> AggregateId
) : ICommand<Trainingslauf>;
