using Abstractions;

namespace Pruefdomaene.Auftrag;

public partial class Auftrag
{
    public partial class Applier : IApplier<Auftrag>
    {
        public void Apply(AuftragGestartet evt) { }
        public void Apply(AuftragBestaetigt evt) => this.State.Bestaetigt = true;
    }
}
