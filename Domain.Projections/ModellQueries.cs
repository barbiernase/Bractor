using Abstractions;

namespace Domain.Projections;

/// <summary>Alle registrierten Modelle (Modelle-Bühne: Vergleich + „aktiv setzen").</summary>
public record HoleModelle() : IQuery;

/// <summary>Das aktuell aktive Modell (Singleton-Zeiger) — für Live-Leiste + Inferenz.</summary>
public record HoleAktivesModell() : IQuery;
