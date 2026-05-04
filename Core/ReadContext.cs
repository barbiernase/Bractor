/*namespace Infrastructure.Projections;

/// <summary>
/// Kontext für einen Query-Aufruf.
/// 
/// Wird vom ProjectionQueryService erstellt und an den Reader-Handler übergeben.
/// Der Entwickler registriert ReadModel-IDs via Track() — 
/// das Framework liest die gesammelten IDs nach dem Handler und holt Deps aus Redis.
/// 
/// Gegenstück zu WriteContext (Write-Seite).
/// WriteContext.Track&lt;T&gt;() registriert Aggregate-Abhängigkeiten beim Schreiben.
/// ReadContext.Track() markiert ReadModel-IDs für die Deps beim Lesen geladen werden.
/// </summary>
public class ReadContext
{
    private readonly List<string> _trackedIds = new();

    /// <summary>
    /// Markiert eine ReadModel-ID.
    /// "Für dieses ReadModel sollen Deps aus Redis geladen werden."
    /// 
    /// Typisch: ctx.Track(query.ArtikelId.ToString()) — 
    /// gleiche ID wie in writer.Execute(readModelId, ...) auf der Write-Seite.
    /// </summary>
    public void Track(string readModelId)
    {
        ArgumentNullException.ThrowIfNull(readModelId);
        _trackedIds.Add(readModelId);
    }

    /// <summary>
    /// Wurden Track-Aufrufe gemacht?
    /// Wenn false: Kein Redis-Read nötig, Deps bleiben null.
    /// </summary>
    internal bool HasTrackedIds => _trackedIds.Count > 0;

    /// <summary>
    /// Gesammelte ReadModel-IDs (Framework-intern).
    /// </summary>
    internal IReadOnlyList<string> TrackedIds => _trackedIds;
}*/