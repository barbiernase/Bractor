using Abstractions;

namespace Pruefdomaene.Abrechnung;

public partial class Rechnung
{
    public partial class Decider : IDecider<Rechnung>
    {
        public IEnumerable<OneOf<RechnungGestellt, RechnungExistiert>> Decide(StelleRechnung cmd)
        {
            if (this.State.Existiert) { yield return new RechnungExistiert(cmd.AggregateId); yield break; }
            yield return new RechnungGestellt(cmd.AggregateId, cmd.Konto, cmd.Betrag);
        }

        public IEnumerable<OneOf<RechnungFreigegeben, RechnungUnbekannt>> Decide(GibRechnungFrei cmd)
        {
            if (!this.State.Existiert) { yield return new RechnungUnbekannt(cmd.AggregateId); yield break; }
            if (this.State.Freigegeben || this.State.Gesperrt) yield break;
            yield return new RechnungFreigegeben();
        }

        public IEnumerable<OneOf<RechnungGesperrt, RechnungUnbekannt>> Decide(SperreRechnung cmd)
        {
            if (!this.State.Existiert) { yield return new RechnungUnbekannt(cmd.AggregateId); yield break; }
            if (this.State.Freigegeben || this.State.Gesperrt) yield break;
            yield return new RechnungGesperrt(cmd.Grund);
        }

        public IEnumerable<OneOf<RechnungBezahlt, RechnungUnbekannt>> Decide(BezahleRechnung cmd)
        {
            if (!this.State.Existiert) { yield return new RechnungUnbekannt(cmd.AggregateId); yield break; }
            if (this.State.Bezahlt) yield break;
            yield return new RechnungBezahlt(cmd.AggregateId, this.State.Konto, this.State.Betrag);
        }

        /// <summary>Die fällige Prüfung der Zahlung: noch offen → überfällig, eine Mahnstufe höher; bezahlt → nichts.</summary>
        public IEnumerable<OneOf<ZahlungUeberfaellig, RechnungUnbekannt>> Decide(PruefeZahlung cmd)
        {
            if (!this.State.Existiert) { yield return new RechnungUnbekannt(cmd.AggregateId); yield break; }
            if (this.State.Bezahlt) yield break;
            yield return new ZahlungUeberfaellig(cmd.AggregateId, this.State.Konto, this.State.Betrag, this.State.Mahnstufe + 1);
        }
    }
}
