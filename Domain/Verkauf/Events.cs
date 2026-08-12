using Abstractions;

namespace Domain.Verkauf;

// ── Persistente Domain Events (rein fachlich; das Value Object Geldwert reist mit) ──
public record VerkaufsauftragEroeffnet(Guid KundeId, Geldwert Kreditlimit) : IEvent;
// „Menge hinzugefügt" — bei gleicher Artikelnummer verschmilzt der Applier die Menge (kein Duplikat).
public record VerkaufspositionHinzugefuegt(string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis) : IEvent;
public record VerkaufsauftragAufgegeben(Geldwert Gesamtsumme) : IEvent;
public record VerkaufsauftragStorniert(string Grund) : IEvent;

// ── Ablehnungen (ITransientEvent — nicht im Log; kommen als CommandResult.RejectionEvent zurück) ──
public record VerkaufsauftragExistiertBereits(Guid AggregateId) : ITransientEvent;
public record VerkaufsauftragNichtGefunden(Guid AggregateId) : ITransientEvent;
public record AuftragNichtOffen(Guid AggregateId) : ITransientEvent;
public record KreditlimitUeberschritten(Geldwert Versucht, Geldwert Limit) : ITransientEvent;
public record WaehrungPasstNicht(Waehrung Erwartet, Waehrung Erhalten) : ITransientEvent;
