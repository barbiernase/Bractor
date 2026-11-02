using Abstractions;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Auftrag;

/// <summary>
/// Die Kanten des Auftrags (docs/13 §13.7): ein gestarteter Auftrag reserviert den Posten und belegt das Kontingent; was Posten und Kontingent darauf
/// entscheiden, meldet sie dem Auftrag zurück; entscheidet der Auftrag einen Gegenzug, sendet sie ihn. Ihre Eingänge sind Commands ohne Ziel aus dem
/// Decide (<see cref="AuftragAbzuwickeln"/>, <see cref="PostenFreizugeben"/>, <see cref="KontingentZuEntlasten"/>); die Events hört sie als ANTWORT
/// auf ihre eigenen Commands (das Ziel entschied sie), nie aus dem Log. Warten, Join und Kompensation entscheidet das Aggregat.
/// </summary>
public sealed class Auftragsabwicklung : IFunktion
{
    public IEnumerable<OneOf<ReservierePosten, BelegeKontingent>> Auf(AuftragAbzuwickeln e)
    {
        var beleg = new Beleg(Belegart.Auftrag, e.AuftragId.Wert);
        yield return new ReservierePosten(e.PostenId, e.Menge, beleg);
        yield return new BelegeKontingent(e.KontingentId, e.Menge, beleg);
    }

    public VermerkeReservierung Auf(PostenReserviert e) => new(new(e.Beleg.Nummer));

    public VermerkeAbsage Auf(ReservierungAbgelehnt e) => new(new(e.Beleg.Nummer), $"Posten: {e.Verfuegbar} verfügbar, {e.Angefordert} angefordert");

    public VermerkeBelegung Auf(KontingentBelegt e) => new(new(e.Beleg.Nummer));

    public VermerkeAbsage Auf(BelegungAbgelehnt e) => new(new(e.Beleg.Nummer), $"Kontingent: {e.Frei} frei, {e.Angefordert} angefordert");

    public GibPostenFrei Auf(PostenFreizugeben c) => new(c.PostenId, c.Menge, new Beleg(Belegart.Auftrag, c.AuftragId.Wert));

    public EntlasteKontingent Auf(KontingentZuEntlasten c) => new(c.KontingentId, c.Menge, new Beleg(Belegart.Auftrag, c.AuftragId.Wert));
}
