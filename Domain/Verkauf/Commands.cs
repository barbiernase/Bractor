using Abstractions;

namespace Domain.Verkauf;

// Reine Commands — nur fachliche Felder, gerichtet an ein Aggregat (AggregateId).
public record EroeffneVerkaufsauftrag(Guid AggregateId, Guid KundeId, Geldwert Kreditlimit) : ICommand;
public record FuegePositionHinzu(Guid AggregateId, string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis) : ICommand;
public record GibVerkaufsauftragAuf(Guid AggregateId) : ICommand;
public record StorniereVerkaufsauftrag(Guid AggregateId, string Grund) : ICommand;
