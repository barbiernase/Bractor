namespace Abstractions;

/// <summary>
/// Envelope-Vertrag für Events (im Gegensatz zu Commands/Queries).
/// Fügt IAggregateEnvelope die Event-Version hinzu — die Position des
/// Events in seinem Stream.
///
/// WARUM ein eigenes Interface statt AggregateVersion auf IAggregateEnvelope:
/// Commands haben KEINE Event-Version (sie tragen nur ExpectedVersion). Ein
/// gemeinsames AggregateVersion auf IAggregateEnvelope würde Commands einen
/// bedeutungslosen Wert aufzwingen. IEventEnvelope trennt sauber: nur echte
/// Events tragen eine AggregateVersion.
///
/// WOFÜR das Framework es braucht (ab Phase 2):
///   - Der Adapter liest AggregateVersion für den Pre-Dispatch-Guard und die
///     Fortschrittsmarke — ohne auf den konkreten EventEnvelope casten zu müssen.
///   - Append-artige Projektionen nutzen (AggregateId, AggregateVersion) als
///     deterministischen Dedup-Schlüssel.
///   - Deterministische Prozess-/Command-Identitäten (spätere Phasen) leiten
///     sich aus (StreamId, AggregateVersion) ab.
///
/// EventEnvelope besitzt AggregateVersion bereits als konkrete Property; es muss
/// dieses Interface nur zusätzlich deklarieren (additiv, kein Verhaltenswechsel).
/// Die Handle-Signaturen der Projektionen bleiben in Phase 0 unverändert
/// (IAggregateEnvelope) und wandern erst in Phase 2/3 auf IEventEnvelope.
/// </summary>
public interface IEventEnvelope : IAggregateEnvelope
{
    /// <summary>
    /// Position dieses Events in seinem Stream (0-basiert, monoton, lückenlos).
    /// Anker für Reihenfolge, Guard, Marke und deterministische Identitäten.
    /// </summary>
    int AggregateVersion { get; }

    /// <summary>
    /// Unter-Position innerhalb EINER gespeicherten Stream-Position (Default 0). Fast immer 0:
    /// ein gespeichertes Event → ein materialisiertes Event. Nur bei einem Upcasting-<b>Split</b>
    /// (eine frühere Gestalt wird beim Lesen zu mehreren Events) tragen die Geschwister dieselbe
    /// <see cref="AggregateVersion"/>, aber aufsteigende <c>SubIndex</c> 0, 1, … — so bleibt der
    /// Dedup-/Exactly-once-Schlüssel append-artiger Projektionen
    /// <c>(AggregateId, AggregateVersion, SubIndex)</c> eindeutig. Die Versions-Zählung selbst bleibt
    /// an den gespeicherten Positionen (der SubIndex zählt NICHT hoch).
    /// </summary>
    int SubIndex { get; }
}
