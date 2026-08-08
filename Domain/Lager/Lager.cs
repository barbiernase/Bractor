using Abstractions;

namespace Domain.Lager;

/// <summary>
/// Ziel-Aggregat „Lager" der Bestell-Saga: verfügbarer + reservierter Bestand eines Artikels. REINE
/// Domäne — kein Wissen über Prozesse, Vorgänge oder Dedup. Id/Version/Handler/Actor/Signale/DTOs
/// generiert das Framework; die Exactly-once-Wirkung sichert die Framework-Inbox (deterministische
/// CommandId), nicht der Fachcode.
/// </summary>
public partial class Lager : IState
{
    public int Bestand { get; set; }      // verfügbar
    public int Reserviert { get; set; }
}

// ── Commands (nur fachliche Felder — ein Command ist ein Command) ──
public record RichteLagerEin(Guid AggregateId, int Bestand) : ICommand;
public record ReserviereBestand(Guid AggregateId, int Menge) : ICommand;
public record GebeBestandFrei(Guid AggregateId, int Menge) : ICommand;

// ── Events ──
public record LagerEingerichtet(int Bestand) : IEvent;
public record BestandReserviert(int Menge) : IEvent;
public record BestandFreigegeben(int Menge) : IEvent;

// ── Ablehnungen (transient — lösen die Kompensation aus) ──
public record BestandReichtNicht(Guid AggregateId, int Verfuegbar, int Angefordert) : ITransientEvent;
public record LagerExistiertBereits(Guid AggregateId) : ITransientEvent;

public partial class Lager
{
    public partial class Decider : IDecider<Lager>
    {
        public IEnumerable<OneOf<LagerEingerichtet, LagerExistiertBereits>> Decide(RichteLagerEin cmd)
        {
            if (this.State.Version > 0) { yield return new LagerExistiertBereits(cmd.AggregateId); yield break; }
            yield return new LagerEingerichtet(cmd.Bestand);
        }

        public IEnumerable<OneOf<BestandReserviert, BestandReichtNicht>> Decide(ReserviereBestand cmd)
        {
            if (this.State.Bestand < cmd.Menge) { yield return new BestandReichtNicht(cmd.AggregateId, this.State.Bestand, cmd.Menge); yield break; }
            yield return new BestandReserviert(cmd.Menge);
        }

        public IEnumerable<OneOf<BestandFreigegeben>> Decide(GebeBestandFrei cmd)
        {
            yield return new BestandFreigegeben(cmd.Menge);
        }
    }

    public partial class Applier : IApplier<Lager>
    {
        public void Apply(LagerEingerichtet evt) => this.State.Bestand = evt.Bestand;

        public void Apply(BestandReserviert evt)
        {
            this.State.Bestand -= evt.Menge;
            this.State.Reserviert += evt.Menge;
        }

        public void Apply(BestandFreigegeben evt)
        {
            this.State.Reserviert -= evt.Menge;
            this.State.Bestand += evt.Menge;
        }
    }
}
