using Abstractions;

namespace Domain.Reise;

/// <summary>
/// Ziel-Aggregat „Reise" der Reisebuchungs-Saga: der VEREINIGUNGS-Punkt des Diamanten. <c>BestaetigeReise</c>
/// feuert erst, wenn beide Zweige (Flug ∥ Hotel) reserviert sind. REINE Domäne. „Eine Reise bestätigt
/// höchstens einmal" ist eine fachliche Invariante (kein Prozess-Dedup); die Framework-Inbox verhindert
/// zusätzlich das Doppel-Feuern.
/// </summary>
public partial class Reise : IState
{
    public bool Bestaetigt { get; set; }
}

public record BestaetigeReise(Guid AggregateId, Guid Kunde) : ICommand;
public record ReiseBestaetigt(Guid Kunde) : IEvent;

public partial class Reise
{
    public partial class Decider : IDecider<Reise>
    {
        public IEnumerable<OneOf<ReiseBestaetigt>> Decide(BestaetigeReise cmd)
        {
            if (this.State.Bestaetigt) yield break;   // fachlich: nur einmal bestätigen
            yield return new ReiseBestaetigt(cmd.Kunde);
        }
    }

    public partial class Applier : IApplier<Reise>
    {
        public void Apply(ReiseBestaetigt evt) => this.State.Bestaetigt = true;
    }
}
