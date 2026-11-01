using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Aus jedem neuen Wert (der Folge des Decide, docs/03 §3.2) ein Merkmal ins Hinweis-Netz — eine Funktion vor dem Aggregat, sie liest nie das Log.</summary>
public sealed class Merkmalsbildung : IFunktion
{
    public Merkmal Auf(NeuerWert m) => new(m.Stelle, m.Wert);
}
