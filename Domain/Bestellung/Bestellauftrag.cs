using Abstractions;

namespace Domain.Bestellung;

/// <summary>
/// Trigger-Aggregat der Bestell-Saga (eigener Namespace — pro Namespace genau EIN Aggregat). Sein Event
/// <see cref="BestellungAufgegeben"/> ist der fachliche Auslöser, aus dem der Korrelations-Router den
/// Bestell-Prozess startet. Es trägt die Bestell-Id (= sein eigener Stream) explizit als Feld, weil die
/// Prozess-Regeln nur den Event-Payload sehen (nicht den Envelope).
/// </summary>
public partial class Bestellauftrag : IState
{
    public bool Aufgegeben { get; set; }
}

public record GibBestellungAuf(Guid AggregateId, Guid Versand, Guid Kunde, Guid Artikel, int Menge, decimal Betrag) : ICommand;

// Trägt die Bestell-Id (= eigener Stream) UND die Versand-Id (eigener Aggregat-Stream, ≠ Bestellung —
// der Event-Store keyt Streams per Guid, also braucht jedes Aggregat eine eindeutige Id).
public record BestellungAufgegeben(Guid Bestellung, Guid Versand, Guid Kunde, Guid Artikel, int Menge, decimal Betrag) : IEvent;

public partial class Bestellauftrag
{
    public partial class Decider : IDecider<Bestellauftrag>
    {
        public IEnumerable<OneOf<BestellungAufgegeben>> Decide(GibBestellungAuf cmd)
        {
            if (this.State.Version > 0) yield break;   // schon aufgegeben → Noop
            yield return new BestellungAufgegeben(cmd.AggregateId, cmd.Versand, cmd.Kunde, cmd.Artikel, cmd.Menge, cmd.Betrag);
        }
    }

    public partial class Applier : IApplier<Bestellauftrag>
    {
        public void Apply(BestellungAufgegeben evt) => this.State.Aufgegeben = true;
    }
}
