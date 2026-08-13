using Abstractions;

namespace Domain.Willkommensbonus;

/// <summary>
/// Kleines Auftrags-Aggregat, das einen Willkommensbonus anstößt (analog zum Überweisungsauftrag).
/// Es hält nur fest, DASS ein Bonus beauftragt wurde, und emittiert den Auslöser-Event für die Saga.
/// Die Geldbewegung selbst passiert auf den zwei Konten (Promo-Konto → neues Konto), orchestriert
/// vom <c>WillkommensbonusProzess</c>. Framework-Pflichtfelder (Id, Version) werden generiert.
/// </summary>
public partial class Willkommensbonusauftrag : IState
{
    public bool Beauftragt { get; set; }
}

// Neues Konto und Promo-Konto sind DOMÄNEN-Referenzen (welche Konten), keine Aggregat-Ids —
// genau wie Quelle/Ziel bei der Überweisung. Der Betrag reist mengenbasiert durch den Auslöser.
public record BeauftrageWillkommensbonus(Guid AggregateId, Guid NeuesKonto, Guid PromoKonto, decimal Betrag) : ICommand;
public record WillkommensbonusBeauftragt(Guid NeuesKonto, Guid PromoKonto, decimal Betrag) : IEvent;

public partial class Willkommensbonusauftrag
{
    public partial class Decider : IDecider<Willkommensbonusauftrag>
    {
        public IEnumerable<OneOf<WillkommensbonusBeauftragt>> Decide(BeauftrageWillkommensbonus cmd)
        {
            yield return new WillkommensbonusBeauftragt(cmd.NeuesKonto, cmd.PromoKonto, cmd.Betrag);
        }
    }

    public partial class Applier : IApplier<Willkommensbonusauftrag>
    {
        public void Apply(WillkommensbonusBeauftragt evt) => this.State.Beauftragt = true;
    }
}
