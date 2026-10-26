using Abstractions;
using Domain.Modell;

namespace Domain.Projections.Modell;

/// <summary>Kopf-Daten eines registrierten Modells (+ ob es das aktive ist).</summary>
public record ModellAntwort(
    Id<Domain.Modell.Modell> Id,
    string? Name,
    string? Pfad,
    Id<Domain.Trainingslauf.Trainingslauf> TrainingslaufId,
    Id<Domain.Datensatz.Datensatz> DatensatzId,
    int DatensatzVersion,
    double Loss,
    double Genauigkeit,
    ModellStatus Status,
    bool IstAktiv,
    DateTimeOffset RegistriertAm
) : IQueryResponse;

/// <summary>Antwort auf <see cref="HoleModelle"/> — alle Modelle (Modelle-Bühne).</summary>
public record ModellListe(
    IReadOnlyList<ModellAntwort> Items
) : IQueryResponse;

/// <summary>
/// Antwort auf <see cref="HoleAktivesModell"/> — der Singleton-Zeiger. <see cref="ModellId"/>
/// ist leer (default), wenn (noch) kein Modell aktiv ist.
/// </summary>
public record AktivesModellAntwort(
    Id<Domain.Modell.Modell> ModellId,
    string? Name,
    string? Pfad
) : IQueryResponse;
