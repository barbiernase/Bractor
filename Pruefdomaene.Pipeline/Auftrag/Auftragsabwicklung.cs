using Abstractions;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Auftrag;

/// <summary>
/// Die Kanten des Auftrags (docs/13 §13.7): ein gestarteter Auftrag reserviert den Posten und belegt das Kontingent;
/// was Posten und Kontingent darauf entscheiden, meldet sie dem Auftrag zurück; ordnet der Auftrag einen Gegenzug an, sendet sie ihn. Eine Funktion vor
/// dem Aggregat (docs/04 §4.4): ihr Eingang ist die Folge <see cref="AuftragAbzuwickeln"/>, die Events hört sie als ANTWORT auf ihre eigenen Commands
/// (das Ziel entschied sie), nie aus dem Log — die Prüfung des Belegs bleibt, weil Posten und Kontingent jede Reservierung so beantworten. Warten,
/// Join und Kompensation entscheidet das Aggregat; die Funktion kennt keinen Zustand.
/// </summary>
public sealed class Auftragsabwicklung : IFunktion
{
    public IEnumerable<OneOf<ReservierePosten, BelegeKontingent>> Auf(AuftragAbzuwickeln e)
    {
        var beleg = new Beleg(Belegart.Auftrag, e.AuftragId.Wert);
        yield return new ReservierePosten(e.PostenId, e.Menge, beleg);
        yield return new BelegeKontingent(e.KontingentId, e.Menge, beleg);
    }

    public VermerkeReservierung? Auf(PostenReserviert e) =>
        e.Beleg.Ist(Belegart.Auftrag) ? new VermerkeReservierung(new(e.Beleg.Nummer)) : null;

    public VermerkeAbsage? Auf(ReservierungAbgelehnt e) =>
        e.Beleg.Ist(Belegart.Auftrag) ? new VermerkeAbsage(new(e.Beleg.Nummer), $"Posten: {e.Verfuegbar} verfügbar, {e.Angefordert} angefordert") : null;

    public VermerkeBelegung? Auf(KontingentBelegt e) =>
        e.Beleg.Ist(Belegart.Auftrag) ? new VermerkeBelegung(new(e.Beleg.Nummer)) : null;

    public VermerkeAbsage? Auf(BelegungAbgelehnt e) =>
        e.Beleg.Ist(Belegart.Auftrag) ? new VermerkeAbsage(new(e.Beleg.Nummer), $"Kontingent: {e.Frei} frei, {e.Angefordert} angefordert") : null;

    public GibPostenFrei Auf(PostenFreigabeAngeordnet e, Herkunft h) => new(e.PostenId, e.Menge, new Beleg(Belegart.Auftrag, h.Stream));

    public EntlasteKontingent Auf(KontingentEntlastungAngeordnet e, Herkunft h) => new(e.KontingentId, e.Menge, new Beleg(Belegart.Auftrag, h.Stream));
}
