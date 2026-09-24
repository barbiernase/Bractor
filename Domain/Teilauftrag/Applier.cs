using Abstractions;

namespace Domain.Teilauftrag;

public partial class Teilauftrag
{
    public partial class Applier : IApplier<Teilauftrag>
    {
        public void Apply(TeilauftragGestartet evt)
        {
            this.State.SammelvorgangId = evt.SammelvorgangId;
        }

        public void Apply(TeilauftragAbgeschlossen evt)
        {
            this.State.Fertig = true;
        }
    }
}
