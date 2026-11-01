using Abstractions;

namespace Pruefdomaene.Messung;

public partial class Messstelle
{
    public partial class Decider : IDecider<Messstelle>
    {
        public IEnumerable<OneOf<MesswertErfasst, NeuerWert>> Decide(ErfasseMesswert cmd)
        {
            yield return new MesswertErfasst(cmd.Wert);
            yield return new NeuerWert(cmd.AggregateId, cmd.Wert);
        }

        public IEnumerable<OneOf<SchaetzungVermerkt>> Decide(VermerkeSchaetzung cmd)
        {
            yield return new SchaetzungVermerkt(cmd.Erwartet, cmd.Anzahl);
        }

        public IEnumerable<OneOf<AusreisserGemeldet>> Decide(MeldeAusreisser cmd)
        {
            yield return new AusreisserGemeldet(cmd.Wert);
        }
    }
}
