using Abstractions;

namespace Domain.Todo;

public partial class TodoItem
{
    public partial class Decider : IDecider<TodoItem>
    {
        public IEnumerable<OneOf<TodoErstellt, TodoExistiertBereits, TodoEingabeUngueltig>> Decide(
            ErstelleTodo cmd)
        {
            if (this.State.Version > 0)
            {
                yield return new TodoExistiertBereits(cmd.AggregateId);
                yield break;
            }
            if (string.IsNullOrWhiteSpace(cmd.Titel))
            {
                yield return new TodoEingabeUngueltig("Titel", "darf nicht leer sein");
                yield break;
            }

            yield return new TodoErstellt(cmd.Titel.Trim(), cmd.Beschreibung?.Trim(),
                cmd.Prioritaet, cmd.Faelligkeit);
        }

        public IEnumerable<OneOf<TitelGeaendert, TodoNichtGefunden, TodoBereitsArchiviert, TodoEingabeUngueltig>> Decide(
            AendereTitel cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert)
            {
                yield return new TodoBereitsArchiviert(cmd.AggregateId);
                yield break;
            }
            if (string.IsNullOrWhiteSpace(cmd.NeuerTitel))
            {
                yield return new TodoEingabeUngueltig("Titel", "darf nicht leer sein");
                yield break;
            }

            var trimmed = cmd.NeuerTitel.Trim();
            if (this.State.Titel == trimmed) yield break; // Noop

            yield return new TitelGeaendert(trimmed);
        }

        public IEnumerable<OneOf<PrioritaetGesetzt, TodoNichtGefunden, TodoBereitsArchiviert>> Decide(
            SetzePrioritaet cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert)
            {
                yield return new TodoBereitsArchiviert(cmd.AggregateId);
                yield break;
            }
            if (this.State.Prioritaet == cmd.NeuePrioritaet) yield break;

            yield return new PrioritaetGesetzt(cmd.NeuePrioritaet);
        }

        public IEnumerable<OneOf<AlsErledigtMarkiert, TodoNichtGefunden, TodoBereitsArchiviert, TodoBereitsErledigt>> Decide(
            MarkiereAlsErledigt cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert)
            {
                yield return new TodoBereitsArchiviert(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstErledigt)
            {
                yield return new TodoBereitsErledigt(cmd.AggregateId);
                yield break;
            }

            yield return new AlsErledigtMarkiert();
        }

        public IEnumerable<OneOf<WiederGeoeffnet, TodoNichtGefunden, TodoBereitsArchiviert, TodoNichtErledigt>> Decide(
            OeffneWieder cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert)
            {
                yield return new TodoBereitsArchiviert(cmd.AggregateId);
                yield break;
            }
            if (!this.State.IstErledigt)
            {
                yield return new TodoNichtErledigt(cmd.AggregateId);
                yield break;
            }

            yield return new WiederGeoeffnet();
        }

        public IEnumerable<OneOf<Archiviert, TodoNichtGefunden>> Decide(
            Archiviere cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert) yield break; // Idempotent

            yield return new Archiviert();
        }

        public IEnumerable<OneOf<TagHinzugefuegt, TodoNichtGefunden, TodoBereitsArchiviert, TodoEingabeUngueltig>> Decide(
            FuegeTagHinzu cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert)
            {
                yield return new TodoBereitsArchiviert(cmd.AggregateId);
                yield break;
            }
            if (string.IsNullOrWhiteSpace(cmd.Tag))
            {
                yield return new TodoEingabeUngueltig("Tag", "darf nicht leer sein");
                yield break;
            }

            var trimmed = cmd.Tag.Trim();
            if (this.State.Tags.Contains(trimmed)) yield break; // Idempotent

            yield return new TagHinzugefuegt(trimmed);
        }

        public IEnumerable<OneOf<TagEntfernt, TodoNichtGefunden, TodoBereitsArchiviert>> Decide(
            EntferneTag cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new TodoNichtGefunden(cmd.AggregateId);
                yield break;
            }
            if (this.State.IstArchiviert)
            {
                yield return new TodoBereitsArchiviert(cmd.AggregateId);
                yield break;
            }
            if (!this.State.Tags.Contains(cmd.Tag)) yield break;

            yield return new TagEntfernt(cmd.Tag);
        }
    }
}