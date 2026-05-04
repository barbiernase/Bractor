using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Lädt die komplette Historie eines ImagePairs.
///
/// Wird vom Client beim Paar-Wechsel publiziert.
/// QueryBridge → gRPC → ImagePairHistorieReader → Store.
/// </summary>
public record GetImagePairHistorie(Guid PairId) : IQuery;