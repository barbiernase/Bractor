using Abstractions;

namespace Pruefdomaene.Verteilung;

public partial class Verteilung
{
    public partial class Decider : IDecider<Verteilung>
    {
        public IEnumerable<OneOf<VerteilungGestartet, VerteilungAbzuwickeln, VerteilungExistiertBereits>> Decide(StarteVerteilung cmd)
        {
            if (this.State.Existiert) { yield return new VerteilungExistiertBereits(cmd.AggregateId); yield break; }
            yield return new VerteilungGestartet(cmd.AggregateId, cmd.QuelleId, cmd.Ziele, cmd.MengeJeZiel);
            yield return new VerteilungAbzuwickeln(cmd.AggregateId, cmd.QuelleId, cmd.Ziele, cmd.MengeJeZiel);
        }

        public IEnumerable<OneOf<QuelleReserviert, Zubuchen, VerteilungNichtGefunden>> Decide(VermerkeQuellreservierung cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.QuelleReserviert || this.State.Gescheitert) yield break;
            yield return new QuelleReserviert();
            yield return new Zubuchen(cmd.AggregateId, this.State.Ziele, this.State.MengeJeZiel);
        }

        public IEnumerable<OneOf<VerteilungGescheitert, VerteilungNichtGefunden>> Decide(VermerkeQuellabsage cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.QuelleReserviert || this.State.Gescheitert) yield break;
            yield return new VerteilungGescheitert(cmd.Verfuegbar);
        }

        /// <summary>Je gebuchtes Ziel ein Vermerk; der letzte schließt die Verteilung ab (einmal: danach ändert nichts mehr).</summary>
        public IEnumerable<OneOf<ZielZugebucht, VerteilungAbgeschlossen, VerteilungNichtGefunden>> Decide(VermerkeZubuchung cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Abgeschlossen) yield break;
            yield return new ZielZugebucht(cmd.Menge);
            if (this.State.ZieleGebucht + 1 == this.State.Ziele.Count) yield return new VerteilungAbgeschlossen(this.State.Zugebucht + cmd.Menge);
        }
    }
}
