using Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Projections;

/// <summary>
/// Liest ReadModel-AbhÃ¤ngigkeiten aus Redis.
/// 
/// Wird vom ProjectionQueryService nach einem Reader-Handler aufgerufen,
/// wenn der ReadContext getrackte IDs enthÃ¤lt und das [ProjectionReader]-Attribut
/// TrackDeps=true hat.
/// 
/// Redis-Datenmodell (geschrieben von ReadModelDepsWriter):
///   Key:   "rm:{subscriberId}:{readModelId}"
///   Field: "{AggregateType}:{AggregateId}" â†’ Version (int)
/// 
/// Ergebnis: Flache, deduplizierte Liste von AggregateMeta.
/// Bei mehreren ReadModel-IDs mit Ã¼berlappenden Aggregaten gewinnt die hÃ¶chste Version.
/// 
/// Graceful Degradation: Bei Redis-Ausfall â†’ leere Liste (Deps=null).
/// </summary>
public class ReadModelDepsReader : IReadModelDepsReader
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ReadModelDepsReader> _logger;

    public ReadModelDepsReader(
        IConnectionMultiplexer redis,
        ILogger<ReadModelDepsReader> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Liest Deps fÃ¼r eine oder mehrere ReadModel-IDs.
    /// 
    /// Ein Pipeline-Roundtrip fÃ¼r alle IDs.
    /// Ergebnis: Deduplizierte AggregateMeta-Liste (hÃ¶chste Version gewinnt).
    /// </summary>
    /// <param name="subscriberId">Der Subscriber/Projektion-Name (z.B. "lagerbestand-projection")</param>
    /// <param name="readModelIds">Die getrackten ReadModel-IDs aus dem ReadContext</param>
    /// <returns>Deduplizierte Deps oder null bei Fehler/leerem Ergebnis</returns>
    public async Task<IReadOnlyList<AggregateMeta>?> ReadAsync(
        string subscriberId,
        IReadOnlyList<string> readModelIds)
    {
        if (readModelIds.Count == 0)
            return null;

        try
        {
            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();

            // Pipeline: HGETALL fÃ¼r jede ReadModel-ID
            var tasks = new List<(string ReadModelId, Task<HashEntry[]> Task)>();

            foreach (var readModelId in readModelIds)
            {
                var key = ReadModelDepsWriter.ReadModelKey(subscriberId, readModelId);
                var task = batch.HashGetAllAsync(key);
                tasks.Add((readModelId, task));
            }

            batch.Execute();

            // Ergebnisse sammeln und deduplizieren
            var aggregateMap = new Dictionary<(string Type, Guid Id), int>();

            foreach (var (readModelId, task) in tasks)
            {
                var entries = await task;

                foreach (var entry in entries)
                {
                    var parsed = ParseField(entry.Name!, (int)entry.Value);
                    if (parsed == null) continue;

                    var key = (parsed.Value.AggregateType, parsed.Value.AggregateId);

                    // HÃ¶chste Version gewinnt
                    if (!aggregateMap.TryGetValue(key, out var existing) ||
                        parsed.Value.Version > existing)
                    {
                        aggregateMap[key] = parsed.Value.Version;
                    }
                }
            }

            if (aggregateMap.Count == 0)
                return null;

            // In AggregateMeta-Liste umwandeln
            var result = aggregateMap
                .Select(kvp => new AggregateMeta(kvp.Key.Id, kvp.Key.Type, kvp.Value))
                .ToList();

            _logger.LogDebug(
                "Read {Count} deps for [{SubscriberId}] from {IdCount} ReadModel(s)",
                result.Count, subscriberId, readModelIds.Count);

            return result;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable. Deps read returning null for [{SubscriberId}].",
                subscriberId);
            return null;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Redis timeout. Deps read returning null for [{SubscriberId}].",
                subscriberId);
            return null;
        }
    }

    /// <summary>
    /// Parst ein Redis Hash-Feld.
    /// Format: "{AggregateType}:{AggregateId}" â†’ Version
    /// </summary>
    private static (string AggregateType, Guid AggregateId, int Version)? ParseField(
        string fieldName, int version)
    {
        var colonIndex = fieldName.IndexOf(':');
        if (colonIndex <= 0 || colonIndex >= fieldName.Length - 1)
            return null;

        var aggregateType = fieldName[..colonIndex];
        var idString = fieldName[(colonIndex + 1)..];

        if (!Guid.TryParse(idString, out var aggregateId))
            return null;

        return (aggregateType, aggregateId, version);
    }
}