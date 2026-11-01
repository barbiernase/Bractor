using System.Runtime.CompilerServices;
using Abstractions;
using Pruefdomaene.Import;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Import;

/// <summary>
/// Eine Funktion mit Strom-Ausgabe: aus EINEM gestarteten Import (seine Folge) werden viele Posten, zwischendurch der Stand. Das Kind sendet jede
/// Ausgabe, bevor der Strom weiterläuft; nach einem Absturz rechnet es den Eingang von vorn — die Ids der Posten sind aus der Herkunft abgeleitet
/// (R13), beim Wiederholen dieselben, die Inbox jedes Postens verwirft das Zweite.
/// </summary>
public sealed class Importlauf(IZeilenquelle quelle) : IFunktion
{
    /// <summary>Nach so vielen Zeilen meldet der Lauf den Stand.</summary>
    public const int StandAlle = 10;

    public async IAsyncEnumerable<OneOf<EroeffnePosten, MeldeImportStand>> Auf(ImportZuLaufen f, Herkunft h,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < f.Zeilen; i++)
        {
            var bestand = await quelle.LiesAsync(f.Import.Wert, i, ct);
            yield return new EroeffnePosten(h.NeueId<Pruefdomaene.Posten.Posten>(i), bestand);
            if ((i + 1) % StandAlle == 0 || i + 1 == f.Zeilen) yield return new MeldeImportStand(f.Import, i + 1);
        }
    }
}
