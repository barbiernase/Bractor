using System;
using System.Collections.Generic;

namespace Infrastructure.Prozess;

/// <summary>
/// P5(a) — die Entscheidung des Prozess-Poll-Backstops, ob ein geändertes Stream-Event an den
/// Korrelations-Router geht. Reine Funktion → Ebene-1-beweisbar.
///
/// Früher: NUR teilnehmende Event-Typen (Typ-Filter). Das terminale Ergebnis-Event der letzten Transition
/// ist aber Auslöser KEINER Regel und daher KEIN teilnehmender Typ → es fiel durch den Filter → der Poll
/// konnte den Terminal-/Fan-out-Fall nicht heilen (Befund 2). Bislang fing das nur der Brute-Force-
/// <c>ProzessOffenIndex</c>-Backstop (weckt periodisch ALLE offenen Prozesse).
///
/// Jetzt ADDITIV: route ein Event auch dann, wenn seine <c>CorrelationId</c> zu einem OFFENEN Prozess
/// gehört (der Manager stempelt beim Feuern die Prozess-Korrelation ins Ziel-Event-Metadatum). Damit wird
/// die Terminal-Erkennung <b>event-getrieben und präzise</b> — nur Prozesse mit tatsächlich geändertem
/// Stream werden geweckt, statt periodisch alle. Der <c>ProzessOffenIndex</c>-Backstop <b>bleibt</b> als
/// Sicherheitsnetz für den fully-stalled Prozess OHNE Stream-Änderung (Auflage A2).
///
/// Kein Über-Wecken: ein fremdes Event (Client-Korrelation, nicht in <paramref name="offene"/>) wird NICHT
/// geroutet.
/// </summary>
public static class ProzessPollFilter
{
    public static bool SollRouten(
        Type eventTyp,
        string correlationId,
        IReadOnlySet<Type> teilnehmend,
        IReadOnlySet<Guid> offene)
        => teilnehmend.Contains(eventTyp)
           || (Guid.TryParse(correlationId, out var korr) && offene.Contains(korr));
}
