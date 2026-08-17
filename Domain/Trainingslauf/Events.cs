using Abstractions;

namespace Domain.Trainingslauf;

// ═══════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — persistiert + publiziert
// ═══════════════════════════════════════════════════

/// <summary>
/// Training angefordert — hält DatensatzId + Version + Hyperparameter fest (reproduzierbar).
/// Der Python-TrainingWorker abonniert dieses Event und startet den Lauf.
/// </summary>
public record TrainingAngefordert(
    Guid DatensatzId,
    int DatensatzVersion,
    Hyperparameter Hyperparameter
) : IEvent;

public record TrainingBegonnen() : IEvent;

/// <summary>Ein Fortschritts-Punkt je Epoche — foldet in die MetrikHistorie (Live-Kurve).</summary>
public record TrainingFortschritt(
    EpochenMetrik Metrik
) : IEvent;

public record TrainingAbgeschlossen(
    string ModellPfad,
    Endmetriken Endmetriken
) : IEvent;

public record TrainingGescheitert(
    string Grund
) : IEvent;

public record TrainingAbgebrochen() : IEvent;

public record TrainingHaengengeblieben() : IEvent;

// ═══════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent) — nicht persistiert
// ═══════════════════════════════════════════════════

public record TrainingslaufExistiertBereits(Guid TrainingslaufId) : ITransientEvent;
public record TrainingslaufNichtGefunden(Guid TrainingslaufId) : ITransientEvent;
public record TrainingNichtAktiv(Guid TrainingslaufId) : ITransientEvent;
public record TrainingBereitsBeendet(Guid TrainingslaufId) : ITransientEvent;
