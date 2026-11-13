using Abstractions;

namespace Pruefwerk.Katalog;

/// <summary>Macht aus einem Messwert sein Merkmal — der Kopf der Merkmalbildung, die Gabel dahinter steht im Graphen.</summary>
public sealed class Merkmalsbildung : IFunktion
{
    public Merkmal Auf(Messwert m) => new(m.Stelle, m.Wert);
}
