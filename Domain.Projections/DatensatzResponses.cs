using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;   // Klassifikation

namespace Domain.Projections;

// ═══════════════════════════════════════════════════════
// SAMPLES (Trainings-Wahrheit, paginiert)
// ═══════════════════════════════════════════════════════

/// <summary>
/// Ein Trainings-Sample: die Bildpfade (Python holt die Pixel über /api/files/…) +
/// Label + Split zum Einfrier-Zeitpunkt. DatensatzId/Version stecken in der Query.
/// </summary>
public record DatensatzSample(
    Guid ImagePairId,
    string Dc0Pfad,
    string Dc2Pfad,
    Klassifikation Label,
    Split Split
);

/// <summary>Antwort auf <see cref="HoleDatensatzSamples"/> — eine Seite Samples + Seiten-Info.</summary>
public record DatensatzSamples(
    IReadOnlyList<DatensatzSample> Samples,
    int GesamtAnzahl,
    int Seite,
    int SeitenGroesse
) : IQueryResponse;

// ═══════════════════════════════════════════════════════
// DATENSATZ-KOPF
// ═══════════════════════════════════════════════════════

public record DatensatzAntwort(
    Guid Id,
    string? Name,
    DatensatzStatus Status,
    int AnzahlMitglieder,
    int EingefroreneVersion,
    SplitKonfig Split,
    IReadOnlyList<RangeHerkunft> Ranges,
    // Die konkreten Entwurfs-Mitglieder (Bildpaar-Ids). Grundlage für die
    // „Datensatz-als-Tag"-Sicht (Konzept datensatz-kuratierung §4.3, §6): der Client
    // baut daraus O(1)-Mitgliedschaft (Badges in Galerie/Einbild) für das Sammel-Ziel.
    IReadOnlyList<Guid> Mitglieder,
    // Beim Kuratieren aussortierte Paare — für die „Ausgeschlossen"-Galerie + Zähler.
    IReadOnlyList<Guid> Ausgeschlossen
) : IQueryResponse;

public record DatensatzNichtGefunden(Guid DatensatzId) : IQueryResponse;

/// <summary>Antwort auf <see cref="HoleDatensaetze"/> — alle Datensätze (Sidebar-Liste).</summary>
public record DatensatzListe(
    IReadOnlyList<DatensatzAntwort> Items
) : IQueryResponse;

// ═══════════════════════════════════════════════════════
// RÜCKWÄRTS-TAGS (Datensatz-Zugehörigkeit eines Bildes)
// ═══════════════════════════════════════════════════════

/// <summary>Ein Datensatz-Tag eines Bildes: Kopf-Daten für den Chip im Einbild.</summary>
public record DatensatzTag(
    Guid Id,
    string? Name,
    DatensatzStatus Status,
    int EingefroreneVersion
);

/// <summary>
/// Antwort auf <see cref="HoleDatensaetzeFuerPaar"/> — alle Datensätze, in denen das Bildpaar
/// liegt. <see cref="ImagePairId"/> trägt die Frage zurück, damit der Client eine späte Antwort
/// gegen den aktuellen Cursor prüfen und verwerfen kann.
/// </summary>
public record DatensaetzeFuerPaar(
    Guid ImagePairId,
    IReadOnlyList<DatensatzTag> Tags
) : IQueryResponse;
