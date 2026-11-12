using Abstractions;

namespace Pruefwerk.Katalog.Messung;

/// <summary>Meldet ein Merkmal über der Schwelle — ein Strom: nichts oder ein Ausreißer.</summary>
public sealed class Ausreisserpruefung : IFunktion
{
    public const double Schwelle = 1_000;

    public async IAsyncEnumerable<Ausreisser> Auf(Merkmal m)
    {
        if (m.Wert > Schwelle) yield return new Ausreisser(m.Stelle, m.Wert);
    }
}
