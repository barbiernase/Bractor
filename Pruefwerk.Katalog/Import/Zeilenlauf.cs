using System.Runtime.CompilerServices;
using Abstractions;

namespace Pruefwerk.Katalog.Import;

/// <summary>
/// Liest die Zeilen eines Vorgangs als STROM (je Element ein Unterlauf): je Zeile eine neue Id mit ihrem Wert, alle <see cref="StandAlle"/> Zeilen und am
/// Ende der Stand. Die Ids kommen aus dem Eingang und der Zeile — dieselben bei jeder Wiederholung.
/// </summary>
public sealed class Zeilenlauf(IZeilenquelle quelle) : IFunktion
{
    public const int StandAlle = 10;

    public async IAsyncEnumerable<OneOf<Gelesen, Stand>> Auf(Laufen l, Herkunft h, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < l.Menge; i++)
        {
            var wert = await quelle.LiesAsync(l.Vorgang, i, ct);
            yield return new Gelesen(h.NeueId("zeile:" + i), wert);
            if ((i + 1) % StandAlle == 0 || i + 1 == l.Menge) yield return new Stand(l.Vorgang, i + 1);
        }
    }
}
