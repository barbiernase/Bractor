using Abstractions;

namespace Pruefdomaene.Abrechnung;

public partial class Kundenkonto
{
    public partial class Decider : IDecider<Kundenkonto>
    {
        public IEnumerable<OneOf<GutschriftGebucht>> Decide(BucheGutschrift cmd)
        {
            yield return new GutschriftGebucht(cmd.Rechnung, cmd.Betrag);
        }

        public IEnumerable<OneOf<MahnungVermerkt>> Decide(VermerkeMahnung cmd)
        {
            yield return new MahnungVermerkt(cmd.Rechnung, cmd.Versand, cmd.Stufe);
        }
    }
}
