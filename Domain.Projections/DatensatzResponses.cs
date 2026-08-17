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
    IReadOnlyList<RangeHerkunft> Ranges
) : IQueryResponse;

public record DatensatzNichtGefunden(Guid DatensatzId) : IQueryResponse;
