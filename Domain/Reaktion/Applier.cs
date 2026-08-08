using Abstractions;

namespace Domain.Reaktion;

public partial class Reaktionsempfaenger
{
    public partial class Applier : IApplier<Reaktionsempfaenger>
    {
        public void Apply(ReaktionGewirkt evt)
        {
            this.State.VerarbeiteteReaktionen.Add(evt.ReaktionId);
            this.State.Wirkungen++;
        }
    }
}
