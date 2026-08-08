using Abstractions;

namespace Domain.Sammelauftrag;

/// <summary>
/// Trigger-Aggregat der Sammelüberweisung (eigener Namespace — pro Namespace gilt genau EIN Aggregat).
/// Sein Event <see cref="SammelUeberweisungBeauftragt"/> trägt die Fan-out-Breite (die Ziel-Liste) und
/// löst den Sammelüberweisungs-Prozess aus.
/// </summary>
public partial class Sammelueberweisungsauftrag : IState
{
    public bool Beauftragt { get; set; }
}

public record BeauftrageSammelueberweisung(Guid AggregateId, Guid Quelle, IReadOnlyList<Guid> Ziele, decimal BetragProZiel) : ICommand;

public record SammelUeberweisungBeauftragt(Guid Quelle, IReadOnlyList<Guid> Ziele, decimal BetragProZiel) : IEvent;

public partial class Sammelueberweisungsauftrag
{
    public partial class Decider : IDecider<Sammelueberweisungsauftrag>
    {
        public IEnumerable<OneOf<SammelUeberweisungBeauftragt>> Decide(BeauftrageSammelueberweisung cmd)
        {
            if (this.State.Version > 0) yield break;   // schon beauftragt → Noop
            yield return new SammelUeberweisungBeauftragt(cmd.Quelle, cmd.Ziele, cmd.BetragProZiel);
        }
    }

    public partial class Applier : IApplier<Sammelueberweisungsauftrag>
    {
        public void Apply(SammelUeberweisungBeauftragt evt) => this.State.Beauftragt = true;
    }
}
