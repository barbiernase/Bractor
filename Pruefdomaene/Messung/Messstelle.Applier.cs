using Abstractions;

namespace Pruefdomaene.Messung;

public partial class Messstelle
{
    public partial class Applier : IApplier<Messstelle>
    {
        public void Apply(MesswertErfasst evt) => this.State.Messwerte++;
        public void Apply(SchaetzungVermerkt evt) => this.State.Schaetzungen++;
        public void Apply(AusreisserGemeldet evt) => this.State.Ausreisser++;
    }
}
