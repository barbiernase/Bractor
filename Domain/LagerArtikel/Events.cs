using System;
using Abstractions;

namespace Domain.Lagerartikel;

// ═══════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — werden persistiert + publiziert
// ═══════════════════════════════════════════════════

public record LagerartikelErstellt(Guid AggregateId, string Name, int InitialeAnzahl) : IEvent;
public record WareneingangGebucht(int Anzahl) : IEvent;
public record WarenabgangGebucht(int Anzahl) : IEvent;
public record LagerartikelDeaktiviert() : IEvent;
    
public record LagerplatzGeaendert(Lagerplatz NeuerPlatz) : IEvent;
public record EinkaufspreisAngepasst(Geldbetrag NeuerPreis) : IEvent;
public record LieferantZugeordnet(Guid LieferantId) : IEvent;
public record BestandReserviert(Guid AuftragsId, int Anzahl) : IEvent;

// Reaktives Event — wird von LagerbestandProjection erzeugt wenn Bestand niedrig
public record NachbestellungAngefordert(Guid ArtikelId, int Menge) : IEvent;

// ═══════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent) — nur Targeted Delivery an Aufrufer
// Nicht persistiert, kein Apply, keine Version-Änderung.
// ═══════════════════════════════════════════════════

// --- Identitäts-Ablehnungen ---

/// <summary>Artikel existiert bereits (Version > 0 bei Erstellung).</summary>
public record ArtikelExistiertBereits(Guid ArtikelId) : ITransientEvent;

/// <summary>Artikel nicht gefunden (Version == 0, kein Event-Stream).</summary>
public record ArtikelNichtGefunden(Guid ArtikelId) : ITransientEvent;

/// <summary>Artikel ist deaktiviert, Operation nicht möglich.</summary>
public record ArtikelInaktiv(Guid ArtikelId) : ITransientEvent;

// --- Bestands-Ablehnungen ---

/// <summary>Lagerbestand reicht nicht für den Warenabgang.</summary>
public record BestandNichtAusreichend(int Angefordert, int Verfuegbar) : ITransientEvent;

/// <summary>Verfügbarer (nicht-reservierter) Bestand reicht nicht für die Reservierung.</summary>
public record VerfuegbarerBestandNichtAusreichend(int Angefordert, int Verfuegbar) : ITransientEvent;

// --- Eingabevalidierung ---

/// <summary>
/// Ungültige Eingabe — für einfache Validierungsregeln die keine eigene Domänensemantik tragen.
/// Beispiele: leerer Name, negative Anzahl, negativer Preis.
/// </summary>
public record EingabeUngueltig(string Feld, string Grund) : ITransientEvent;