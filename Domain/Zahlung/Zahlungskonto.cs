using Abstractions;

namespace Domain.Zahlung;

/// <summary>
/// Ziel-Aggregat „Zahlungskonto" der Bestell-Saga: das Guthaben eines Kunden. Zweiter Zweig des Diamanten.
/// REINE Domäne — keine Prozess-Mechanik; Idempotenz sichert die Framework-Inbox.
/// </summary>
public partial class Zahlungskonto : IState
{
    public decimal Saldo { get; set; }
}

public record RichteZahlungskontoEin(Guid AggregateId, decimal Guthaben) : ICommand;
public record BelasteKonto(Guid AggregateId, decimal Betrag) : ICommand;
public record ErstatteKonto(Guid AggregateId, decimal Betrag) : ICommand;

public record ZahlungskontoEingerichtet(decimal Guthaben) : IEvent;
public record KontoBelastet(decimal Betrag) : IEvent;
public record KontoErstattet(decimal Betrag) : IEvent;
public record KontoUngedeckt(Guid AggregateId, decimal Verfuegbar, decimal Angefordert) : ITransientEvent;

public partial class Zahlungskonto
{
    public partial class Decider : IDecider<Zahlungskonto>
    {
        public IEnumerable<OneOf<ZahlungskontoEingerichtet>> Decide(RichteZahlungskontoEin cmd)
        {
            if (this.State.Version > 0) yield break;
            yield return new ZahlungskontoEingerichtet(cmd.Guthaben);
        }

        public IEnumerable<OneOf<KontoBelastet, KontoUngedeckt>> Decide(BelasteKonto cmd)
        {
            if (this.State.Saldo < cmd.Betrag) { yield return new KontoUngedeckt(cmd.AggregateId, this.State.Saldo, cmd.Betrag); yield break; }
            yield return new KontoBelastet(cmd.Betrag);
        }

        public IEnumerable<OneOf<KontoErstattet>> Decide(ErstatteKonto cmd)
        {
            yield return new KontoErstattet(cmd.Betrag);
        }
    }

    public partial class Applier : IApplier<Zahlungskonto>
    {
        public void Apply(ZahlungskontoEingerichtet evt) => this.State.Saldo = evt.Guthaben;
        public void Apply(KontoBelastet evt) => this.State.Saldo -= evt.Betrag;
        public void Apply(KontoErstattet evt) => this.State.Saldo += evt.Betrag;
    }
}
