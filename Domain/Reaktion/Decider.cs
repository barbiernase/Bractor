using Abstractions;

namespace Domain.Reaktion;

public partial class Reaktionsempfaenger
{
    public partial class Decider : IDecider<Reaktionsempfaenger>
    {
        /// <summary>
        /// Noop-Decider (Spec 9.3): Ist die Reaktions-Id schon verarbeitet, produziert
        /// der Decider KEINE Events → der wiederholte Command verpufft (Erfolg ohne
        /// Wirkung, keine neuen Signale). Sonst wird die Reaktion genau einmal wirksam.
        /// </summary>
        public IEnumerable<OneOf<ReaktionGewirkt>> Decide(WirkeReaktion cmd)
        {
            if (this.State.VerarbeiteteReaktionen.Contains(cmd.ReaktionId))
                yield break;   // Duplikat → Noop

            yield return new ReaktionGewirkt(cmd.ReaktionId, cmd.Notiz, DateTimeOffset.UtcNow);
        }
    }
}
