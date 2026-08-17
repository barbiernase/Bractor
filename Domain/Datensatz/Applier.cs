using Abstractions;

namespace Domain.Datensatz;

public partial class Datensatz
{
    public partial class Applier : IApplier<Datensatz>
    {
        // ═══════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════

        public void Apply(DatensatzErstellt evt)
        {
            this.State.Name = evt.Name;
            this.State.Status = DatensatzStatus.Entwurf;
        }

        // ═══════════════════════════════════════════════════
        // RANGE
        // ═══════════════════════════════════════════════════

        // Marker — der Resolver reagiert darauf, der State ändert sich (noch) nicht.
        public void Apply(RangeAngefordert evt) { }

        public void Apply(PaareAufgenommen evt)
        {
            foreach (var id in evt.ImagePairIds)
                this.State.DraftMitglieder.Add(id);   // Union, dedupliziert

            this.State.Ranges.Add(evt.Herkunft);
        }

        // ═══════════════════════════════════════════════════
        // MANUELLES DELTA
        // ═══════════════════════════════════════════════════

        public void Apply(PaarAufgenommen evt)
        {
            this.State.DraftMitglieder.Add(evt.ImagePairId);
        }

        public void Apply(PaarEntfernt evt)
        {
            this.State.DraftMitglieder.Remove(evt.ImagePairId);
        }

        // ═══════════════════════════════════════════════════
        // SPLIT
        // ═══════════════════════════════════════════════════

        public void Apply(SplitGesetzt evt)
        {
            this.State.Split = new SplitKonfig(
                evt.TrainProzent, evt.ValProzent, evt.TestProzent, evt.Seed);
        }

        // ═══════════════════════════════════════════════════
        // EINFRIEREN
        // ═══════════════════════════════════════════════════

        // Marker — der Resolver snapshottet daraufhin; der State ändert sich (noch) nicht.
        public void Apply(EinfrierenAngefordert evt) { }

        public void Apply(DatensatzEingefroren evt)
        {
            this.State.Status = DatensatzStatus.Eingefroren;
            this.State.EingefroreneVersion = evt.Version;
            this.State.EingefroreneMitglieder = evt.Mitglieder;
        }
    }
}
