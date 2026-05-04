using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

// ─── Client-Events (lokal, nie gRPC) ───

/// <summary>
/// Wird vom ImagePairStatistikHandler emittiert
/// wenn sich die Zähler ändern.
/// </summary>
public record ImagePairStatistikBerechnet(
    int AnzahlGesamt,
    int AnzahlKomplett,
    int AnzahlMitKiKlassifikation,
    int AnzahlOhneKlassifikation,
    int AnzahlAnomalie
) : IClientEvent;

/// <summary>
/// Wird emittiert wenn der Nutzer den Filter ändert.
/// Löst eine neue SucheImagePairs-Query aus.
/// </summary>
public record ImagePairFilterGeaendert(
    ImagePairFilter Filter
) : IClientEvent;

/// <summary>
/// Wird emittiert wenn der Nutzer ein ImagePair zur
/// Detailansicht auswählt.
/// </summary>
public record ImagePairAusgewaehlt(
    Guid PairId
) : IClientEvent;