using Abstractions;

namespace Domain.ImagePair;

// FOLGEN des Bildpaars (docs/03 §3.2): was eine Entscheidung den Funktionen sagt, die sie hören — schlichte Records, nicht im Log.

/// <summary>Das Paar ist komplett: es ist mit dem aktiven Modell zu beurteilen.</summary>
public record PaarZuBeurteilen(Id<ImagePair> Paar);
