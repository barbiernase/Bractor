using System.Runtime.CompilerServices;
using Abstractions;
using Pruefdomaene.Import;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Import;

/// <summary>
/// Eine Funktion mit Strom-Ausgabe: aus EINEM gestarteten Import werden viele Posten, zwischendurch der Stand. Das Framework hält stückweise fest
/// und sendet jedes Stück, bevor der Strom weiterläuft; nach einem Absturz setzt es bei der nächsten Ausgabe fort (Fortschritt). Die Ids der
/// Posten sind aus der Herkunft abgeleitet (R13) — beim Wiederholen dieselben.
/// </summary>
public sealed class Importlauf(IZeilenquelle quelle) : IFunktion
{
    /// <summary>Nach so vielen Zeilen meldet der Lauf den Stand.</summary>
    public const int StandAlle = 10;

    public async IAsyncEnumerable<OneOf<EroeffnePosten, MeldeImportStand>> Auf(ImportGestartet e, Herkunft h,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < e.Zeilen; i++)
        {
            var bestand = await quelle.LiesAsync(h.Stream, i, ct);
            yield return new EroeffnePosten(h.NeueId<Posten.Posten>(i), bestand);
            if ((i + 1) % StandAlle == 0 || i + 1 == e.Zeilen) yield return new MeldeImportStand(new(h.Stream), i + 1);
        }
    }
}
