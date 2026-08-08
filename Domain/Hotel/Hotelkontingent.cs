using Abstractions;

namespace Domain.Hotel;

/// <summary>
/// Ziel-Aggregat „Hotelkontingent" der Reisebuchungs-Saga: freie Zimmer eines Hotels (zweiter paralleler
/// Zweig des Diamanten). REINE Domäne — keine Prozess-Mechanik; Idempotenz sichert die Framework-Inbox.
/// </summary>
public partial class Hotelkontingent : IState
{
    public int Zimmer { get; set; }      // frei
}

// ── Commands ──
public record RichteHotelEin(Guid AggregateId, int Zimmer) : ICommand;
public record ReserviereHotel(Guid AggregateId, int Anzahl) : ICommand;
public record GebeHotelFrei(Guid AggregateId, int Anzahl) : ICommand;

// ── Events ──
public record HotelEingerichtet(int Zimmer) : IEvent;
public record HotelReserviert(int Anzahl) : IEvent;
public record HotelFreigegeben(int Anzahl) : IEvent;

// ── Ablehnungen (transient — lösen die Kompensation aus) ──
public record HotelAusgebucht(Guid AggregateId, int Frei, int Angefordert) : ITransientEvent;
public record HotelExistiertBereits(Guid AggregateId) : ITransientEvent;

public partial class Hotelkontingent
{
    public partial class Decider : IDecider<Hotelkontingent>
    {
        public IEnumerable<OneOf<HotelEingerichtet, HotelExistiertBereits>> Decide(RichteHotelEin cmd)
        {
            if (this.State.Version > 0) { yield return new HotelExistiertBereits(cmd.AggregateId); yield break; }
            yield return new HotelEingerichtet(cmd.Zimmer);
        }

        public IEnumerable<OneOf<HotelReserviert, HotelAusgebucht>> Decide(ReserviereHotel cmd)
        {
            if (this.State.Zimmer < cmd.Anzahl) { yield return new HotelAusgebucht(cmd.AggregateId, this.State.Zimmer, cmd.Anzahl); yield break; }
            yield return new HotelReserviert(cmd.Anzahl);
        }

        public IEnumerable<OneOf<HotelFreigegeben>> Decide(GebeHotelFrei cmd)
        {
            yield return new HotelFreigegeben(cmd.Anzahl);
        }
    }

    public partial class Applier : IApplier<Hotelkontingent>
    {
        public void Apply(HotelEingerichtet evt) => this.State.Zimmer = evt.Zimmer;
        public void Apply(HotelReserviert evt) => this.State.Zimmer -= evt.Anzahl;
        public void Apply(HotelFreigegeben evt) => this.State.Zimmer += evt.Anzahl;
    }
}
