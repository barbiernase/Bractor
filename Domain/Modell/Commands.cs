using Abstractions;

namespace Domain.Modell;

// ═══════════════════════════════════════════════════
// MODELL — Lebenszyklus
//   RegistriereModell ──► ModellRegistriert   (aus einem abgeschlossenen Trainingslauf)
//   SetzeModellAktiv  ──► ModellAktiviert      (der Inferenz-Worker lädt das aktive Modell)
//   ArchiviereModell  ──► ModellArchiviert
// ═══════════════════════════════════════════════════

/// <summary>
/// Registriert ein trainiertes Modell-Artefakt als erststelligen Baustein.
/// Trägt die Provenienz mit: aus welchem Trainingslauf / welcher eingefrorenen Datensatz-Version
/// es stammt, plus Pfad zum Artefakt und Endmetriken.
/// </summary>
public record RegistriereModell(
    Id<Modell> AggregateId,
    Id<Trainingslauf.Trainingslauf> TrainingslaufId,
    Id<Datensatz.Datensatz> DatensatzId,
    int DatensatzVersion,
    string Name,
    string Pfad,
    ModellMetriken Metriken
) : ICommand<Modell>, ICreationCommand;

/// <summary>
/// Setzt dieses Modell aktiv („genau ein aktives Modell je Zweck"). Der Inferenz-
/// Worker abonniert das resultierende <see cref="ModellAktiviert"/> und lädt das neue Modell.
/// </summary>
public record SetzeModellAktiv(
    Id<Modell> AggregateId
) : ICommand<Modell>;

/// <summary>Archiviert das Modell (nicht mehr aktivierbar).</summary>
public record ArchiviereModell(
    Id<Modell> AggregateId
) : ICommand<Modell>;
