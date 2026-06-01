using Abstractions;

namespace Domain.Counter;

public partial class Counter
{
    public partial class Decider : IDecider<Counter>
    {
        // ──────────────────────────────────────────
        // LIFECYCLE
        // ──────────────────────────────────────────

        public IEnumerable<OneOf<CounterErstellt, CounterEingabeUngueltig>> Decide(
            ErstelleCounter cmd)
        {
            // Eingabe-Validierung als fachliche Ablehnung,
            // nicht als Exception. Jeder Pfad ist Daten.

            if (string.IsNullOrWhiteSpace(cmd.Name))
            {
                yield return new CounterEingabeUngueltig("Name", "darf nicht leer sein");
                yield break;
            }

            if (cmd.MaxGrenze.HasValue && cmd.MinGrenze.HasValue
                && cmd.MaxGrenze.Value < cmd.MinGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "Grenzen", "MaxGrenze < MinGrenze");
                yield break;
            }

            if (cmd.MaxGrenze.HasValue && cmd.StartWert > cmd.MaxGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "StartWert", "über MaxGrenze");
                yield break;
            }

            if (cmd.MinGrenze.HasValue && cmd.StartWert < cmd.MinGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "StartWert", "unter MinGrenze");
                yield break;
            }

            yield return new CounterErstellt(
                cmd.Name, cmd.MaxGrenze, cmd.MinGrenze,
                cmd.StartWert, DateTimeOffset.UtcNow);
        }

        // ──────────────────────────────────────────
        // INCREMENTIERE / DEKREMENTIERE
        // ──────────────────────────────────────────

        public IEnumerable<OneOf<
            CounterIncrementiert,
            MaximumErreicht,
            OberhalbMaximum,
            CounterNichtGefunden,
            CounterBereitsGeloescht>>
        Decide(Incrementiere cmd)
        {
            // Existenz-Check: Version 0 heißt "Aggregat existiert nicht"
            if (this.State.Version == 0)
            {
                yield return new CounterNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstGeloescht)
            {
                yield return new CounterBereitsGeloescht(cmd.AggregateId);
                yield break;
            }

            var neuerWert = this.State.Wert + cmd.Delta;

            if (this.State.MaxGrenze.HasValue && neuerWert > this.State.MaxGrenze.Value)
            {
                yield return new OberhalbMaximum(neuerWert, this.State.MaxGrenze.Value);
                yield break;
            }

            // Haupt-Event: der Wert ändert sich
            yield return new CounterIncrementiert(
                cmd.Delta, neuerWert, DateTimeOffset.UtcNow);

            // Folge-Event: bei Gleichheit Maximum-Audit yielden.
            // Beide Events kommen aus EINEM Decide-Aufruf,
            // werden zusammen persistiert.
            if (this.State.MaxGrenze.HasValue && neuerWert == this.State.MaxGrenze.Value)
                yield return new MaximumErreicht(neuerWert, this.State.MaxGrenze.Value);
        }

        public IEnumerable<OneOf<
            CounterDekrementiert,
            MinimumErreicht,
            UnterhalbMinimum,
            CounterNichtGefunden,
            CounterBereitsGeloescht>>
        Decide(Dekrementiere cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new CounterNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstGeloescht)
            {
                yield return new CounterBereitsGeloescht(cmd.AggregateId);
                yield break;
            }

            var neuerWert = this.State.Wert - cmd.Delta;

            if (this.State.MinGrenze.HasValue && neuerWert < this.State.MinGrenze.Value)
            {
                yield return new UnterhalbMinimum(neuerWert, this.State.MinGrenze.Value);
                yield break;
            }

            yield return new CounterDekrementiert(
                cmd.Delta, neuerWert, DateTimeOffset.UtcNow);

            if (this.State.MinGrenze.HasValue && neuerWert == this.State.MinGrenze.Value)
                yield return new MinimumErreicht(neuerWert, this.State.MinGrenze.Value);
        }

        // ──────────────────────────────────────────
        // RESET (mit Idempotenz)
        // ──────────────────────────────────────────

        public IEnumerable<OneOf<
            CounterZurueckgesetzt,
            CounterNichtGefunden,
            CounterBereitsGeloescht>>
        Decide(Reset cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new CounterNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstGeloescht)
            {
                yield return new CounterBereitsGeloescht(cmd.AggregateId);
                yield break;
            }

            // Idempotenz: ist der Wert schon 0, gibt es nichts zu tun.
            // Keine Events, keine Ablehnung — sauberes No-Op.
            if (this.State.Wert == 0)
                yield break;

            yield return new CounterZurueckgesetzt(this.State.Wert);
        }

        // ──────────────────────────────────────────
        // GRENZEN ÄNDERN
        // ──────────────────────────────────────────

        public IEnumerable<OneOf<
            MaxGrenzeGeaendert,
            CounterNichtGefunden,
            CounterBereitsGeloescht,
            CounterEingabeUngueltig>>
        Decide(SetzeMaxGrenze cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new CounterNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstGeloescht)
            {
                yield return new CounterBereitsGeloescht(cmd.AggregateId);
                yield break;
            }

            // Konsistenz: neue MaxGrenze nicht unter aktueller MinGrenze
            if (cmd.MaxGrenze.HasValue && this.State.MinGrenze.HasValue
                && cmd.MaxGrenze.Value < this.State.MinGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "MaxGrenze", "kleiner als aktuelle MinGrenze");
                yield break;
            }

            // Konsistenz: neue MaxGrenze nicht unter aktuellem Wert
            if (cmd.MaxGrenze.HasValue && this.State.Wert > cmd.MaxGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "MaxGrenze", "kleiner als aktueller Wert");
                yield break;
            }

            yield return new MaxGrenzeGeaendert(cmd.MaxGrenze);
        }

        public IEnumerable<OneOf<
            MinGrenzeGeaendert,
            CounterNichtGefunden,
            CounterBereitsGeloescht,
            CounterEingabeUngueltig>>
        Decide(SetzeMinGrenze cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new CounterNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.IstGeloescht)
            {
                yield return new CounterBereitsGeloescht(cmd.AggregateId);
                yield break;
            }

            if (cmd.MinGrenze.HasValue && this.State.MaxGrenze.HasValue
                && cmd.MinGrenze.Value > this.State.MaxGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "MinGrenze", "größer als aktuelle MaxGrenze");
                yield break;
            }

            if (cmd.MinGrenze.HasValue && this.State.Wert < cmd.MinGrenze.Value)
            {
                yield return new CounterEingabeUngueltig(
                    "MinGrenze", "größer als aktueller Wert");
                yield break;
            }

            yield return new MinGrenzeGeaendert(cmd.MinGrenze);
        }

        // ──────────────────────────────────────────
        // LÖSCHEN (Soft-Delete, idempotent)
        // ──────────────────────────────────────────

        public IEnumerable<OneOf<CounterGeloescht, CounterNichtGefunden>>
        Decide(LoescheCounter cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new CounterNichtGefunden(cmd.AggregateId);
                yield break;
            }

            // Idempotenz: schon gelöscht → No-Op
            if (this.State.IstGeloescht)
                yield break;

            yield return new CounterGeloescht();
        }
    }
}
