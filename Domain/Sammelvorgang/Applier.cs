using Abstractions;

namespace Domain.Sammelvorgang;

public partial class Sammelvorgang
{
    public partial class Applier : IApplier<Sammelvorgang>
    {
        public void Apply(SammelvorgangGestartet evt)
        {
            this.State.Erwartet = evt.Anzahl;
        }

        public void Apply(TeilVerbucht evt)
        {
            this.State.Fertig = evt.Fertig;
        }

        public void Apply(SammelvorgangAbgeschlossen evt)
        {
            this.State.Abgeschlossen = true;
        }
    }
}
