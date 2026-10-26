using Abstractions;

namespace Pruefdomaene.Kontingent;

public partial class Kontingent
{
    public partial class Applier : IApplier<Kontingent>
    {
        public void Apply(KontingentEingerichtet evt) => this.State.Frei = evt.Plaetze;
        public void Apply(KontingentBelegt evt) => this.State.Frei -= evt.Plaetze;
        public void Apply(BelegungAbgelehnt evt) { }
        public void Apply(KontingentEntlastet evt) => this.State.Frei += evt.Plaetze;
    }
}
