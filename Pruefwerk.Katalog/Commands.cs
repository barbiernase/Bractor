using Abstractions;

namespace Pruefwerk.Katalog;

// ── Abrechnung ──

// Die Nachrichten von Freigabe und Mahnung — domänenneutral: ein Vorgang und ein Empfänger sind Guids, keine Ids einer Anwendung.

/// <summary>Ein Betrag ist zur Freigabe zu prüfen.</summary>
public record Pruefe(Guid Vorgang, decimal Betrag) : ICommand;

/// <summary>Der Betrag liegt unter der Grenze: freigegeben.</summary>
public record Freigegeben(Guid Vorgang) : ICommand;

/// <summary>Der Betrag liegt über der Grenze: ein Mensch sieht ihn sich an.</summary>
public record ZurPruefungVorzulegen(Guid Vorgang) : ICommand;

/// <summary>Ein offener Betrag ist anzumahnen — beim Empfänger, in einer Stufe.</summary>
public record Mahne(Guid Vorgang, Guid Empfaenger, decimal Betrag, int Stufe) : ICommand;

/// <summary>Die Mahnung ist verschickt — mit der Versandnummer.</summary>
public record Gemahnt(Guid Vorgang, Guid Empfaenger, string Versand, int Stufe) : ICommand;

// ── Import ──

/// <summary>Ein Import ist angefordert — so viele Zeilen.</summary>
public record Anfordern(int Menge) : ICommand;

/// <summary>Der Import ist angenommen: ein neuer Vorgang (eine Id, aus dem Eingang abgeleitet).</summary>
public record Angenommen(Guid Vorgang, int Menge) : ICommand;

/// <summary>Ein Vorgang ist zu laufen — so viele Zeilen.</summary>
public record Laufen(Guid Vorgang, int Menge) : ICommand;

/// <summary>Eine Zeile ist gelesen: eine neue Id (aus dem Eingang und der Zeile) und ihr Wert.</summary>
public record Gelesen(Guid Id, int Wert) : ICommand;

/// <summary>Der Stand eines Vorgangs: so viele Zeilen gelesen.</summary>
public record Stand(Guid Vorgang, int Gelesen) : ICommand;

// ── Messung ──

/// <summary>Ein Wert einer Stelle ist gemessen.</summary>
public record Messwert(Guid Stelle, double Wert) : ICommand;

/// <summary>Das Merkmal eines Werts (Gabel: geprüft und geschätzt).</summary>
public record Merkmal(Guid Stelle, double Wert) : ICommand;

/// <summary>Ein Wert über der Schwelle.</summary>
public record Ausreisser(Guid Stelle, double Wert) : ICommand;

/// <summary>Die Schätzung zu einem Wert: der erwartete Wert und aus wie vielen bisherigen.</summary>
public record Geschaetzt(Guid Stelle, double Erwartet, int Anzahl) : ICommand;
