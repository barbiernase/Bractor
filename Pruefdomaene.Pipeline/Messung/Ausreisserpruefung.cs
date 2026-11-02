using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Ein Merkmal über der Schwelle ist ein Ausreißer — an die Messstelle gemeldet.</summary>
public sealed class Ausreisserpruefung : IFunktion
{
    public const double Schwelle = 1_000;

    public IEnumerable<MeldeAusreisser> Auf(Merkmal m)
    {
        if (m.Wert > Schwelle) yield return new MeldeAusreisser(m.Stelle, m.Wert);
    }
}
