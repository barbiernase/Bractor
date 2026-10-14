using Abstractions;

namespace Domain.Trainingslauf;

public partial class Trainingslauf
{
    public partial class Applier : IApplier<Trainingslauf>
    {
        public void Apply(TrainingAngefordert evt)
        {
            this.State.DatensatzId = evt.DatensatzId;
            this.State.DatensatzVersion = evt.DatensatzVersion;
            this.State.Hyperparameter = evt.Hyperparameter;
            this.State.Status = TrainingsStatus.Angefordert;
        }

        public void Apply(TrainingBegonnen evt)
        {
            this.State.Status = TrainingsStatus.Laeuft;
        }

        public void Apply(TrainingFortschritt evt)
        {
            this.State.AktuelleEpoche = evt.Metrik.Epoche;
            this.State.MetrikHistorie.Add(evt.Metrik);
        }

        public void Apply(TrainingAbgeschlossen evt)
        {
            this.State.Status = TrainingsStatus.Abgeschlossen;
            this.State.ModellPfad = evt.ModellPfad;
            this.State.Endmetriken = evt.Endmetriken;
        }

        public void Apply(TrainingGescheitert evt)
        {
            this.State.Status = TrainingsStatus.Gescheitert;
            this.State.Fehlergrund = evt.Grund;
        }

        public void Apply(TrainingAbgebrochen evt)
        {
            this.State.Status = TrainingsStatus.Abgebrochen;
        }

        public void Apply(TrainingHaengengeblieben evt)
        {
            this.State.Status = TrainingsStatus.Haengengeblieben;
        }
    }
}
