using Abstractions;

namespace Domain.Modell;

// ═══════════════════════════════════════════════════
// MODELL — Lebenszyklus (Konzept konzept-training-und-datensatz §5)
//   RegistriereModell ──► ModellRegistriert   (aus einem abgeschlossenen Trainingslauf)
//   SetzeModellAktiv  ──► ModellAktiviert      (der Inferenz-Worker lädt das aktive Modell)
//   ArchiviereModell  ──► ModellArchiviert
// ═══════════════════════════════════════════════════

/// <summary>
/// Registriert ein trainiertes Modell-Artefakt als erststelligen Baustein (Konzept §5).
/// Trägt die Provenienz mit: aus welchem Trainingslauf / welcher eingefrorenen Datensatz-Version
/// es stammt, plus Pfad zum Artefakt und Endmetriken.
/// </summary>
public record RegistriereModell(
    Guid AggregateId,
    Guid TrainingslaufId,
    Guid DatensatzId,
    int DatensatzVersion,
    string Name,
    string Pfad,
    ModellMetriken Metriken
) : ICreationCommand;

/// <summary>
/// Setzt dieses Modell aktiv (Konzept §5: „genau ein aktives Modell je Zweck"). Der Inferenz-
/// Worker abonniert das resultierende <see cref="ModellAktiviert"/> und lädt das neue Modell.
/// </summary>
public record SetzeModellAktiv(
    Guid AggregateId
) : ICommand;

/// <summary>Archiviert das Modell (nicht mehr aktivierbar).</summary>
public record ArchiviereModell(
    Guid AggregateId
) : ICommand;
