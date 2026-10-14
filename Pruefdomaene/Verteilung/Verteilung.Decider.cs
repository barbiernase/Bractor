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

        public IEnumerable<OneOf<VerteilungAbgeschlossen, VerteilungNichtGefunden>> Decide(SchliesseVerteilungAb cmd)
        {
            if (!this.State.Existiert) { yield return new VerteilungNichtGefunden(cmd.AggregateId); yield break; }
            yield return new VerteilungAbgeschlossen(cmd.Zugebucht);
        }
    }
}
