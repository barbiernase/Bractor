using Abstractions;

namespace Domain.ImagePair;

/// <summary>Der Stand eines Bildpaars aus dem Aggregat: komplett, Label des Menschen, inspiziert.</summary>
public record BildPaarStand(Id<ImagePair> Paar, bool Komplett, Klassifikation? Label, bool Inspiziert) : IQueryResponse;

/// <summary>Das Bildpaar gibt es (noch) nicht.</summary>
public record BildPaarUnbekannt(Id<ImagePair> Paar) : IQueryResponse;
