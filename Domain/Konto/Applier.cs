using Abstractions;

namespace Domain.Konto;

public partial class Konto
{
    public partial class Applier : IApplier<Konto>
    {
        public void Apply(KontoEroeffnet evt)
        {
            this.State.Saldo = evt.StartSaldo;
            this.State.Gesperrt = evt.Gesperrt;
        }

        public void Apply(BetragReserviert evt) => this.State.Reserviert += evt.Betrag;
        public void Apply(ReservierungFreigegeben evt) => this.State.Reserviert -= evt.Betrag;
        public void Apply(Gutgeschrieben evt) => this.State.Saldo += evt.Betrag;
        public void Apply(GutschriftStorniert evt) => this.State.Saldo -= evt.Betrag;

        public void Apply(ReservierungGebucht evt)
        {
            this.State.Reserviert -= evt.Betrag;
            this.State.Saldo -= evt.Betrag;
        }

        // Reine Auslöse-Marke — kein Zustandswechsel; die Gutschrift macht die Saga über SchreibeGut.
        public void Apply(WillkommensbonusFaellig evt) { }
    }
}
