using Abstractions;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>
/// Die Kanten der Verteilung: die Quelle reserviert die Gesamtmenge; gelingt es, gibt die Verteilung <see cref="Zubuchen"/> aus, und diese Funktion
/// bucht jedes Ziel zu (FAN-OUT als Strom von Commands); jede Zubuchung wird dort gezählt (COUNT-JOIN im Zähl-Aggregat). Ihre Eingänge sind die
/// Commands <see cref="VerteilungAbzuwickeln"/> und <see cref="Zubuchen"/> aus dem Decide; die Events hört sie als Antwort auf ihre eigenen Commands.
/// </summary>
public sealed class Verteilabwicklung : IFunktion
{
    public ReservierePosten Auf(VerteilungAbzuwickeln e) =>
        new(e.QuelleId, e.MengeJeZiel * e.Ziele.Count, new Beleg(Belegart.Verteilung, e.VerteilungId.Wert));

    public VermerkeQuellreservierung Auf(PostenReserviert e) => new(new(e.Beleg.Nummer));

    public VermerkeQuellabsage Auf(ReservierungAbgelehnt e) => new(new(e.Beleg.Nummer), e.Verfuegbar);

    public IEnumerable<BuchePostenZu> Auf(Zubuchen c) =>
        c.Ziele.Select(z => new BuchePostenZu(z, c.MengeJeZiel, new Beleg(Belegart.Verteilung, c.VerteilungId.Wert)));

    public ZaehleZubuchung Auf(PostenZugebucht e) => new(new(e.Beleg.Nummer), e.Menge);
}
