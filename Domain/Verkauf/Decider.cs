using Abstractions;

namespace Domain.Verkauf;

public partial class Verkaufsauftrag
{
    /// <summary>
    /// DECIDER — prüft die Invarianten und entscheidet, WELCHE Ereignisse folgen. Rein
    /// (keine Seiteneffekte), gibt bei Regelbruch ein Ablehnungs-Ereignis zurück statt zu
    /// werfen — genau das Framework-Idiom (siehe Konto).
    /// </summary>
    public partial class Decider : IDecider<Verkaufsauftrag>
    {
        public IEnumerable<OneOf<VerkaufsauftragEroeffnet, VerkaufsauftragExistiertBereits>> Decide(EroeffneVerkaufsauftrag cmd)
        {
            if (this.State.Version > 0) { yield return new VerkaufsauftragExistiertBereits(cmd.AggregateId); yield break; }
            yield return new VerkaufsauftragEroeffnet(cmd.KundeId, cmd.Kreditlimit);
        }

        public IEnumerable<OneOf<VerkaufspositionHinzugefuegt, KreditlimitUeberschritten, AuftragNichtOffen, WaehrungPasstNicht, VerkaufsauftragNichtGefunden>> Decide(FuegePositionHinzu cmd)
        {
            if (this.State.Version == 0) { yield return new VerkaufsauftragNichtGefunden(cmd.AggregateId); yield break; }
            if (!this.State.IstOffen) { yield return new AuftragNichtOffen(cmd.AggregateId); yield break; }
            if (!cmd.Einzelpreis.GleicheWaehrung(this.State.Kreditlimit))
            {
                yield return new WaehrungPasstNicht(this.State.Kreditlimit.Waehrung, cmd.Einzelpreis.Waehrung);
                yield break;
            }

            // Bei gleicher Artikelnummer zählt der bereits gebuchte Einzelpreis (der Applier verschmilzt).
            var vorhanden = this.State.Position(cmd.ArtikelNr);
            var preisFuerZuwachs = vorhanden?.Einzelpreis ?? cmd.Einzelpreis;
            var prospektiv = this.State.Gesamtsumme.Plus(preisFuerZuwachs.Mal(cmd.Menge));
            if (prospektiv.GroesserAls(this.State.Kreditlimit)) { yield return new KreditlimitUeberschritten(prospektiv, this.State.Kreditlimit); yield break; }

            yield return new VerkaufspositionHinzugefuegt(cmd.ArtikelNr, cmd.Bezeichnung, cmd.Menge, cmd.Einzelpreis);
        }

        public IEnumerable<OneOf<VerkaufsauftragAufgegeben, AuftragNichtOffen, VerkaufsauftragNichtGefunden>> Decide(GibVerkaufsauftragAuf cmd)
        {
            if (this.State.Version == 0) { yield return new VerkaufsauftragNichtGefunden(cmd.AggregateId); yield break; }
            if (!this.State.IstOffen) { yield return new AuftragNichtOffen(cmd.AggregateId); yield break; }
            if (this.State.Positionen.Count == 0) { yield return new AuftragNichtOffen(cmd.AggregateId); yield break; }
            yield return new VerkaufsauftragAufgegeben(this.State.Gesamtsumme);
        }

        public IEnumerable<OneOf<VerkaufsauftragStorniert, VerkaufsauftragNichtGefunden>> Decide(StorniereVerkaufsauftrag cmd)
        {
            if (this.State.Version == 0) { yield return new VerkaufsauftragNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Status == Auftragsstatus.Storniert) yield break; // idempotent
            yield return new VerkaufsauftragStorniert(cmd.Grund);
        }
    }
}
