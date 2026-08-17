using Abstractions;

namespace Domain.Trainingslauf;

public partial class Trainingslauf
{
    public partial class Decider : IDecider<Trainingslauf>
    {
        // ═══════════════════════════════════════════════════
        // START
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<TrainingAngefordert, TrainingslaufExistiertBereits>> Decide(
            StarteTraining cmd)
        {
            if (this.State.Existiert)
            {
                yield return new TrainingslaufExistiertBereits(cmd.AggregateId);
                yield break;
            }

            yield return new TrainingAngefordert(
                cmd.DatensatzId, cmd.DatensatzVersion, cmd.Hyperparameter);
        }

        // ═══════════════════════════════════════════════════
        // PYTHON — Fortschritt
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<TrainingBegonnen, TrainingslaufNichtGefunden, TrainingBereitsBeendet>> Decide(
            MeldeTrainingBegonnen cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TrainingslaufNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstBeendet)
            {
                yield return new TrainingBereitsBeendet(cmd.AggregateId);
                yield break;
            }

            // Idempotent — läuft bereits, kein zweites Event (doppelt zugestellt).
            if (this.State.Status == TrainingsStatus.Laeuft)
                yield break;

            yield return new TrainingBegonnen();
        }

        public IEnumerable<OneOf<TrainingFortschritt, TrainingslaufNichtGefunden, TrainingNichtAktiv, TrainingBereitsBeendet>> Decide(
            MeldeFortschritt cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TrainingslaufNichtGefunden(cmd.AggregateId);
                yield break;
            }

            // Beendetes Training: konsistent mit den anderen Meldungen.
            if (this.State.IstBeendet)
            {
                yield return new TrainingBereitsBeendet(cmd.AggregateId);
                yield break;
            }

            // Fortschritt nur während der Lauf läuft (sonst: noch Angefordert).
            if (this.State.Status != TrainingsStatus.Laeuft)
            {
                yield return new TrainingNichtAktiv(cmd.AggregateId);
                yield break;
            }

            yield return new TrainingFortschritt(cmd.Metrik);
        }

        public IEnumerable<OneOf<TrainingAbgeschlossen, TrainingslaufNichtGefunden, TrainingBereitsBeendet>> Decide(
            MeldeTrainingAbgeschlossen cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TrainingslaufNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstBeendet)
            {
                yield return new TrainingBereitsBeendet(cmd.AggregateId);
                yield break;
            }

            yield return new TrainingAbgeschlossen(cmd.ModellPfad, cmd.Endmetriken);
        }

        public IEnumerable<OneOf<TrainingGescheitert, TrainingslaufNichtGefunden, TrainingBereitsBeendet>> Decide(
            MeldeTrainingGescheitert cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TrainingslaufNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstBeendet)
            {
                yield return new TrainingBereitsBeendet(cmd.AggregateId);
                yield break;
            }

            yield return new TrainingGescheitert(cmd.Grund);
        }

        // ═══════════════════════════════════════════════════
        // GUI — Abbruch
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<TrainingAbgebrochen, TrainingslaufNichtGefunden, TrainingBereitsBeendet>> Decide(
            BricheTrainingAb cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TrainingslaufNichtGefunden(cmd.AggregateId);
                yield break;
            }

            // Idempotent — schon abgebrochen, kein zweites Event.
            if (this.State.Status == TrainingsStatus.Abgebrochen)
                yield break;

            if (this.State.IstBeendet)
            {
                yield return new TrainingBereitsBeendet(cmd.AggregateId);
                yield break;
            }

            yield return new TrainingAbgebrochen();
        }

        // ═══════════════════════════════════════════════════
        // FRIST — Timeout
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<TrainingHaengengeblieben, TrainingslaufNichtGefunden>> Decide(
            MarkiereAlsHaengengeblieben cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new TrainingslaufNichtGefunden(cmd.AggregateId);
                yield break;
            }

            // Eine späte Frist darf ein bereits beendetes Training NICHT überschreiben (idempotent).
            if (this.State.IstBeendet)
                yield break;

            yield return new TrainingHaengengeblieben();
        }
    }
}
