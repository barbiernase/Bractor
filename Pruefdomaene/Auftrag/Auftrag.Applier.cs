using Abstractions;

namespace Pruefdomaene.Auftrag;

public partial class Auftrag
{
    public partial class Applier : IApplier<Auftrag>
    {
        public void Apply(AuftragGestartet evt)
        {
            this.State.PostenId = evt.PostenId;
            this.State.KontingentId = evt.KontingentId;
            this.State.Menge = evt.Menge;
        }

        public void Apply(ReservierungVermerkt evt) => this.State.Reserviert = true;
        public void Apply(BelegungVermerkt evt) => this.State.Belegt = true;
        public void Apply(AuftragBestaetigt evt) => this.State.Bestaetigt = true;
        public void Apply(AuftragGescheitert evt) => this.State.Gescheitert = true;
        public void Apply(PostenfreigabeAngefordert evt) => this.State.ReservierungZurueck = true;
        public void Apply(KontingententlastungAngefordert evt) => this.State.BelegungZurueck = true;
    }
}
