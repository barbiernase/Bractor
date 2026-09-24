using Abstractions;

namespace Domain.Sammelvorgang;

// ═══════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — persistiert + publiziert
// ═══════════════════════════════════════════════════

/// <summary>Barriere gestartet — hält die erwartete Anzahl N fest (der Laufzeitwert).</summary>
public record SammelvorgangGestartet(
    int Anzahl
) : IEvent;

/// <summary>Ein Teil verbucht — trägt den Fortschritt (Fertig von Erwartet), foldet den Zähler hoch.</summary>
public record TeilVerbucht(
    int Fertig,
    int Erwartet
) : IEvent;

/// <summary>
/// Die Barriere ist erreicht: ALLE N Teile sind fertig. Hierauf reagiert die Batch-Abschluss-Aktion
/// (z. B. „Lohnlauf abgeschlossen", „Datensatz-Report erstellen") — als ganz normaler Einzel-Trigger.
/// </summary>
public record SammelvorgangAbgeschlossen() : IEvent;

// ═══════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent) — nicht persistiert
// ═══════════════════════════════════════════════════

public record SammelvorgangExistiertBereits(Guid SammelvorgangId) : ITransientEvent;
public record SammelvorgangNichtGefunden(Guid SammelvorgangId) : ITransientEvent;
public record SammelvorgangBereitsAbgeschlossen(Guid SammelvorgangId) : ITransientEvent;
