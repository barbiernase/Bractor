using Abstractions;

namespace Domain.Teilauftrag;

// FOLGEN des Teilauftrags (docs/03 §3.2): was eine Entscheidung den Funktionen sagt, die sie hören — schlichte Records, nicht im Log.

/// <summary>Das Teil ist fertig: es meldet sich an seiner Barriere.</summary>
public record TeilFertig(Id<Sammelvorgang.Sammelvorgang> Sammelvorgang);
