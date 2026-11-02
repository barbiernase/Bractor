namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIE COMMANDS DES FUNKTIONS-KATALOGS (docs/04 §4.7, Entscheidung 8): was die Quellfunktionen des Frameworks melden (Core/Katalog). Eine Quelle ist
// nichts Eigenes: eine Funktion am Kopf eines Graphen, die lauscht (Lausche statt Auf); was sie meldet, ist ein gewöhnlicher Command ohne Ziel, den
// der nächste Knoten des Graphen nimmt. Quell-Commands stehen nicht im Log — ob eine Quelle Doppeltes vermeidet oder nach einem Neustart weitermacht,
// regelt sie über ihren gesicherten Zustand.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Der Dateiwächter fand eine fertig geschriebene Datei.
/// </summary>
/// <param name="Name">Dateiname ohne Verzeichnis.</param>
/// <param name="Pfad">Absoluter Pfad, wie der Host ihn sieht.</param>
/// <param name="RelativerPfad">Pfad relativ zum beobachteten Verzeichnis (für Rechner, die das Verzeichnis anders gemountet sehen).</param>
/// <param name="Groesse">Größe in Bytes, als die Datei fertig war.</param>
/// <param name="GeaendertAm">Letzte Änderung laut Dateisystem.</param>
/// <param name="ErkanntAm">Wann der Wächter sie als fertig erkannte.</param>
public sealed record DateiGefunden(string Name, string Pfad, string RelativerPfad, long Groesse, DateTimeOffset GeaendertAm, DateTimeOffset ErkanntAm)
    : ICommand;

/// <summary>Der Taktgeber meldet den Beginn eines Intervalls (auf das Intervall gerundet, UTC) — je Intervall einmal.</summary>
public sealed record Takt(DateTimeOffset Zeitpunkt) : ICommand;

/// <summary>Der Webhook nahm einen Aufruf an: sein Inhalt (JSON, wie gesendet) und die Kennung, die der Absender mitgab (sonst eine neue).</summary>
public sealed record Aufruf(string Kennung, string Inhalt) : ICommand;
