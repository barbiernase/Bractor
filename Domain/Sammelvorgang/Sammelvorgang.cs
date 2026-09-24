using Abstractions;

namespace Domain.Sammelvorgang;

/// <summary>
/// Aggregat <see cref="Sammelvorgang"/> — die „warte auf ALLE N"-Barriere als SKALARER Zähler.
///
/// Der Zweck: eine Aktion auf Batch-Ebene erst auslösen, wenn ALLE N aufgefächerten Teile fertig
/// sind (Fan-in / Synchronisations-Barriere). N ist ein <b>Laufzeitwert</b> — er wird beim Start
/// übergeben (der Client/die Pipeline kennt ihn erst dann); statisch/compile-time ist er NICHT
/// wissbar. Genau deshalb kann das kein festes Schlüsselwort sein, sondern muss aus den Daten kommen.
///
/// Bewusst OHNE Collection: der State hält nur zwei Zahlen + ein Flag — ein einzelner Datenpunkt,
/// „gesammelt" wird nur gezählt (Haltung: Aggregate = einzelne Punkte). Die Liste der Teile lebt
/// nirgends im Schreibmodell; nur die skalare Anzahl N ist ein durables Faktum.
///
/// Idempotenz doppelter Teil-Meldungen liegt in der Framework-Inbox (deterministische CommandId je
/// Teil), nicht im Aggregat — der Fachcode bleibt rein (Invariante 5). Id/Version generiert.
/// </summary>
public partial class Sammelvorgang : IState
{
    /// <summary>N — die erwartete Anzahl Teile. Zur Laufzeit beim Start gesetzt.</summary>
    public int Erwartet { get; set; }

    /// <summary>Wie viele Teile bisher fertig gemeldet wurden.</summary>
    public int Fertig { get; set; }

    /// <summary>Barriere erreicht — alle N sind da; terminal.</summary>
    public bool Abgeschlossen { get; set; }

    // ─── Helfer ───

    public bool Existiert => Version > 0;

    /// <summary>Fortschritt in Prozent (0..100) — für Live-Anzeige, rein abgeleitet.</summary>
    public int FortschrittProzent => Erwartet <= 0 ? 100 : (int)(100L * Fertig / Erwartet);
}
