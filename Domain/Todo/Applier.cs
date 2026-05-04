using Abstractions;

namespace Domain.Todo;

public partial class TodoItem
{
    public partial class Applier : IApplier<TodoItem>
    {
        public void Apply(TodoErstellt evt)
        {
            this.State.Titel        = evt.Titel;
            this.State.Beschreibung = evt.Beschreibung;
            this.State.Prioritaet   = evt.Prioritaet;
            this.State.Faelligkeit  = evt.Faelligkeit;
            this.State.Status       = TodoStatus.Offen;
            this.State.ErstelltAm   = DateTimeOffset.UtcNow;
        }

        public void Apply(TitelGeaendert evt)
        {
            this.State.Titel = evt.NeuerTitel;
        }

        public void Apply(PrioritaetGesetzt evt)
        {
            this.State.Prioritaet = evt.NeuePrioritaet;
        }

        public void Apply(AlsErledigtMarkiert evt)
        {
            this.State.Status     = TodoStatus.Erledigt;
            this.State.ErledigtAm = DateTimeOffset.UtcNow;
        }

        public void Apply(WiederGeoeffnet evt)
        {
            this.State.Status     = TodoStatus.Offen;
            this.State.ErledigtAm = null;
        }

        public void Apply(Archiviert evt)
        {
            this.State.Status = TodoStatus.Archiviert;
        }

        public void Apply(TagHinzugefuegt evt)
        {
            this.State.Tags.Add(evt.Tag);
        }

        public void Apply(TagEntfernt evt)
        {
            this.State.Tags.Remove(evt.Tag);
        }
    }
}