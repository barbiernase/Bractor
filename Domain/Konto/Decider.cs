using Abstractions;

namespace Domain.Konto;

public partial class Konto
{
    public partial class Decider : IDecider<Konto>
    {
        public IEnumerable<OneOf<KontoEroeffnet, KontoExistiertBereits>> Decide(EroeffneKonto cmd)
        {
            if (this.State.Version > 0) { yield return new KontoExistiertBereits(cmd.AggregateId); yield break; }
            yield return new KontoEroeffnet(cmd.StartSaldo, cmd.Gesperrt);
        }

        public IEnumerable<OneOf<BetragReserviert, KontoGesperrt, DeckungReichtNicht, KontoNichtGefunden>> Decide(ReserviereBetrag cmd)
        {
            if (this.State.Version == 0) { yield return new KontoNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Gesperrt) { yield return new KontoGesperrt(cmd.AggregateId); yield break; }
            if (this.State.Verfuegbar < cmd.Betrag) { yield return new DeckungReichtNicht(this.State.Verfuegbar, cmd.Betrag); yield break; }
            yield return new BetragReserviert(cmd.Betrag);
        }

        public IEnumerable<OneOf<ReservierungFreigegeben>> Decide(GebeReservierungFrei cmd)
        {
            yield return new ReservierungFreigegeben(cmd.Betrag);
        }

        public IEnumerable<OneOf<Gutgeschrieben, KontoGesperrt, KontoNichtGefunden>> Decide(SchreibeGut cmd)
        {
            if (this.State.Version == 0) { yield return new KontoNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Gesperrt) { yield return new KontoGesperrt(cmd.AggregateId); yield break; }
            yield return new Gutgeschrieben(cmd.Betrag);
        }

        public IEnumerable<OneOf<GutschriftStorniert>> Decide(StorniereGutschrift cmd)
        {
            yield return new GutschriftStorniert(cmd.Betrag);
        }

        public IEnumerable<OneOf<ReservierungGebucht>> Decide(BucheReservierung cmd)
        {
            yield return new ReservierungGebucht(cmd.Betrag);
        }

        public IEnumerable<OneOf<WillkommensbonusFaellig, KontoNichtGefunden>> Decide(GewaehreWillkommensbonus cmd)
        {
            if (this.State.Version == 0) { yield return new KontoNichtGefunden(cmd.AggregateId); yield break; }
            yield return new WillkommensbonusFaellig(cmd.AggregateId, cmd.Betrag);
        }
    }
}
