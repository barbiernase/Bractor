using Abstractions;

namespace Domain.Lagerartikel
{
    public partial class Lagerartikel
    {
        public partial class Applier : IApplier<Lagerartikel>
        {
            public void Apply(LagerartikelErstellt evt)
            {
                this.State.Name = evt.Name;
                this.State.AnzLagernd = evt.InitialeAnzahl;
                this.State.IstAktiv = true;
            }

            public void Apply(WareneingangGebucht evt)
            {
                this.State.AnzLagernd += evt.Anzahl;
            }
            
            public void Apply(WarenabgangGebucht evt)
            {
                this.State.AnzLagernd -= evt.Anzahl;
            }

            public void Apply(LagerartikelDeaktiviert evt)
            {
                this.State.IstAktiv = false;
            }
            
            
            public void Apply(LagerplatzGeaendert evt)
            {
                this.State.Platz = evt.NeuerPlatz;
            }

            public void Apply(EinkaufspreisAngepasst evt)
            {
                this.State.Einkaufspreis = evt.NeuerPreis;
            }

            public void Apply(LieferantZugeordnet evt)
            {
                this.State.LieferantId = evt.LieferantId;
            }

            public void Apply(BestandReserviert evt)
            {
                this.State.Reservierungen[evt.AuftragsId] = evt.Anzahl;
            }
        }
    }
}