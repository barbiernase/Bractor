using Abstractions;

namespace Pruefdomaene.Auftrag;

public partial class Auftrag
{
    public partial class Decider : IDecider<Auftrag>
    {
        public IEnumerable<OneOf<AuftragGestartet, AuftragExistiertBereits>> Decide(StarteAuftrag cmd)
        {
            if (this.State.Existiert) { yield return new AuftragExistiertBereits(cmd.AggregateId); yield break; }
            yield return new AuftragGestartet(cmd.AggregateId, cmd.PostenId, cmd.KontingentId, cmd.Menge);
        }

        public IEnumerable<OneOf<AuftragBestaetigt, AuftragNichtGefunden>> Decide(BestaetigeAuftrag cmd)
        {
            if (!this.State.Existiert) { yield return new AuftragNichtGefunden(cmd.AggregateId); yield break; }
            yield return new AuftragBestaetigt();
        }
    }
}
