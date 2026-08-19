using Abstractions;

namespace Domain.Modell;

public partial class Modell
{
    public partial class Decider : IDecider<Modell>
    {
        public IEnumerable<OneOf<ModellRegistriert, ModellExistiertBereits>> Decide(
            RegistriereModell cmd)
        {
            if (this.State.Existiert)
            {
                yield return new ModellExistiertBereits(cmd.AggregateId);
                yield break;
            }

            yield return new ModellRegistriert(
                cmd.TrainingslaufId, cmd.DatensatzId, cmd.DatensatzVersion,
                cmd.Name, cmd.Pfad, cmd.Metriken);
        }

        public IEnumerable<OneOf<ModellAktiviert, ModellNichtGefunden, ModellBereitsArchiviert>> Decide(
            SetzeModellAktiv cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new ModellNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstArchiviert)
            {
                yield return new ModellBereitsArchiviert(cmd.AggregateId);
                yield break;
            }

            // Aktivierung ist ein Fakt — erneut aktivieren ist idempotent-verträglich (die Read-Seite
            // upsertet denselben Aktiv-Zeiger). Wir emittieren trotzdem, damit ein Inferenz-Worker
            // auch nach Neustart durch erneutes „aktiv setzen" wieder geweckt werden kann.
            yield return new ModellAktiviert(this.State.Pfad ?? "", this.State.Name ?? "");
        }

        public IEnumerable<OneOf<ModellArchiviert, ModellNichtGefunden>> Decide(
            ArchiviereModell cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new ModellNichtGefunden(cmd.AggregateId);
                yield break;
            }

            // Schon archiviert → idempotent, kein zweites Event.
            if (this.State.IstArchiviert)
                yield break;

            yield return new ModellArchiviert();
        }
    }
}
