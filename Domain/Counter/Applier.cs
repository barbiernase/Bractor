using Abstractions;

namespace Domain.Counter;

public partial class Counter
{
    public partial class Applier : IApplier<Counter>
    {
        // ──────────────────────────────────────────
        // LIFECYCLE
        // ──────────────────────────────────────────

        public void Apply(CounterErstellt evt)
        {
            this.State.Name = evt.Name;
            this.State.MaxGrenze = evt.MaxGrenze;
            this.State.MinGrenze = evt.MinGrenze;
            this.State.Wert = evt.StartWert;
            this.State.ErstelltAm = evt.ErstelltAm;
        }

        // ──────────────────────────────────────────
        // WERT-ÄNDERUNGEN
        // ──────────────────────────────────────────

        public void Apply(CounterIncrementiert evt)
        {
            this.State.Wert = evt.NeuerWert;
            this.State.LetzteAenderungAm = evt.Zeitpunkt;
        }

        public void Apply(CounterDekrementiert evt)
        {
            this.State.Wert = evt.NeuerWert;
            this.State.LetzteAenderungAm = evt.Zeitpunkt;
        }

        public void Apply(CounterZurueckgesetzt evt)
        {
            this.State.Wert = 0;
            this.State.LetzteAenderungAm = DateTimeOffset.UtcNow;
        }

        // ──────────────────────────────────────────
        // AUDIT-EVENTS — leerer Apply
        // ──────────────────────────────────────────
        //
        // Das Event ist persistiert (es ist IEvent),
        // ändert aber keinen State. Andere Komponenten
        // (Pipeline, UI-Statistik) reagieren darauf,
        // hier passiert bewusst nichts.

        public void Apply(MaximumErreicht evt) { }
        public void Apply(MinimumErreicht evt) { }

        // ──────────────────────────────────────────
        // GRENZEN
        // ──────────────────────────────────────────

        public void Apply(MaxGrenzeGeaendert evt)
        {
            this.State.MaxGrenze = evt.MaxGrenze;
        }

        public void Apply(MinGrenzeGeaendert evt)
        {
            this.State.MinGrenze = evt.MinGrenze;
        }

        // ──────────────────────────────────────────
        // LÖSCHEN
        // ──────────────────────────────────────────

        public void Apply(CounterGeloescht evt)
        {
            this.State.IstGeloescht = true;
        }
    }
}
