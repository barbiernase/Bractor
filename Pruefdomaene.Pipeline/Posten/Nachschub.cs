using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Posten;

/// <summary>
/// Drückt eine Reservierung den verfügbaren Bestand eines Postens unter seinen Mindestbestand, wird die Fehlmenge nachgebucht. Eine Funktion vor dem
/// Aggregat (docs/04 §4.4): sie hört die Folge <see cref="Unterschritten"/> des Decide — nicht das Log —, je Entscheidung genau einen offenen Eingang;
/// die Command-Id folgt aus seiner Kennung, die Inbox des Postens verwirft eine Wiederholung.
/// </summary>
public sealed class Nachschub : IFunktion
{
    public BuchePostenZu Auf(Unterschritten f) => new(f.Posten, f.Fehlmenge, Beleg.Ohne);
}
