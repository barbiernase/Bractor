using Abstractions;

namespace Domain.Konto;

// Reine Commands — nur fachliche Felder. Kein Vorgang, kein Marker: das Framework injiziert die
// Idempotenz über die Envelope-CommandId, nicht über den Command-Typ.
public record EroeffneKonto(Guid AggregateId, decimal StartSaldo, bool Gesperrt = false) : ICommand;
public record ReserviereBetrag(Guid AggregateId, decimal Betrag) : ICommand;
public record GebeReservierungFrei(Guid AggregateId, decimal Betrag) : ICommand;
public record SchreibeGut(Guid AggregateId, decimal Betrag) : ICommand;
public record StorniereGutschrift(Guid AggregateId, decimal Betrag) : ICommand;
public record BucheReservierung(Guid AggregateId, decimal Betrag) : ICommand;
