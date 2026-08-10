namespace Abstractions;

/// <summary>
/// Der Ops-/Read-Pfad auf die DLQ (Feature-Strom). Ergänzt den schreibenden <see cref="IDeadLetterSink"/>
/// um Beobachtung + Auflösung: nicht zustellbare Commands (technischer/Routing-Fehler, KlärungNötig einer
/// abgelehnten Kompensation) werden auflistbar, je Korrelation abfragbar und nach manueller Klärung
/// auflösbar (Eintrag entfernt).
///
/// Bewusst KEIN Auto-Replay: der Eintrag trägt nur Metadaten (Typ/Aggregat/Grund), nicht die Command-
/// Payload — ein automatischer Retry könnte auf dem OCC-Pfad bei verlorener Quittung doppelt wirken
/// (siehe <see cref="IDeadLetterSink"/>). „Replay" heißt hier: der Betreiber sieht den Verlust, klärt
/// ihn manuell (z. B. Command neu auslösen) und löst den Eintrag auf.
/// </summary>
public interface IDeadLetterReadStore
{
    /// <summary>Die neuesten toten Commands (nach Erfassungszeit absteigend), begrenzt auf <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<DeadLetter>> ListAsync(int limit, CancellationToken ct = default);

    /// <summary>Alle toten Commands einer Korrelation (z. B. eines Prozesses in KlärungNötig).</summary>
    Task<IReadOnlyList<DeadLetter>> ListByCorrelationAsync(string correlationId, CancellationToken ct = default);

    /// <summary>Ein einzelner Eintrag, oder null.</summary>
    Task<DeadLetter?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Anzahl offener DLQ-Einträge (für Monitoring/Alerting).</summary>
    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>Löst einen Eintrag nach manueller Klärung auf (entfernt ihn). true, wenn es ihn gab.</summary>
    Task<bool> ResolveAsync(Guid id, CancellationToken ct = default);
}
