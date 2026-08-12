using Abstractions;

namespace Domain.Verkauf;

// ── Persistente Domain Events — fachliche Fakten in der Vergangenheitsform ──
public record VerkaufsauftragEroeffnet(Guid KundeId, Geldwert Kreditlimit) : IEvent;
// „Menge hinzugefügt" — bei gleicher Artikelnummer verschmilzt die Wurzel die Menge (kein Duplikat).
public record VerkaufspositionHinzugefuegt(string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis) : IEvent;
public record VerkaufsauftragAufgegeben(Geldwert Gesamtsumme) : IEvent;
public record VerkaufsauftragStorniert(string Grund) : IEvent;

// ── Ablehnungen (ITransientEvent — gehören nicht in die Historie; fachliche Absagen) ──
public record VerkaufsauftragExistiertBereits(Guid AggregateId) : ITransientEvent;
public record VerkaufsauftragNichtGefunden(Guid AggregateId) : ITransientEvent;
public record AuftragNichtOffen(Guid AggregateId) : ITransientEvent;
public record KreditlimitUeberschritten(Geldwert Versucht, Geldwert Limit) : ITransientEvent;
public record WaehrungPasstNicht(Waehrung Erwartet, Waehrung Erhalten) : ITransientEvent;
