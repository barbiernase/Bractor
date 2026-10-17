namespace Abstractions;

public class CommandResult : IWireMessage
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid AggregateId { get; set; }
    
    /// <summary>
    /// Version des Aggregats nach Verarbeitung des Commands.
    /// Wird vom Actor nach Apply gesetzt.
    /// Ermöglicht dem Client, die aktuelle Version zu kennen ohne Redis abzufragen.
    /// Bleibt 0 bei Fehlern oder wenn kein Event erzeugt wurde.
    /// </summary>
    public int NewVersion { get; set; }

    /// <summary>
    /// Version des ersten Events, das DIESER Aufruf geschrieben hat (Wirkung, Noop- oder Ablehnungs-Marke) — sein Ausgang steht im Stream genau
    /// in <c>ErsteVersion … NewVersion</c>. 0, wenn der Aufruf nichts geschrieben hat (als Wiederholung erkannt, Fehler): dann ist unbekannt, wo
    /// der Ausgang steht. Ein Hinweis für Leser, die den Ausgang im Log suchen (Prozess-Fold); die Wahrheit bleibt der Log.
    /// </summary>
    public int ErsteVersion { get; set; }

    public IReadOnlyList<IEvent> Events { get; set; } = new List<IEvent>();

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