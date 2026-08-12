using Abstractions;

namespace Domain.Verkauf;

// Reine Commands — nur fachliche Felder (das VALUE OBJECT Geldwert reist mit). Idempotenz
// sichert die Framework-Inbox über die Envelope-CommandId, nicht der Command-Typ.
public record EroeffneVerkaufsauftrag(Guid AggregateId, Guid KundeId, Geldwert Kreditlimit) : ICommand;
public record FuegePositionHinzu(Guid AggregateId, string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis) : ICommand;
public record GibVerkaufsauftragAuf(Guid AggregateId) : ICommand;
public record StorniereVerkaufsauftrag(Guid AggregateId, string Grund) : ICommand;
