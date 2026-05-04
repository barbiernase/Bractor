namespace Abstractions;

public class CommandResult
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
}