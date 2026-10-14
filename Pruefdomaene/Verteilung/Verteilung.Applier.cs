using Abstractions;

namespace Pruefdomaene.Verteilung;

public partial class Verteilung
{
    public partial class Applier : IApplier<Verteilung>
    {
        public void Apply(VerteilungGestartet evt) { }
        public void Apply(VerteilungAbgeschlossen evt) { this.State.Abgeschlossen = true; this.State.Zugebucht = evt.Zugebucht; }
    }
}
