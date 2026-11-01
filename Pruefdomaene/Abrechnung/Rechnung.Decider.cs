using Abstractions;

namespace Pruefdomaene.Abrechnung;

public partial class Rechnung
{
    public partial class Decider : IDecider<Rechnung>
    {
        /// <summary>Mit Zahlungsziel setzt die Rechnung eine Frist: die Uhr prüft nach Ablauf, ob gezahlt ist. Die Freigabe hört die Folge.</summary>
        public IEnumerable<OneOf<RechnungGestellt, Frist<PruefeZahlung>, RechnungExistiert, Freizugeben>> Decide(StelleRechnung cmd)
        {
            if (this.State.Existiert) { yield return new RechnungExistiert(cmd.AggregateId); yield break; }
            yield return new RechnungGestellt(cmd.AggregateId, cmd.Konto, cmd.Betrag);
            yield return new Freizugeben(cmd.AggregateId, cmd.Betrag);
            if (cmd.ZahlungszielSekunden > 0)
                yield return new Frist<PruefeZahlung>(new PruefeZahlung(cmd.AggregateId), TimeSpan.FromSeconds(cmd.ZahlungszielSekunden));
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

        public IEnumerable<OneOf<RechnungBezahlt, Gutschreibung, RechnungUnbekannt>> Decide(BezahleRechnung cmd)
        {
            if (!this.State.Existiert) { yield return new RechnungUnbekannt(cmd.AggregateId); yield break; }
            if (this.State.Bezahlt) yield break;
            yield return new RechnungBezahlt(cmd.AggregateId, this.State.Konto, this.State.Betrag);
            yield return new Gutschreibung(this.State.Konto, cmd.AggregateId, this.State.Betrag);
        }

        /// <summary>Die fällige Prüfung der Zahlung: noch offen → überfällig, eine Mahnstufe höher; bezahlt → nichts.</summary>
        public IEnumerable<OneOf<ZahlungUeberfaellig, Mahnfall, RechnungUnbekannt>> Decide(PruefeZahlung cmd)
        {
            if (!this.State.Existiert) { yield return new RechnungUnbekannt(cmd.AggregateId); yield break; }
            if (this.State.Bezahlt) yield break;
            yield return new ZahlungUeberfaellig(cmd.AggregateId, this.State.Konto, this.State.Betrag, this.State.Mahnstufe + 1);
            yield return new Mahnfall(cmd.AggregateId, this.State.Konto, this.State.Betrag, this.State.Mahnstufe + 1);
        }
    }
}
