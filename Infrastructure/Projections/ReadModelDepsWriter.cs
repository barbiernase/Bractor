using Abstractions;
using Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Projections;

/// <summary>
/// Schreibt ReadModel-Abhängigkeiten nach Redis.
/// 
/// Wird vom SubscriberActorBase nach jedem Event-Handler aufgerufen.
/// Nimmt die gesammelten WriteResults und:
///   1. Löst Versionen auf (kausal vs. lookup)
///   2. Schreibt einen Redis Hash pro ReadModel
/// 
/// Redis-Datenmodell:
///   Key:   "rm:{subscriberId}:{readModelId}"
///   Field: "{AggregateType}:{AggregateId}" → Version (int)
/// 
/// Versionsauflösung:
///   Kausal: Track-Aggregat == Envelope-Aggregat → Version aus Envelope (kostenlos)
///   Lookup: Track-Aggregat != Envelope-Aggregat → Version aus Redis "agg:{id}"
/// 
/// Graceful Degradation:
///   Bei Redis-Ausfall wird geloggt, der Event-Flow wird NICHT unterbrochen.
/// </summary>
public class ReadModelDepsWriter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IVersionTracker _versionTracker;
    private readonly ILogger<ReadModelDepsWriter> _logger;

    // Redis Key-Prefix für ReadModel-Deps
    internal const string ReadModelKeyPrefix = "rm:";

    public ReadModelDepsWriter(
        IConnectionMultiplexer redis,
        IVersionTracker versionTracker,
        ILogger<ReadModelDepsWriter> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _versionTracker = versionTracker ?? throw new ArgumentNullException(nameof(versionTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Schreibt Deps für alle WriteResults in Redis.
    /// 
    /// Ein Aufruf pro Event-Verarbeitung im Subscriber-Actor.
    /// </summary>
    public async Task WriteAsync(
        string subscriberId,
        EventEnvelope envelope,
        IReadOnlyList<WriteResult> results,
        CancellationToken ct = default)
    {
        if (results.Count == 0)
            return;

        try
        {
            // ① Alle Tracks sammeln und Versionen auflösen
            var resolvedResults = await ResolveVersionsAsync(envelope, results);

            // ② Redis Batch-Write
            await WriteBatchAsync(subscriberId, resolvedResults);

            _logger.LogDebug(
                "Wrote deps for {Count} ReadModel(s) in [{SubscriberId}]",
                results.Count, subscriberId);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable. Deps write skipped for [{SubscriberId}]. " +
                "ReadModels will have stale deps until next event.",
                subscriberId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Redis timeout. Deps write skipped for [{SubscriberId}].",
                subscriberId);
        }
    }

    /// <summary>
    /// Löst Versionen für alle Tracks auf.
    /// 
    /// Kausal: Track.Id == envelope.AggregateId → Version aus Envelope
    /// Lookup: Track.Id != envelope.AggregateId → Version aus Redis VersionTracker
    /// 
    /// Ein Pipeline-Read für alle Lookups (ein Roundtrip).
    /// </summary>
    private async Task<List<ResolvedWriteResult>> ResolveVersionsAsync(
        EventEnvelope envelope,
        IReadOnlyList<WriteResult> results)
    {
        // Alle Lookup-IDs sammeln (nicht-kausale Refs)
        var lookupIds = new HashSet<Guid>();

        foreach (var result in results)
        {
            foreach (var track in result.Tracks)
            {
                if (track.Id != envelope.AggregateId)
                {
                    lookupIds.Add(track.Id);
                }
            }
        }

        // Lookup-Versionen in einem Roundtrip laden
        IReadOnlyDictionary<Guid, AggregateVersionInfo> lookupVersions =
            lookupIds.Count > 0
                ? await _versionTracker.GetManyAsync(lookupIds.ToList())
                : new Dictionary<Guid, AggregateVersionInfo>();

        // Resolved Results bauen
        var resolved = new List<ResolvedWriteResult>(results.Count);

        foreach (var result in results)
        {
            var deps = new List<ResolvedDep>(result.Tracks.Count);

            foreach (var track in result.Tracks)
            {
                int? version;

                if (track.Id == envelope.AggregateId)
                {
                    // Kausal: Version aus Envelope — kostenlos
                    version = envelope.AggregateVersion;
                }
                else if (lookupVersions.TryGetValue(track.Id, out var info))
                {
                    // Lookup: Version aus Redis
                    version = info.Version;
                }
                else
                {
                    // Aggregat nicht im VersionTracker (noch nie getrackt oder Redis-Ausfall)
                    _logger.LogWarning(
                        "Lookup-Ref for {AggregateType}/{AggregateId} not found in VersionTracker. " +
                        "Skipping dep for ReadModel '{ReadModelId}'.",
                        track.AggregateType.Name, track.Id, result.ReadModelId);
                    continue;
                }

                deps.Add(new ResolvedDep(
                    AggregateType: track.AggregateType.Name,
                    AggregateId: track.Id,
                    Version: version.Value));
            }

            resolved.Add(new ResolvedWriteResult(result.ReadModelId, deps));
        }

        return resolved;
    }

    /// <summary>
    /// Schreibt alle resolved Deps in Redis.
    /// Ein Batch = ein Pipeline-Roundtrip für alle ReadModels.
    /// </summary>
    private async Task WriteBatchAsync(
        string subscriberId,
        List<ResolvedWriteResult> results)
    {
        var db = _redis.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var result in results)
        {
            if (result.Deps.Count == 0)
                continue;

            var key = ReadModelKey(subscriberId, result.ReadModelId);
            var entries = new HashEntry[result.Deps.Count];

            for (int i = 0; i < result.Deps.Count; i++)
            {
                var dep = result.Deps[i];
                var field = $"{dep.AggregateType}:{dep.AggregateId}";
                entries[i] = new HashEntry(field, dep.Version);
            }

            tasks.Add(batch.HashSetAsync(key, entries));
        }

        batch.Execute();

        foreach (var task in tasks)
        {
            await task;
        }
    }

    // =========================================================================
    // KEY HELPERS
    // =========================================================================

    /// <summary>
    /// Redis-Key für ReadModel-Deps.
    /// Format: "rm:{subscriberId}:{readModelId}"
    /// </summary>
    public static string ReadModelKey(string subscriberId, string readModelId)
        => $"{ReadModelKeyPrefix}{subscriberId}:{readModelId}";
}

// =========================================================================
// INTERNE MODELLE
// =========================================================================

/// <summary>
/// WriteResult mit aufgelösten Versionen — bereit für Redis-Write.
/// </summary>
internal record ResolvedWriteResult(
    string ReadModelId,
    List<ResolvedDep> Deps);

/// <summary>
/// Einzelne aufgelöste Abhängigkeit mit konkreter Version.
/// </summary>
internal record ResolvedDep(
    string AggregateType,
    Guid AggregateId,
    int Version);