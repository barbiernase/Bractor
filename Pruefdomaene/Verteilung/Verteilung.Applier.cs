using Abstractions;

namespace Pruefdomaene.Verteilung;

public partial class Verteilung
{
    public partial class Applier : IApplier<Verteilung>
    {
        public void Apply(VerteilungGestartet evt) { this.State.Ziele = evt.Ziele; this.State.MengeJeZiel = evt.MengeJeZiel; }
        public void Apply(QuelleReserviert evt) => this.State.QuelleReserviert = true;
        public void Apply(ZielZugebucht evt) { this.State.ZieleGebucht++; this.State.Zugebucht += evt.Menge; }
        public void Apply(VerteilungAbgeschlossen evt) => this.State.Abgeschlossen = true;
        public void Apply(VerteilungGescheitert evt) => this.State.Gescheitert = true;
    }
}
