using Abstractions;

namespace Domain.Modell;

public partial class Modell
{
    public partial class Applier : IApplier<Modell>
    {
        public void Apply(ModellRegistriert evt)
        {
            this.State.TrainingslaufId = evt.TrainingslaufId;
            this.State.DatensatzId = evt.DatensatzId;
            this.State.DatensatzVersion = evt.DatensatzVersion;
            this.State.Name = evt.Name;
            this.State.Pfad = evt.Pfad;
            this.State.Metriken = evt.Metriken;
            this.State.Status = ModellStatus.Registriert;
        }

        // Aktivierung ändert keinen Aggregat-Status (die Read-Seite hält den Aktiv-Zeiger).
        public void Apply(ModellAktiviert evt) { }

        public void Apply(ModellArchiviert evt)
        {
            this.State.Status = ModellStatus.Archiviert;
        }
    }
}
