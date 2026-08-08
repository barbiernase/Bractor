using Abstractions;

namespace Domain.Versand;

/// <summary>
/// Ziel-Aggregat „Versand" der Bestell-Saga: der VEREINIGUNGS-Punkt des Diamanten. <c>Versende</c> feuert
/// erst, wenn beide Zweige fertig sind. REINE Domäne. „Ein Versand versendet höchstens einmal" ist eine
/// fachliche Invariante (kein Prozess-Dedup); die Framework-Inbox verhindert zusätzlich das Doppel-Feuern.
/// </summary>
public partial class Versand : IState
{
    public bool Versandt { get; set; }
}

public record Versende(Guid AggregateId, Guid Kunde) : ICommand;
public record Versendet(Guid Kunde) : IEvent;

public partial class Versand
{
    public partial class Decider : IDecider<Versand>
    {
        public IEnumerable<OneOf<Versendet>> Decide(Versende cmd)
        {
            if (this.State.Versandt) yield break;   // fachlich: nur einmal versenden
            yield return new Versendet(cmd.Kunde);
        }
    }

    public partial class Applier : IApplier<Versand>
    {
        public void Apply(Versendet evt) => this.State.Versandt = true;
    }
}
