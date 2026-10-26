using Abstractions;

namespace Pruefdomaene.Auftrag;

public partial class Auftrag
{
    public partial class Decider : IDecider<Auftrag>
    {
        public IEnumerable<OneOf<AuftragGestartet, AuftragExistiertBereits>> Decide(StarteAuftrag cmd)
        {
            if (this.State.Existiert) { yield return new AuftragExistiertBereits(cmd.AggregateId); yield break; }
            yield return new AuftragGestartet(cmd.AggregateId, cmd.PostenId, cmd.KontingentId, cmd.Menge);
        }

        /// <summary>Die Reservierung ist gelungen: zusammen mit der Belegung bestätigt sie den Auftrag — ist er schon gescheitert, geht sie zurück.</summary>
        public IEnumerable<OneOf<ReservierungVermerkt, AuftragBestaetigt, PostenFreigabeAngeordnet, AuftragNichtGefunden>> Decide(VermerkeReservierung cmd)
        {
            if (!this.State.Existiert) { yield return new AuftragNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Reserviert) yield break;
            yield return new ReservierungVermerkt();
            if (this.State.Gescheitert) yield return new PostenFreigabeAngeordnet(this.State.PostenId, this.State.Menge);
            else if (this.State.Belegt) yield return new AuftragBestaetigt();
        }

        /// <summary>Die Belegung ist gelungen — spiegelbildlich zur Reservierung.</summary>
        public IEnumerable<OneOf<BelegungVermerkt, AuftragBestaetigt, KontingentEntlastungAngeordnet, AuftragNichtGefunden>> Decide(VermerkeBelegung cmd)
        {
            if (!this.State.Existiert) { yield return new AuftragNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Belegt) yield break;
            yield return new BelegungVermerkt();
            if (this.State.Gescheitert) yield return new KontingentEntlastungAngeordnet(this.State.KontingentId, this.State.Menge);
            else if (this.State.Reserviert) yield return new AuftragBestaetigt();
        }

        /// <summary>
        /// Ein Zweig ist abgelehnt: beim ersten Mal scheitert der Auftrag, und was bis dahin gelang, wird zurückgenommen (je Zweig genau einmal; eine
        /// spätere Rückmeldung des anderen Zweigs nimmt sich selbst zurück). Ein bestätigter Auftrag scheitert nicht mehr.
        /// </summary>
        public IEnumerable<OneOf<AuftragGescheitert, PostenFreigabeAngeordnet, KontingentEntlastungAngeordnet, AuftragNichtGefunden>> Decide(VermerkeAbsage cmd)
        {
            if (!this.State.Existiert) { yield return new AuftragNichtGefunden(cmd.AggregateId); yield break; }
            if (this.State.Gescheitert || this.State.Bestaetigt) yield break;
            yield return new AuftragGescheitert(cmd.Grund);
            if (this.State.Reserviert && !this.State.PostenZurueck) yield return new PostenFreigabeAngeordnet(this.State.PostenId, this.State.Menge);
            if (this.State.Belegt && !this.State.KontingentZurueck) yield return new KontingentEntlastungAngeordnet(this.State.KontingentId, this.State.Menge);
        }
    }
}
