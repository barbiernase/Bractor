using Abstractions;

namespace Pruefdomaene.Posten;

public partial class Posten
{
    public partial class Applier : IApplier<Posten>
    {
        public void Apply(PostenEroeffnet evt) { this.State.Bestand = evt.AnfangsBestand; this.State.Mindestbestand = evt.Mindestbestand; }
        public void Apply(PostenReserviert evt) => this.State.Reserviert += evt.Menge;
        public void Apply(PostenFreigegeben evt) => this.State.Reserviert -= evt.Menge;
        public void Apply(PostenZugebucht evt) => this.State.Bestand += evt.Menge;
    }
}
