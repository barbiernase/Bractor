using Abstractions;

namespace Domain.Sammelvorgang;

public partial class Sammelvorgang
{
    public partial class Decider : IDecider<Sammelvorgang>
    {
        // ═══════════════════════════════════════════════════
        // START — N wird zur Laufzeit übergeben (statisch nicht wissbar)
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<SammelvorgangGestartet, SammelvorgangAbgeschlossen, SammelvorgangExistiertBereits>> Decide(
            StarteSammelvorgang cmd)
        {
            if (this.State.Existiert)
            {
                yield return new SammelvorgangExistiertBereits(cmd.AggregateId);
                yield break;
            }

            yield return new SammelvorgangGestartet(cmd.Anzahl);

            // Leere Menge (N ≤ 0): die Barriere ist sofort erfüllt — kein Teil kann sie je auslösen.
            if (cmd.Anzahl <= 0)
                yield return new SammelvorgangAbgeschlossen();
        }

        // ═══════════════════════════════════════════════════
        // TEIL FERTIG — zählen; die Barriere feuert erst beim N-ten (N = Laufzeitwert)
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<TeilVerbucht, SammelvorgangAbgeschlossen, SammelvorgangNichtGefunden, SammelvorgangBereitsAbgeschlossen>> Decide(
            MeldeTeilFertig cmd)
        {
            if (!this.State.Existiert)
            {
                yield return new SammelvorgangNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.Abgeschlossen)
            {
                yield return new SammelvorgangBereitsAbgeschlossen(cmd.AggregateId);
                yield break;
            }

            var fertig = this.State.Fertig + 1;
            yield return new TeilVerbucht(fertig, this.State.Erwartet);

            // DIE BARRIERE: erst wenn ALLE N da sind, feuert der Abschluss. N ist State.Erwartet
            // (Laufzeitwert), der Vergleich passiert also zur Laufzeit — nicht compile-time.
            if (fertig >= this.State.Erwartet)
                yield return new SammelvorgangAbgeschlossen();
        }
    }
}
