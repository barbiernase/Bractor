using Abstractions;

namespace Domain.Spende;

/// <summary>
/// Eine Spenden-Kampagne — das autoritative Aggregat (Event-Sourced). Jede Spende ist ein Command
/// an die Kampagne; der Applier summiert den Topf aus dem Log (die WAHRE Summe). Bewusst winzig:
/// genau so viel Domänencode, wie ein Anwendungsentwickler schreibt. Framework-Pflichtfelder (Id,
/// Version) generiert der Aggregat-Generator.
/// </summary>
public partial class Kampagne : IState
{
    /// <summary>Der gesammelte Betrag — die autoritative Summe aus dem Event-Log (OCC-serialisiert).</summary>
    public decimal Gesammelt { get; set; }

    /// <summary>Die Zahl der eingegangenen Spenden.</summary>
    public int Anzahl { get; set; }
}

// Command (imperativ) + Event (Vergangenheit) — rein fachlich, keine Framework-Felder.
public record SpendeEin(Guid AggregateId, decimal Betrag) : ICommand;
public record SpendeEingegangen(decimal Betrag) : IEvent;

public partial class Kampagne
{
    public partial class Decider : IDecider<Kampagne>
    {
        public IEnumerable<OneOf<SpendeEingegangen>> Decide(SpendeEin cmd)
        {
            if (cmd.Betrag <= 0) yield break;   // ungültige Spende → Noop
            yield return new SpendeEingegangen(cmd.Betrag);
        }
    }

    public partial class Applier : IApplier<Kampagne>
    {
        public void Apply(SpendeEingegangen evt)
        {
            this.State.Gesammelt += evt.Betrag;
            this.State.Anzahl += 1;
        }
    }
}
