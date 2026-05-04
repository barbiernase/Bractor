using Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Persistence;

/// <summary>
/// Redis-basierter Version-Tracker für Aggregate.
/// 
/// Nicht-autoritativ: Die Wahrheit liegt im EventStore (Marten/PostgreSQL).
/// Redis ist ein schneller, verteilter Index für Stale Detection.
/// 
/// Datenmodell pro Aggregat (Redis Hash):
///   Key:   "agg:{aggregateId}"
///   Field: "v"    → Version (int)
///   Field: "t"    → AggregateType (string)
///   Field: "ts"   → UpdatedAt (Unix-Ticks, long)
/// 
/// Sekundärer Index (Redis Set):
///   Key:   "agg_idx:{aggregateType}"
///   Value: aggregateId (Guid als String)
///   → Wird nur bei Version 1 hinzugefügt (erstes Tracking)
///   → Ermöglicht: "Zeige alle Aggregate vom Typ Lagerartikel"
/// 
/// Graceful Degradation:
///   Alle Operationen fangen RedisConnectionException ab.
///   Bei Ausfall: GetAsync → null, TrackAsync → loggt Warning.
///   Der Command-Flow wird NICHT unterbrochen.
/// </summary>
public class RedisVersionTracker : IVersionTracker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisVersionTracker> _logger;

    // Redis Key-Prefixe
    private const string AggregateKeyPrefix = "agg:";
    private const string IndexKeyPrefix = "agg_idx:";

    // Hash-Felder
    private const string FieldVersion = "v";
    private const string FieldType = "t";
    private const string FieldTimestamp = "ts";

    public RedisVersionTracker(
        IConnectionMultiplexer redis,
        ILogger<RedisVersionTracker> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Aktualisiert die Version eines Aggregats in Redis.
    /// 
    /// Bei Version 1 (erste Erstellung) wird zusätzlich der sekundäre Index befüllt.
    /// Nutzt Redis-Pipeline für atomares Schreiben (Hash + optionaler SADD).
    /// </summary>
    public async Task TrackAsync(Guid aggregateId, string aggregateType, int newVersion)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = AggregateKey(aggregateId);
            var now = DateTimeOffset.UtcNow.Ticks;

            var batch = db.CreateBatch();

            // Hash setzen: Version, Typ, Timestamp
            var hashTask = batch.HashSetAsync(key, new[]
            {
                new HashEntry(FieldVersion, newVersion),
                new HashEntry(FieldType, aggregateType),
                new HashEntry(FieldTimestamp, now)
            });

            // Sekundärer Index: nur bei erster Erstellung (Version 1)
            Task? indexTask = null;
            if (newVersion == 1)
            {
                var indexKey = IndexKey(aggregateType);
                indexTask = batch.SetAddAsync(indexKey, aggregateId.ToString());
            }

            batch.Execute();

            await hashTask;
            if (indexTask != null)
                await indexTask;

            _logger.LogDebug(
                "Tracked {AggregateType}/{AggregateId} → Version {Version}",
                aggregateType, aggregateId, newVersion);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable. Version tracking skipped for {AggregateId}. " +
                "Will catch up on next successful command.",
                aggregateId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Redis timeout. Version tracking skipped for {AggregateId}.",
                aggregateId);
        }
    }

    /// <summary>
    /// Liest die Version eines Aggregats aus Redis.
    /// Gibt null zurück bei nicht existierendem Key oder Redis-Ausfall.
    /// </summary>
    public async Task<AggregateVersionInfo?> GetAsync(Guid aggregateId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = AggregateKey(aggregateId);

            var hash = await db.HashGetAllAsync(key);

            if (hash.Length == 0)
                return null;

            return ParseHash(hash);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable. GetAsync returning null for {AggregateId}.",
                aggregateId);
            return null;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Redis timeout. GetAsync returning null for {AggregateId}.",
                aggregateId);
            return null;
        }
    }

    /// <summary>
    /// Liest die Versionen mehrerer Aggregate in einem Roundtrip.
    /// Nutzt Redis-Pipeline für minimale Latenz.
    /// Nicht existierende Aggregate fehlen im Ergebnis (kein null-Eintrag).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, AggregateVersionInfo>> GetManyAsync(
        IReadOnlyList<Guid> aggregateIds)
    {
        var result = new Dictionary<Guid, AggregateVersionInfo>();

        if (aggregateIds.Count == 0)
            return result;

        try
        {
            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();

            // Alle Requests in einer Pipeline
            var tasks = new List<(Guid Id, Task<HashEntry[]> Task)>();
            foreach (var id in aggregateIds)
            {
                var key = AggregateKey(id);
                var task = batch.HashGetAllAsync(key);
                tasks.Add((id, task));
            }

            batch.Execute();

            // Ergebnisse sammeln
            foreach (var (id, task) in tasks)
            {
                var hash = await task;
                if (hash.Length > 0)
                {
                    var info = ParseHash(hash);
                    if (info != null)
                        result[id] = info;
                }
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable. GetManyAsync returning empty for {Count} aggregates.",
                aggregateIds.Count);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Redis timeout. GetManyAsync returning empty for {Count} aggregates.",
                aggregateIds.Count);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // Private Helpers
    // ═══════════════════════════════════════════════════════════

    private static string AggregateKey(Guid aggregateId)
        => $"{AggregateKeyPrefix}{aggregateId}";

    private static string IndexKey(string aggregateType)
        => $"{IndexKeyPrefix}{aggregateType}";

    private static AggregateVersionInfo? ParseHash(HashEntry[] hash)
    {
        int version = 0;
        string type = "";
        long ticks = 0;

        foreach (var entry in hash)
        {
            switch ((string)entry.Name!)
            {
                case FieldVersion:
                    version = (int)entry.Value;
                    break;
                case FieldType:
                    type = entry.Value!;
                    break;
                case FieldTimestamp:
                    ticks = (long)entry.Value;
                    break;
            }
        }

        if (string.IsNullOrEmpty(type))
            return null;

        return new AggregateVersionInfo(
            type,
            version,
            new DateTimeOffset(ticks, TimeSpan.Zero));
    }
}