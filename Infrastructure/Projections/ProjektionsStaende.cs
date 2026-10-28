using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Projections;

/// <summary>
/// Der Stand einer Projektion je gezeigter Id (Plan Pub/Sub-Neustrukturierung §2.7, Phase 6): wie weit die Projektion den Stream dieser Id
/// verarbeitet hat — aus ihrem Fortschritt, dem Guard je (Projektion, Stream), den der Batch-Leser in DERSELBEN Transaktion schreibt wie ihre
/// Wirkungen (<see cref="ProjectionCheckpoint"/>). Damit ist der Stand nie vor den Daten sichtbar, sinkt nie und beginnt mit einem Neuaufbau wieder
/// bei null. Eine Antwort trägt ihn je gezeigter Id; der Client prüft daran Lesen nach Schreiben und nimmt ihn als Version (FB-10: der Guard rückt
/// nur mit den Events vor, die die Projektion hört). Ersetzt den Redis-Versionsindex.
/// </summary>
public sealed class ProjektionsStaende(IDocumentStore store, ILogger<ProjektionsStaende>? logger = null) : IReadModelDepsReader
{
    public async Task<IReadOnlyList<AggregateMeta>?> ReadAsync(string subscriberId, IReadOnlyList<string> readModelIds)
    {
        var ids = readModelIds.Select(r => Guid.TryParse(r, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct()
            .Select(g => ProjectionCheckpoint.MakeId(subscriberId, g)).ToArray();
        if (ids.Length == 0) return null;
        try
        {
            await using var s = store.QuerySession();
            var staende = await s.LoadManyAsync<ProjectionCheckpoint>(CancellationToken.None, ids);
            return staende.Count == 0 ? null : staende.Select(cp => new AggregateMeta(cp.StreamId, "", cp.Version)).ToList();
        }
        catch (Exception ex)
        {
            // Nicht autoritativ: ohne Stand gilt die Antwort als frisch (wie zuvor bei einem Ausfall des Index).
            logger?.LogWarning(ex, "[Abfrage] Stand der Projektion {Projektion} nicht lesbar", subscriberId);
            return null;
        }
    }
}
