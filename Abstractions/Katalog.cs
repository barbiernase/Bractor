namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIE MELDUNGEN DER QUELLARTEN DES FRAMEWORKS (docs/04 §4.7, Core/Katalog): was Dateiwächter, Taktgeber und Webhook melden. Eine MELDUNG (IMeldung)
// ist kein Command: sie geht an keinen Knoten und keinen Kopf, eine Übersetzung der Akteur-Seite macht daraus eine Nachricht, die der Akteur des
// Eingangs darf. Sie nennt die Kennung ihres Elements, aus seinem Inhalt. Meldungen stehen nicht im Log — ob eine Quelle Doppeltes vermeidet oder
// nach einem Neustart weitermacht, regelt sie über ihren gesicherten Zustand; die Kennung macht, dass dasselbe Element nach einem Neustart dieselben
// Ids ergibt, gleich in welcher Reihenfolge es wiederkommt.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Eine MELDUNG einer Quelle (docs/04 §4.7): sie nennt die <see cref="Kennung"/> ihres Elements — aus seinem Inhalt, nie aus einer Reihenfolge. Aus ihr
/// leitet die Laufzeit die Kennung der Kette ab und daraus alle Ids, die die Kette erzeugt: meldet die Quelle nach einem Neustart dasselbe Element
/// erneut (auch in anderer Reihenfolge), entstehen dieselben Ids, und die Inbox jedes Ziels verwirft, was schon gewirkt hat.
/// </summary>
public interface IMeldung
{
    /// <summary>Die Kennung des Elements: gleich für dasselbe Element, verschieden für verschiedene (Datei, Intervall, Aufruf eines Absenders).</summary>
    string Kennung { get; }
}

/// <summary>
/// Der Dateiwächter fand eine fertig geschriebene Datei. Ihre Kennung ist relativer Pfad, Größe und letzte Änderung — eine neu geschriebene Datei
/// desselben Namens ist ein anderes Element.
/// </summary>
/// <param name="Name">Dateiname ohne Verzeichnis.</param>
/// <param name="Pfad">Absoluter Pfad, wie der Host ihn sieht.</param>
/// <param name="RelativerPfad">Pfad relativ zum beobachteten Verzeichnis (für Rechner, die das Verzeichnis anders gemountet sehen).</param>
/// <param name="Groesse">Größe in Bytes, als die Datei fertig war.</param>
/// <param name="GeaendertAm">Letzte Änderung laut Dateisystem.</param>
/// <param name="ErkanntAm">Wann der Wächter sie als fertig erkannte.</param>
public sealed record DateiGefunden(string Name, string Pfad, string RelativerPfad, long Groesse, DateTimeOffset GeaendertAm, DateTimeOffset ErkanntAm)
    : IMeldung
{
    string IMeldung.Kennung => $"{RelativerPfad.Replace('\\', '/')}|{Groesse}|{GeaendertAm.UtcTicks}";
}

/// <summary>Der Taktgeber meldet den Beginn eines Intervalls (auf das Intervall gerundet, UTC) — je Intervall einmal; seine Kennung ist dieser Beginn.</summary>
public sealed record Takt(DateTimeOffset Zeitpunkt) : IMeldung
{
    string IMeldung.Kennung => Zeitpunkt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Der Host nahm einen Aufruf an einen Webhook an (eine Annahme, <see cref="IAnnahme{TMeldung}"/>): sein Inhalt (der Körper, wie gesendet) und die
/// Kennung, die der Absender im Kopf <c>kennung</c> mitgab — sie ist die Kennung der Meldung: wiederholt der Absender mit derselben Kennung, wirkt der
/// Aufruf einmal.
/// </summary>
public sealed record Aufruf(string Kennung, string Inhalt) : IMeldung;
