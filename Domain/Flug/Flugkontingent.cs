using Abstractions;

namespace Domain.Flug;

/// <summary>
/// Ziel-Aggregat „Flugkontingent" der Reisebuchungs-Saga: freie Plätze eines Flugs (erster paralleler
/// Zweig des Diamanten). REINE Domäne — kein Wissen über Prozesse, Vorgänge oder Dedup. Die Exactly-once-
/// Wirkung sichert die Framework-Inbox (deterministische CommandId), nicht der Fachcode.
/// </summary>
public partial class Flugkontingent : IState
{
    public int Plaetze { get; set; }      // frei
}

// ── Commands (nur fachliche Felder — ein Command ist ein Command) ──
public record RichteFlugEin(Guid AggregateId, int Plaetze) : ICommand;
public record ReserviereFlug(Guid AggregateId, int Anzahl) : ICommand;
public record GebeFlugFrei(Guid AggregateId, int Anzahl) : ICommand;

// ── Events ──
public record FlugEingerichtet(int Plaetze) : IEvent;
public record FlugReserviert(int Anzahl) : IEvent;
public record FlugFreigegeben(int Anzahl) : IEvent;

// ── Ablehnungen (transient — lösen die Kompensation aus) ──
public record FlugAusgebucht(Guid AggregateId, int Frei, int Angefordert) : ITransientEvent;
public record FlugExistiertBereits(Guid AggregateId) : ITransientEvent;

public partial class Flugkontingent
{
    public partial class Decider : IDecider<Flugkontingent>
    {
        public IEnumerable<OneOf<FlugEingerichtet, FlugExistiertBereits>> Decide(RichteFlugEin cmd)
        {
            if (this.State.Version > 0) { yield return new FlugExistiertBereits(cmd.AggregateId); yield break; }
            yield return new FlugEingerichtet(cmd.Plaetze);
        }

        public IEnumerable<OneOf<FlugReserviert, FlugAusgebucht>> Decide(ReserviereFlug cmd)
        {
            if (this.State.Plaetze < cmd.Anzahl) { yield return new FlugAusgebucht(cmd.AggregateId, this.State.Plaetze, cmd.Anzahl); yield break; }
            yield return new FlugReserviert(cmd.Anzahl);
        }

        public IEnumerable<OneOf<FlugFreigegeben>> Decide(GebeFlugFrei cmd)
        {
            yield return new FlugFreigegeben(cmd.Anzahl);
        }
    }

    public partial class Applier : IApplier<Flugkontingent>
    {
        public void Apply(FlugEingerichtet evt) => this.State.Plaetze = evt.Plaetze;
        public void Apply(FlugReserviert evt) => this.State.Plaetze -= evt.Anzahl;
        public void Apply(FlugFreigegeben evt) => this.State.Plaetze += evt.Anzahl;
    }
}
