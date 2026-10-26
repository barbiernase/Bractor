using Abstractions;

namespace Pruefdomaene.Import;

public partial class Importauftrag
{
    public partial class Applier : IApplier<Importauftrag>
    {
        public void Apply(ImportGestartet evt) => this.State.Zeilen = evt.Zeilen;
        public void Apply(ImportStandGemeldet evt) => this.State.Stand = evt.Zeilen;
    }
}
