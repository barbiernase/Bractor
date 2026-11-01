using Abstractions;

namespace Pruefdomaene.Posten;

public partial class Posten
{
    public partial class Decider : IDecider<Posten>
    {
        public IEnumerable<OneOf<PostenEroeffnet, PostenExistiertBereits>> Decide(EroeffnePosten cmd)
        {
            if (this.State.Existiert) { yield return new PostenExistiertBereits(cmd.AggregateId); yield break; }
            yield return new PostenEroeffnet(cmd.Bestand, cmd.Mindestbestand);
        }

        public IEnumerable<OneOf<PostenReserviert, Unterschritten, ReservierungAbgelehnt, PostenNichtGefunden>> Decide(ReservierePosten cmd)
        {
            if (!this.State.Existiert) { yield return new PostenNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Verfuegbar < cmd.Menge)
            {
                yield return new ReservierungAbgelehnt(this.State.Verfuegbar, cmd.Menge, cmd.Beleg);
                yield break;
            }
            var fehlmenge = Math.Max(0, this.State.Mindestbestand - (this.State.Verfuegbar - cmd.Menge));
            yield return new PostenReserviert(cmd.Menge, fehlmenge, cmd.Beleg);
            if (fehlmenge > 0) yield return new Unterschritten(cmd.AggregateId, fehlmenge);   // eine Folge: an die Funktionen, die sie hören
        }

        public IEnumerable<OneOf<PostenFreigegeben, PostenNichtGefunden>> Decide(GibPostenFrei cmd)
        {
            if (!this.State.Existiert) { yield return new PostenNichtGefunden(cmd.AggregateId); yield break; }
            yield return new PostenFreigegeben(cmd.Menge, cmd.Beleg);
        }

        public IEnumerable<OneOf<PostenZugebucht, PostenNichtGefunden>> Decide(BuchePostenZu cmd)
        {
            if (!this.State.Existiert) { yield return new PostenNichtGefunden(cmd.AggregateId); yield break; }
            yield return new PostenZugebucht(cmd.Menge, cmd.Beleg);
        }
    }
}
