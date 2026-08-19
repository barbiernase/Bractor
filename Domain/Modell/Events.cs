using Abstractions;

namespace Domain.Modell;

// ═══════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — persistiert + publiziert
// ═══════════════════════════════════════════════════

/// <summary>Modell registriert — Provenienz + Pfad + Metriken festgeschrieben.</summary>
public record ModellRegistriert(
    Guid TrainingslaufId,
    Guid DatensatzId,
    int DatensatzVersion,
    string Name,
    string Pfad,
    ModellMetriken Metriken
) : IEvent;

/// <summary>
/// Modell aktiviert (ein Fakt zum Zeitpunkt T). Trägt <see cref="Pfad"/> + <see cref="Name"/> mit,
/// damit der Inferenz-Worker das Modell ohne Zusatz-Query laden kann. Das „aktuell aktive Modell"
/// ist die Projektion der jeweils jüngsten Aktivierung (last-writer-wins).
/// </summary>
public record ModellAktiviert(
    string Pfad,
    string Name
) : IEvent;

/// <summary>Modell archiviert.</summary>
public record ModellArchiviert() : IEvent;

// ═══════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent) — nicht persistiert
// ═══════════════════════════════════════════════════

public record ModellExistiertBereits(Guid ModellId) : ITransientEvent;
public record ModellNichtGefunden(Guid ModellId) : ITransientEvent;
public record ModellBereitsArchiviert(Guid ModellId) : ITransientEvent;
