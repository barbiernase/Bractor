using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Posten;

/// <summary>
/// Drückt eine Reservierung den verfügbaren Bestand eines Postens unter seinen Mindestbestand, wird die Fehlmenge nachgebucht. Eine Funktion vor dem
/// Aggregat (docs/04 §4.4): sie nimmt <see cref="Unterschritten"/> am Kopf (ein Client, der auf die Reservierung reagiert, gibt ihn hinein); die
/// Command-Id folgt aus der Kennung des Eingangs, die Inbox des Postens verwirft eine Wiederholung.
/// </summary>
public sealed class Nachschub : IFunktion
{
    public BuchePostenZu Auf(Unterschritten f) => new(f.Posten, f.Fehlmenge, Beleg.Ohne);
}
