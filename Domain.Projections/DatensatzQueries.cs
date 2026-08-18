using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Die eingefrorenen Samples einer Datensatz-Version — paginiert (Konzept §6).
/// Derselbe typisierte Query-Kanal für Python (TrainingWorker) und Blazor (Vorschau):
/// eine Deklaration hier, per Proto-Regen sehen sie beide Clients.
/// Liest den <em>immutablen</em> Snapshot → reproduzierbar.
/// </summary>
public record HoleDatensatzSamples(
    Guid DatensatzId,
    int Version,
    int Seite = 1,
    int SeitenGroesse = 500
) : IQuery;

/// <summary>Ein einzelner Datensatz (Kopf-Daten) — für GUI-Detail und Integrations-Prüfung.</summary>
public record HoleDatensatz(Guid DatensatzId) : IQuery;

/// <summary>Alle Datensätze (Sidebar-Liste mit Entwurf/Eingefroren-Badge, Größe, Version).</summary>
public record HoleDatensaetze() : IQuery;

/// <summary>
/// Rückwärts-Frage (Konzept datensatz-kuratierung §4.3): in welchen Datensätzen liegt dieses
/// Bildpaar? Speist die „in Datensätzen: …"-Chips im Einbild. Liest den Rückwärts-Index.
/// </summary>
public record HoleDatensaetzeFuerPaar(Guid ImagePairId) : IQuery;

/// <summary>Welche Bildpaar-Sicht eines Datensatzes die Galerie zeigt.</summary>
public enum DatensatzPaarModus
{
    // Bewusst 1-basiert (nicht 0): der DtoMapper verwirft den Enum-Default-Wert 0 auf der Wire.
    Mitglieder = 1,
    Ausgeschlossen = 2,
}

/// <summary>
/// Die Bildpaare eines Datensatzes (Mitglieder ODER Ausgeschlossene), paginiert — speist die
/// Datensatz-gescopte Galerie (Konzept datensatz-kuratierung: „in der Galerie ansehen").
/// Antwortet mit <see cref="ImagePairSuchergebnis"/> (wiederverwendet), damit dasselbe
/// virtuelle Fenster wie die normale Suche absorbiert — kein neuer Lade-/Merge-Pfad.
/// </summary>
public record HoleDatensatzPaare(
    Guid DatensatzId,
    DatensatzPaarModus Modus,
    int Seite = 1,
    int SeitenGroesse = 50
) : IQuery;
