using Abstractions;

namespace Pruefdomaene.Abrechnung;

public partial class Rechnung
{
    public partial class Applier : IApplier<Rechnung>
    {
        public void Apply(RechnungGestellt evt) { this.State.Konto = evt.Konto; this.State.Betrag = evt.Betrag; }
        public void Apply(RechnungFreigegeben evt) => this.State.Freigegeben = true;
        public void Apply(RechnungGesperrt evt) => this.State.Gesperrt = true;
        public void Apply(RechnungBezahlt evt) => this.State.Bezahlt = true;
        public void Apply(ZahlungUeberfaellig evt) => this.State.Mahnstufe = evt.Mahnstufe;
    }
}
