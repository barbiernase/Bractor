using Abstractions;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>
/// Der Anfang der Verteilung: die Quelle reserviert die Gesamtmenge; ihr Eingang ist der Command <see cref="VerteilungAbzuwickeln"/> aus dem Decide,
/// die Events hört sie als Antwort auf ihren eigenen Command. Gelingt es, gibt die Verteilung <see cref="Zubuchen"/> aus — den Kopf eines neuen Laufs
/// (<see cref="Zielaufteilung"/>).
/// </summary>
public sealed class Verteilabwicklung : IFunktion
{
    public ReservierePosten Auf(VerteilungAbzuwickeln e) =>
        new(e.QuelleId, e.MengeJeZiel * e.Ziele.Count, new Beleg(Belegart.Verteilung, e.VerteilungId.Wert));

    public VermerkeQuellreservierung Auf(PostenReserviert e) => new(new(e.Beleg.Nummer));

    public VermerkeQuellabsage Auf(ReservierungAbgelehnt e) => new(new(e.Beleg.Nummer), e.Verfuegbar);
}
