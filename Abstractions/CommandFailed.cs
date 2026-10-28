namespace Abstractions;

/// <summary>
/// Event das publiziert wird wenn ein Command fehlschlägt.
/// 
/// Geht mit ZielSitzung nur an die Sitzung, die den Command gesendet hat (über den Verteiler ihres Knotens).
/// Der Client matched über die CorrelationId im EventEnvelope.
/// 
/// Implementiert ITransientEvent (nicht IEvent direkt), weil CommandFailed
/// nie im EventStore persistiert wird – es geht nur an die Sitzung.
/// </summary>
public record CommandFailed(
    /// <summary>
    /// Name des Command-Typs der fehlgeschlagen ist (z.B. "BucheWareneingang")
    /// </summary>
    string CommandType,
    
    /// <summary>
    /// Fehlermeldung (z.B. "Bestand nicht ausreichend")
    /// </summary>
    string Reason,
    
    /// <summary>
    /// Optional: AggregateId als String (für Debugging)
    /// </summary>
    string? AggregateId = null
) : ITransientEvent;