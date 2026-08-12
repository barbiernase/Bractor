using Abstractions;

namespace Domain.Verkauf;

public partial class Verkaufsauftrag
{
    /// <summary>
    /// APPLIER — die einzige Stelle, die Zustand mutiert. Pflegt den laufenden
    /// <see cref="Gesamtsumme"/>-Saldo inkrementell (O(1)), damit das Kreditlimit-Prüfen im
    /// Decider nicht jede Position neu aufsummiert (O(N²) über den ganzen Auftrag).
    /// </summary>
    public partial class Applier : IApplier<Verkaufsauftrag>
    {
        public void Apply(VerkaufsauftragEroeffnet evt)
        {
            this.State.KundeId = evt.KundeId;
            this.State.Kreditlimit = evt.Kreditlimit;
            this.State.Status = Auftragsstatus.Offen;
            this.State.Gesamtsumme = Geldwert.Null(evt.Kreditlimit.Waehrung);
        }

        public void Apply(VerkaufspositionHinzugefuegt evt)
        {
            var index = this.State.Positionen.FindIndex(p => p.ArtikelNr == evt.ArtikelNr);
            if (index >= 0)
            {
                // Verschmelzen: bestehende Position, Menge erhöhen (bereits gebuchter Einzelpreis zählt).
                var alt = this.State.Positionen[index];
                this.State.Positionen[index] = alt with { Menge = alt.Menge + evt.Menge };
                this.State.Gesamtsumme = this.State.Gesamtsumme.Plus(alt.Einzelpreis.Mal(evt.Menge));
            }
            else
            {
                this.State.Positionen.Add(new Auftragsposition(evt.ArtikelNr, evt.Bezeichnung, evt.Menge, evt.Einzelpreis));
                this.State.Gesamtsumme = this.State.Gesamtsumme.Plus(evt.Einzelpreis.Mal(evt.Menge));
            }
        }

        public void Apply(VerkaufsauftragAufgegeben evt) => this.State.Status = Auftragsstatus.Aufgegeben;

        public void Apply(VerkaufsauftragStorniert evt) => this.State.Status = Auftragsstatus.Storniert;
    }
}
