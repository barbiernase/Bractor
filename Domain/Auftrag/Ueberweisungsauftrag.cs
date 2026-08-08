using Abstractions;

namespace Domain.Auftrag;

/// <summary>
/// Trigger-Aggregat des Piloten (eigener Namespace, weil pro Namespace genau EIN Aggregat gilt —
/// die Command→Aggregat-Zuordnung des Generators leitet den Aggregat-Namen aus dem Namespace ab).
/// Sein einziges Event ist der fachliche Auslöser, auf den der Handler <c>Ueberweisungen</c> reagiert
/// und den Prozess startet. So läuft der Pilot end-to-end aus EINEM dispatchten Fach-Command.
/// </summary>
public partial class Ueberweisungsauftrag : IState
{
    public bool Beauftragt { get; set; }
}

public record BeauftrageUeberweisung(Guid AggregateId, Guid Quelle, Guid Ziel, decimal Betrag) : ICommand;

public record UeberweisungBeauftragt(Guid Quelle, Guid Ziel, decimal Betrag) : IEvent;

public partial class Ueberweisungsauftrag
{
    public partial class Decider : IDecider<Ueberweisungsauftrag>
    {
        public IEnumerable<OneOf<UeberweisungBeauftragt>> Decide(BeauftrageUeberweisung cmd)
        {
            if (this.State.Version > 0) yield break;   // schon beauftragt → Noop
            yield return new UeberweisungBeauftragt(cmd.Quelle, cmd.Ziel, cmd.Betrag);
        }
    }

    public partial class Applier : IApplier<Ueberweisungsauftrag>
    {
        public void Apply(UeberweisungBeauftragt evt) => this.State.Beauftragt = true;
    }
}
