using Abstractions;
using Domain.Modell;

namespace Domain.Projections;

/// <summary>Kopf-Daten eines registrierten Modells (+ ob es das aktive ist).</summary>
public record ModellAntwort(
    Guid Id,
    string? Name,
    string? Pfad,
    Guid TrainingslaufId,
    Guid DatensatzId,
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
/// ist leer (Guid.Empty), wenn (noch) kein Modell aktiv ist.
/// </summary>
public record AktivesModellAntwort(
    Guid ModellId,
    string? Name,
    string? Pfad
) : IQueryResponse;
