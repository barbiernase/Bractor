using Abstractions;

namespace Pruefdomaene.Messung;

/// <summary>Eine FOLGE (docs/03 §3.2): ein Messwert ist erfasst — die Merkmalsbildung macht daraus ein Merkmal.</summary>
public record NeuerWert(Id<Messstelle> Stelle, double Wert);
