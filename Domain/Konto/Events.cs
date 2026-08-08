using Abstractions;

namespace Domain.Konto;

// ── Persistente Events (rein fachlich, mengenbasiert — kein Vorgang) ──
public record KontoEroeffnet(decimal StartSaldo, bool Gesperrt) : IEvent;
public record BetragReserviert(decimal Betrag) : IEvent;
public record ReservierungFreigegeben(decimal Betrag) : IEvent;
public record Gutgeschrieben(decimal Betrag) : IEvent;
public record GutschriftStorniert(decimal Betrag) : IEvent;
public record ReservierungGebucht(decimal Betrag) : IEvent;

// ── Ablehnungen (ITransientEvent — nicht im Log; kommen als CommandResult.RejectionEvent zurück und
//    lösen beim Prozess die Kompensation aus) ──
public record KontoGesperrt(Guid AggregateId) : ITransientEvent;
public record DeckungReichtNicht(decimal Verfuegbar, decimal Angefordert) : ITransientEvent;
public record KontoExistiertBereits(Guid AggregateId) : ITransientEvent;
public record KontoNichtGefunden(Guid AggregateId) : ITransientEvent;
