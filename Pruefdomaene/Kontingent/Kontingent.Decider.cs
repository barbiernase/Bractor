using Abstractions;

namespace Pruefdomaene.Kontingent;

public partial class Kontingent
{
    public partial class Decider : IDecider<Kontingent>
    {
        public IEnumerable<OneOf<KontingentEingerichtet, KontingentExistiertBereits>> Decide(RichteKontingentEin cmd)
        {
            if (this.State.Existiert) { yield return new KontingentExistiertBereits(cmd.AggregateId); yield break; }
            yield return new KontingentEingerichtet(cmd.Plaetze);
        }

        public IEnumerable<OneOf<KontingentBelegt, BelegungAbgelehnt, KontingentNichtGefunden>> Decide(BelegeKontingent cmd)
        {
            if (!this.State.Existiert) { yield return new KontingentNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Frei < cmd.Plaetze) { yield return new BelegungAbgelehnt(this.State.Frei, cmd.Plaetze, cmd.Beleg); yield break; }
            yield return new KontingentBelegt(cmd.Plaetze, cmd.Beleg);
        }

        public IEnumerable<OneOf<KontingentEntlastet, KontingentNichtGefunden>> Decide(EntlasteKontingent cmd)
        {
            if (!this.State.Existiert) { yield return new KontingentNichtGefunden(cmd.AggregateId); yield break; }
            yield return new KontingentEntlastet(cmd.Plaetze, cmd.Beleg);
        }

        /// <summary>Die direkte Abfrage (docs/03 §3.2): die freien Plätze, auf denen die nächste Belegung entschieden würde.</summary>
        public OneOf<KontingentStand, KontingentUnbekannt> Frage(HoleKontingent q) =>
            this.State.Existiert ? new KontingentStand(q.Kontingent, this.State.Frei) : new KontingentUnbekannt(q.Kontingent);
    }
}
