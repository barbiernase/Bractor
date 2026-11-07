using Abstractions;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>
/// Der Anfang der Verteilung: die Quelle reserviert die Gesamtmenge; ihr Eingang ist der Command <see cref="VerteilungAbzuwickeln"/> (ein Client
/// reagiert damit auf <c>VerteilungGestartet</c>), die Events hört sie als Antwort auf ihren eigenen Command. Gelingt es, steht <c>QuelleReserviert</c>
/// im Log, und der Client gibt <see cref="Zubuchen"/> an den Kopf eines neuen Laufs (<see cref="Zielaufteilung"/>).
/// </summary>
public sealed class Verteilabwicklung : IFunktion
{
    public ReservierePosten Auf(VerteilungAbzuwickeln e) =>
        new(e.QuelleId, e.MengeJeZiel * e.Ziele.Count, new Beleg(Belegart.Verteilung, e.VerteilungId.Wert));

    public VermerkeQuellreservierung Auf(PostenReserviert e) => new(new(e.Beleg.Nummer));

    public VermerkeQuellabsage Auf(ReservierungAbgelehnt e) => new(new(e.Beleg.Nummer), e.Verfuegbar);
}
