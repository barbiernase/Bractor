using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.Kuratieren;

// Client-lokale Intents (IClientEvent) der „Datensatz-als-Tag"-Kuratierung.
// Der KuratierenIntentHandler übersetzt sie in Datensatz-Commands.

/// <summary>
/// Mitgliedschaft eines Bildpaars im aktiven Sammel-Ziel umschalten (Konzept §5).
/// <paramref name="PairId"/> == null → das aktuelle Cursor-Paar (Einbild-Geste, Taste A);
/// gesetzt → ein konkretes Paar (Galerie-Badge-Klick).
/// </summary>
public record DatensatzTagGetoggelt(Guid? PairId = null) : IClientEvent;

// ── Dedizierte Mehrfach-Auswahl (Rubber-Band), Konzept §5 ──
// Die Markierung ist ein eigener, sichtbarer Zustand (Häkchen auf den Kacheln),
// unabhängig von der Mitgliedschaft.

/// <summary>Eine einzelne Kachel markieren/entmarkieren (Klick in der Auswahl-Mode).</summary>
public record MarkierungGetoggelt(Guid PairId) : IClientEvent;

/// <summary>Einen aufgezogenen Bereich (Rubber-Band) zur Markierung hinzufügen (Union).</summary>
public record MarkierungBereichHinzugefuegt(IReadOnlyList<Guid> PairIds) : IClientEvent;

/// <summary>Die Markierung leeren.</summary>
public record MarkierungGeleert() : IClientEvent;

/// <summary>
/// Die aktuelle Markierung als Batch ins aktive Sammel-Ziel aufnehmen. Der IntentHandler
/// dispatcht dafür ein einzelnes <c>NimmRangeAuf</c> (Herkunft „manuelle Auswahl",
/// Wiederverwendung — KEIN neues Batch-Command) und leert danach die Markierung.
/// </summary>
public record AuswahlAufgenommenAngefordert() : IClientEvent;
