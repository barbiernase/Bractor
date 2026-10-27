using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Aus jedem Messwert im Log ein Merkmal ins Hinweis-Netz — gleichbleibend (R12): nichts festgehalten, nichts im Log außer dem Eingang.</summary>
public sealed class Merkmalsbildung : IFunktion
{
    public Merkmal Auf(MesswertErfasst e, Herkunft h) => new(new(h.Stream), e.Wert);
}
