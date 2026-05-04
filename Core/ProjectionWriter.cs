
namespace Core;

/// <summary>
/// Orchestriert Write-Scopes für Projektionen.
/// 
/// Pro Handler-Aufruf wird ein ProjectionWriter erstellt.
/// Der Handler kann writer.Execute() 0..N mal aufrufen (typisch: 1 mal).
/// Jeder Execute-Aufruf erzeugt einen WriteContext mit eigener ReadModelId.
/// 
/// Nach dem Handler-Aufruf liest das Framework die gesammelten Results aus
/// und schreibt die Deps in Redis (ab Phase 2).
/// 
/// Phase 1: Sammelt nur — kein Redis-Write.
/// </summary>
public class ProjectionWriter
{
    private readonly List<WriteResult> _results = new();

    /// <summary>
    /// Führt einen Write-Scope aus.
    /// 
    /// Erstellt einen WriteContext mit der readModelId,
    /// führt die Lambda aus (Entwickler macht DB-Write + ctx.Track()),
    /// und speichert das Ergebnis.
    /// </summary>
    /// <param name="readModelId">Fachliche ID des ReadModels</param>
    /// <param name="write">Lambda mit DB-Write und Track-Aufrufen</param>
    public async Task Execute(string readModelId, Func<WriteContext, Task> write)
    {
        ArgumentNullException.ThrowIfNull(readModelId);
        ArgumentNullException.ThrowIfNull(write);

        var ctx = new WriteContext(readModelId);
        await write(ctx);

        _results.Add(new WriteResult(readModelId, ctx.TrackedAggregates));
    }

    /// <summary>
    /// Wurden Execute-Aufrufe gemacht?
    /// </summary>
    public bool HasResults => _results.Count > 0;

    /// <summary>
    /// Gesammelte Write-Results (Framework-intern).
    /// Jeder Eintrag repräsentiert einen Execute-Aufruf mit ReadModelId + Tracks.
    /// </summary>
    public IReadOnlyList<WriteResult> GetResults() => _results;
}

/// <summary>
/// Ergebnis eines einzelnen writer.Execute()-Aufrufs.
/// </summary>
/// <param name="ReadModelId">Die ReadModel-ID dieses Scopes</param>
/// <param name="Tracks">Die registrierten Aggregate-Abhängigkeiten</param>
public record WriteResult(
    string ReadModelId,
    IReadOnlyList<TrackedAggregate> Tracks);