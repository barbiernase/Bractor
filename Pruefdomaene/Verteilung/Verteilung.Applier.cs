using Abstractions;

namespace Pruefdomaene.Verteilung;

public partial class Verteilung
{
    public partial class Applier : IApplier<Verteilung>
    {
        public void Apply(VerteilungGestartet evt) { this.State.Ziele = evt.Ziele; this.State.MengeJeZiel = evt.MengeJeZiel; }
        public void Apply(ZubuchungenAngeordnet evt) => this.State.Angeordnet = true;
        public void Apply(ZubuchungGezaehlt evt) { this.State.Gezaehlt++; this.State.Zugebucht += evt.Menge; }
        public void Apply(VerteilungAbgeschlossen evt) { this.State.Abgeschlossen = true; this.State.Zugebucht = evt.Zugebucht; }
        public void Apply(VerteilungGescheitert evt) => this.State.Gescheitert = true;
    }
}
