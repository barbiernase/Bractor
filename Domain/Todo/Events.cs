using Abstractions;

namespace Domain.Todo;

// ═══════════════════════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — werden persistiert + publiziert
// ═══════════════════════════════════════════════════════════════════

public record TodoErstellt(
    string Titel, string? Beschreibung,
    Prioritaet Prioritaet, DateTimeOffset? Faelligkeit) : IEvent;

public record TitelGeaendert(string NeuerTitel) : IEvent;
public record PrioritaetGesetzt(Prioritaet NeuePrioritaet) : IEvent;
public record AlsErledigtMarkiert() : IEvent;
public record WiederGeoeffnet() : IEvent;
public record Archiviert() : IEvent;
public record TagHinzugefuegt(string Tag) : IEvent;
public record TagEntfernt(string Tag) : IEvent;

// ═══════════════════════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent) — nur Targeted Delivery
// Nicht persistiert, kein Apply, keine Version-Änderung.
// ═══════════════════════════════════════════════════════════════════

/// <summary>Todo existiert bereits (Version > 0 bei Erstellung).</summary>
public record TodoExistiertBereits(Guid TodoId) : ITransientEvent;

/// <summary>Todo nicht gefunden (Version == 0, kein Event-Stream).</summary>
public record TodoNichtGefunden(Guid TodoId) : ITransientEvent;

/// <summary>Todo ist archiviert — keine Änderungen mehr möglich.</summary>
public record TodoBereitsArchiviert(Guid TodoId) : ITransientEvent;

/// <summary>Todo ist bereits erledigt.</summary>
public record TodoBereitsErledigt(Guid TodoId) : ITransientEvent;

/// <summary>Todo ist nicht erledigt — OeffneWieder hat keinen Effekt.</summary>
public record TodoNichtErledigt(Guid TodoId) : ITransientEvent;

/// <summary>Ungültige Eingabe — leerer Titel, leerer Tag, etc.</summary>
public record TodoEingabeUngueltig(string Feld, string Grund) : ITransientEvent;