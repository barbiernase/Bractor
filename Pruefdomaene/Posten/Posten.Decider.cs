using Abstractions;

namespace Pruefdomaene.Posten;

public partial class Posten
{
    public partial class Decider : IDecider<Posten>
    {
        public IEnumerable<OneOf<PostenEroeffnet, PostenExistiertBereits>> Decide(EroeffnePosten cmd)
        {
            if (this.State.Existiert) { yield return new PostenExistiertBereits(cmd.AggregateId); yield break; }
            yield return new PostenEroeffnet(cmd.Bestand);
        }

        public IEnumerable<OneOf<PostenReserviert, PostenNichtGefunden, PostenReichtNicht>> Decide(ReservierePosten cmd)
        {
            if (!this.State.Existiert) { yield return new PostenNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Verfuegbar < cmd.Menge)
            {
                yield return new PostenReichtNicht(cmd.AggregateId, this.State.Verfuegbar, cmd.Menge);
                yield break;
            }
            yield return new PostenReserviert(cmd.Menge);
        }

        public IEnumerable<OneOf<PostenFreigegeben, PostenNichtGefunden>> Decide(GibPostenFrei cmd)
        {
            if (!this.State.Existiert) { yield return new PostenNichtGefunden(cmd.AggregateId); yield break; }
            yield return new PostenFreigegeben(cmd.Menge);
        }

        public IEnumerable<OneOf<PostenZugebucht, PostenNichtGefunden>> Decide(BuchePostenZu cmd)
        {
            if (!this.State.Existiert) { yield return new PostenNichtGefunden(cmd.AggregateId); yield break; }
            yield return new PostenZugebucht(cmd.Menge);
        }
    }
}
