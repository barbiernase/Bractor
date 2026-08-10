using Abstractions;

namespace Domain.Antrag;

/// <summary>
/// Auslöser-Aggregat der PROZESS-VERKETTUNGS-Demo: ein Antrag wird gestellt und startet den ersten Prozess
/// (Genehmigung). REINE Domäne — kennt keine Prozesse. <see cref="StelleAntrag.Vorgang"/> ist eine EIGENE,
/// distinkte Id für das Vorgang-Aggregat (jeder Aggregat-Stream braucht eine eindeutige Guid — Anleitung §9).
/// </summary>
public partial class Antrag : IState
{
    public bool Gestellt { get; set; }
}

public record StelleAntrag(Guid AggregateId, Guid Vorgang) : ICommand;
public record AntragGestellt(Guid AntragId, Guid Vorgang) : IEvent;

public partial class Antrag
{
    public partial class Decider : IDecider<Antrag>
    {
        public IEnumerable<OneOf<AntragGestellt>> Decide(StelleAntrag cmd)
        {
            if (this.State.Version > 0) yield break;   // schon gestellt → Noop (idempotent)
            yield return new AntragGestellt(cmd.AggregateId, cmd.Vorgang);
        }
    }

    public partial class Applier : IApplier<Antrag>
    {
        public void Apply(AntragGestellt evt) => this.State.Gestellt = true;
    }
}
