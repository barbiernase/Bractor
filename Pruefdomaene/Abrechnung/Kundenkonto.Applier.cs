using Abstractions;

namespace Pruefdomaene.Abrechnung;

public partial class Kundenkonto
{
    public partial class Applier : IApplier<Kundenkonto>
    {
        public void Apply(GutschriftGebucht evt) { this.State.Guthaben += evt.Betrag; this.State.Gutschriften++; }
        public void Apply(MahnungVermerkt evt) => this.State.Mahnungen++;
    }
}
