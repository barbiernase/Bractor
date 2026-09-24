using Abstractions;

namespace Domain.Teilauftrag;

public partial class Teilauftrag
{
    public partial class Decider : IDecider<Teilauftrag>
    {
        public IEnumerable<OneOf<TeilauftragGestartet, TeilauftragExistiertBereits>> Decide(
            StarteTeilauftrag cmd)
        {
            if (this.State.Existiert)
            {
                yield return new TeilauftragExistiertBereits(cmd.AggregateId);
                yield break;
            }

            yield return new TeilauftragGestartet(cmd.SammelvorgangId);
        }

        public IEnumerable<OneOf<TeilauftragAbgeschlossen, TeilauftragNichtGefunden, TeilauftragBereitsFertig>> Decide(
            SchließeTeilauftragAb cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TeilauftragNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.Fertig)
            {
                yield return new TeilauftragBereitsFertig(cmd.AggregateId);
                yield break;
            }

            yield return new TeilauftragAbgeschlossen(this.State.SammelvorgangId);
        }
    }
}
