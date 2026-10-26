using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Posten;

/// <summary>
/// Drückt eine Reservierung den verfügbaren Bestand eines Postens unter seinen Mindestbestand, wird die Fehlmenge nachgebucht (früher eine Reaktion).
/// Sie hört die langlebigen Posten-Streams (jede Reservierung eines Auftrags oder einer Verteilung landet dort) — der Prüfling dafür, ob die Kosten
/// einer Funktion mit der Stream-Länge wachsen. Gleichbleibend (R12): derselbe Eingang ergibt dieselbe Zubuchung mit derselben Command-Id.
/// </summary>
public sealed class Nachschub : IFunktion
{
    public BuchePostenZu? Auf(PostenReserviert e, Herkunft h) =>
        e.Fehlmenge > 0 ? new BuchePostenZu(new(h.Stream), e.Fehlmenge, Beleg.Ohne) : null;
}
