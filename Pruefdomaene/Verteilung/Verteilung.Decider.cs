using Abstractions;

namespace Pruefdomaene.Verteilung;

public partial class Verteilung
{
    public partial class Decider : IDecider<Verteilung>
    {
        public IEnumerable<OneOf<VerteilungGestartet, VerteilungExistiertBereits>> Decide(StarteVerteilung cmd)
        {
            if (this.State.Existiert) { yield return new VerteilungExistiertBereits(cmd.AggregateId); yield break; }
            yield return new VerteilungGestartet(cmd.AggregateId, cmd.QuelleId, cmd.Ziele, cmd.MengeJeZiel);
        }

        public IEnumerable<OneOf<ZubuchungenAngeordnet, VerteilungNichtGefunden>> Decide(VermerkeQuellreservierung cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Angeordnet || this.State.Gescheitert) yield break;
            yield return new ZubuchungenAngeordnet(this.State.Ziele, this.State.MengeJeZiel);
        }

        public IEnumerable<OneOf<VerteilungGescheitert, VerteilungNichtGefunden>> Decide(VermerkeQuellabsage cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Angeordnet || this.State.Gescheitert) yield break;
            yield return new VerteilungGescheitert(cmd.Verfuegbar);
        }

        /// <summary>Zählt ein angekommenes Ziel; beim N-ten schließt die Verteilung ab (danach zählt nichts mehr).</summary>
        public IEnumerable<OneOf<ZubuchungGezaehlt, VerteilungAbgeschlossen, VerteilungNichtGefunden>> Decide(ZaehleZubuchung cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Abgeschlossen) yield break;
            yield return new ZubuchungGezaehlt(cmd.Menge);
            if (this.State.Gezaehlt + 1 >= this.State.Ziele.Count) yield return new VerteilungAbgeschlossen(this.State.Zugebucht + cmd.Menge);
        }
    }
}
