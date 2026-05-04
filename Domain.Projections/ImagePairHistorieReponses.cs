using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Antwort auf GetImagePairHistorie — enthält die komplette Timeline.
/// </summary>
public record ImagePairHistorieAntwort(
    Guid PairId,
    IReadOnlyList<HistorieEintrag> Eintraege
) : IQueryResponse;

/// <summary>
/// Antwort wenn das ImagePair nicht in der Historie-Projektion existiert.
/// Kann vorkommen wenn die Projektion noch nicht aufgeholt hat.
/// </summary>
public record ImagePairHistorieNichtGefunden(
    Guid PairId
) : IQueryResponse;