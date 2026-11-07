using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Aus jedem neuen Wert (ein Client reagiert damit auf <c>MesswertErfasst</c>) ein Merkmal an die Knoten dahinter — sie liest nie das Log.</summary>
public sealed class Merkmalsbildung : IFunktion
{
    public Merkmal Auf(NeuerWert m) => new(m.Stelle, m.Wert);
}
