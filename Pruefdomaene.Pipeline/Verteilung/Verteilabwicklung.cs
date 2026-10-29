using Abstractions;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>
/// Die Kanten der Verteilung: die Quelle reserviert die Gesamtmenge; gelingt es, ordnet die Verteilung die
/// Zubuchungen an, und diese Funktion bucht jedes Ziel zu (FAN-OUT als Strom von Commands); jede Zubuchung mit dem Beleg der Verteilung wird dort
/// gezählt (COUNT-JOIN im Zähl-Aggregat). Gleichbleibend (R12).
/// </summary>
public sealed class Verteilabwicklung : IFunktion
{
    public ReservierePosten Auf(VerteilungGestartet e) =>
        new(e.QuelleId, e.MengeJeZiel * e.Ziele.Count, new Beleg(Belegart.Verteilung, e.VerteilungId.Wert));

    public VermerkeQuellreservierung? Auf(PostenReserviert e) =>
        e.Beleg.Ist(Belegart.Verteilung) ? new VermerkeQuellreservierung(new(e.Beleg.Nummer)) : null;

    public VermerkeQuellabsage? Auf(ReservierungAbgelehnt e) =>
        e.Beleg.Ist(Belegart.Verteilung) ? new VermerkeQuellabsage(new(e.Beleg.Nummer), e.Verfuegbar) : null;

    public IEnumerable<BuchePostenZu> Auf(ZubuchungenAngeordnet e, Herkunft h) =>
        e.Ziele.Select(z => new BuchePostenZu(z, e.MengeJeZiel, new Beleg(Belegart.Verteilung, h.Stream)));

    public ZaehleZubuchung? Auf(PostenZugebucht e) =>
        e.Beleg.Ist(Belegart.Verteilung) ? new ZaehleZubuchung(new(e.Beleg.Nummer), e.Menge) : null;
}
