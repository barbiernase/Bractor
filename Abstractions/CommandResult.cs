namespace Abstractions;

/// <summary>
/// Die Antwort des Aggregats auf einen Command: nur der <b>Ausgang</b> (gewirkt, abgelehnt,
/// technischer Fehler), die <b>Versionen</b> und die <b>Ablehnung</b>. Die Events, die der Command geschrieben hat, stehen im Log
/// (<see cref="ErsteVersion"/> … <see cref="NewVersion"/>) — wer sie zeigen will, liest sie dort (die Wahrheit ist der Log, Invariante 1).
/// </summary>
public class CommandResult : IWireMessage
{
    /// <summary>Gewirkt (auch als idempotente Wiederholung eines schon gewirkten Commands).</summary>
    public bool Success { get; set; }

    /// <summary>Technischer Fehler, wenn weder <see cref="Success"/> noch <see cref="Abgelehnt"/> — dann ist die Wirkung unbekannt.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Version des Aggregats nach Verarbeitung des Commands — die erwartete Version des nächsten Client-Commands.
    /// Bleibt 0 bei Fehlern, bei denen der Stand unbekannt ist.
    /// </summary>
    public int NewVersion { get; set; }

    /// <summary>
    /// Version des ersten Events, das DIESER Aufruf geschrieben hat — seine Events stehen im Stream genau in <c>ErsteVersion … NewVersion</c>.
    /// 0, wenn der Aufruf kein Event geschrieben hat (Noop, Ablehnung, als Wiederholung erkannt, Fehler). Ein Hinweis für Leser, die die Events
    /// im Log suchen (JSON-Eingang); die Wahrheit bleibt der Log, „gewirkt/abgelehnt“ hält die Inbox-Tabelle.
    /// </summary>
    public int ErsteVersion { get; set; }

    /// <summary>
    /// Typisiertes Ablehnungs-Event bei Domänen-Ablehnungen.
    ///
    /// Gesetzt wenn der Decider ein ITransientEvent yieldet (z.B. BestandNichtAusreichend).
    /// Null bei Erfolg oder bei technischen Fehlern (dann ist ErrorMessage gesetzt).
    ///
    /// Ermöglicht dem Client pattern matching:
    ///   case BestandNichtAusreichend r → ShowError($"Nur {r.Verfuegbar} von {r.Angefordert}")
    ///   case ArtikelNichtGefunden    → ShowError("Artikel existiert nicht")
    /// </summary>
    public IEvent? RejectionEvent { get; set; }

    /// <summary>
    /// Fachlich abgelehnt — auch als idempotente Wiederholung einer schon abgelehnten CommandId (dann ohne
    /// <see cref="RejectionEvent"/>). Trennt die entschiedene Ablehnung vom technischen Fehler (<see cref="ErrorMessage"/>
    /// ohne <see cref="Abgelehnt"/>), dessen Wirkung unbekannt ist (<c>EmitAusgang</c>).
    /// </summary>
    public bool Abgelehnt { get; set; }
}
