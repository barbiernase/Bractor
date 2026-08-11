using Infrastructure.Mapping;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto;
using Proto.Cluster;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Verwaltet PubSub-Subscriptions für eine gRPC-Verbindung.
/// 
/// KEIN ACTOR - normales Objekt!
/// 
/// Lebt im Scope der Connect()-Methode und wird im finally-Block aufgeräumt.
/// Nutzt MessageTypeMapping.Resolve() für die Typ-Auflösung.
/// </summary>
public class SubscriptionTracker : IAsyncDisposable
{
    private readonly Cluster _cluster;
    private readonly PID _proxyPid;
    private readonly string _subscriberId;
    private readonly ILogger _logger;

    // Nur EventType als Key - kein AggregateId-Filter (MVP: Client filtert selbst)
    private readonly Dictionary<Type, BrokerSubscription> _subscriptions = new();

    // Lock für Thread-Safety (mehrere async Operationen möglich)
    private readonly SemaphoreSlim _lock = new(1, 1);

    private bool _disposed = false;

    public SubscriptionTracker(Cluster cluster, PID proxyPid, string subscriberId, ILogger? logger = null)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _proxyPid = proxyPid ?? throw new ArgumentNullException(nameof(proxyPid));
        _subscriberId = subscriberId ?? throw new ArgumentNullException(nameof(subscriberId));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Subscribed für einen Event-Typ.
    /// </summary>
    /// <param name="eventTypeName">Name des Event-Typs (z.B. "LagerartikelErstellt") oder "*" für alle</param>
    /// <returns>True wenn erfolgreich subscribed, False wenn Typ unbekannt oder bereits subscribed</returns>
    public async Task<bool> SubscribeAsync(string eventTypeName, CancellationToken ct = default)
    {
        // "*" = Wildcard wird im MVP nicht unterstützt
        if (string.IsNullOrEmpty(eventTypeName) || eventTypeName == "*")
        {
            _logger.LogWarning("[SubscriptionTracker-{Subscriber}] Wildcard '*' not supported in MVP", _subscriberId);
            return false;
        }

        var (resolvedType, category) = MessageTypeMapping.Resolve(eventTypeName);
        if (resolvedType == null || category != MessageCategory.Event)
        {
            _logger.LogWarning("[SubscriptionTracker-{Subscriber}] Unknown or non-event type: {EventType}", _subscriberId, eventTypeName);
            return false;
        }
        var eventType = resolvedType;

        await _lock.WaitAsync(ct);
        try
        {
            // Bereits subscribed?
            if (_subscriptions.ContainsKey(eventType))
            {
                _logger.LogDebug("[SubscriptionTracker-{Subscriber}] Already subscribed to {EventType}", _subscriberId, eventTypeName);
                return true; // Kein Fehler, nur noop
            }

            // Neue Subscription erstellen
            var subscription = new BrokerSubscription(_cluster, _subscriberId, _proxyPid);
            await subscription.SubscribeAsync(eventType, ct);
            
            _subscriptions[eventType] = subscription;
            
            _logger.LogDebug("[SubscriptionTracker-{Subscriber}] Subscribed to {EventType}", _subscriberId, eventTypeName);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Beendet Subscription für einen Event-Typ.
    /// </summary>
    /// <param name="eventTypeName">Name des Event-Typs</param>
    /// <returns>True wenn erfolgreich unsubscribed, False wenn nicht subscribed war</returns>
    public async Task<bool> UnsubscribeAsync(string eventTypeName, CancellationToken ct = default)
    {
        var (resolvedType, _) = MessageTypeMapping.Resolve(eventTypeName);
        if (resolvedType == null)
        {
            _logger.LogWarning("[SubscriptionTracker-{Subscriber}] Unknown event type: {EventType}", _subscriberId, eventTypeName);
            return false;
        }
        var eventType = resolvedType;

        await _lock.WaitAsync(ct);
        try
        {
            if (!_subscriptions.TryGetValue(eventType, out var subscription))
            {
                _logger.LogDebug("[SubscriptionTracker-{Subscriber}] Not subscribed to {EventType}", _subscriberId, eventTypeName);
                return false;
            }

            await subscription.UnsubscribeAsync(eventType, ct);
            _subscriptions.Remove(eventType);
            
            _logger.LogDebug("[SubscriptionTracker-{Subscriber}] Unsubscribed from {EventType}", _subscriberId, eventTypeName);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sendet die Subscription für ALLE getrackten Typen ERNEUT an die Shards — die Selbst-Heilung des
    /// Client-Targeted-Pfads (das Poll-Äquivalent, das durable Projektionen längst haben).
    ///
    /// WARUM nötig (Multi-Node): die Subscriber-Liste eines Broker-Shards ist in-memory. Rebalanciert der
    /// Shard-Actor (Node-Ausfall, Topologie-Änderung), ist sie weg — und für den Client-Targeted-Pfad gibt
    /// es KEINEN Poll-Backstop wie bei den Projektionen. Ein periodisches Re-Assert von HIER (dem
    /// socket-haltenden Node) stellt sie wieder her; die Ziel-PID ist stets die aktuelle Proxy-PID.
    /// Idempotent am Shard (Dictionary-Overwrite). BEWUSST kein Dead-Letter: Targeted-Delivery ist
    /// verlierbar (Invariante 6) — es geht nur um Selbst-Heilung, nicht um Wiederzustellung.
    ///
    /// Der Lock wird nur für den Snapshot gehalten, NICHT über die Netzwerk-Sends (die dürfen einen
    /// gleichzeitigen Subscribe/Unsubscribe nicht blockieren).
    /// </summary>
    public async Task ReassertAllAsync(CancellationToken ct = default)
    {
        List<KeyValuePair<Type, BrokerSubscription>> snapshot;
        await _lock.WaitAsync(ct);
        try
        {
            if (_disposed || _subscriptions.Count == 0) return;
            snapshot = _subscriptions.ToList();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var (eventType, subscription) in snapshot)
        {
            try
            {
                await subscription.SubscribeAsync(eventType, ct);
            }
            catch (Exception ex)
            {
                // Best-effort: ein fehlgeschlagenes Re-Assert heilt der nächste Durchlauf.
                _logger.LogDebug(ex,
                    "[SubscriptionTracker-{Subscriber}] Re-Assert {Type} fehlgeschlagen — nächster Durchlauf heilt",
                    _subscriberId, eventType.Name);
            }
        }
    }

    /// <summary>
    /// Beendet ALLE Subscriptions. Wird im finally-Block aufgerufen.
    /// </summary>
    public async Task UnsubscribeAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_subscriptions.Count == 0)
                return;

            _logger.LogDebug("[SubscriptionTracker-{Subscriber}] Unsubscribing from {Count} types", _subscriberId, _subscriptions.Count);

            var errors = new List<Exception>();
            
            foreach (var (eventType, subscription) in _subscriptions)
            {
                try
                {
                    await subscription.UnsubscribeAsync(eventType);
                }
                catch (Exception ex)
                {
                    // Fehler sammeln aber weitermachen
                    errors.Add(ex);
                    _logger.LogWarning(ex, "[SubscriptionTracker-{Subscriber}] Error unsubscribing from {EventType}", _subscriberId, eventType.Name);
                }
            }
            
            _subscriptions.Clear();
            
            _logger.LogDebug("[SubscriptionTracker-{Subscriber}] All subscriptions cleared", _subscriberId);

            // Wenn Fehler aufgetreten sind, als AggregateException werfen
            if (errors.Count > 0)
            {
                throw new AggregateException("Errors during unsubscribe", errors);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Anzahl aktiver Subscriptions.
    /// </summary>
    public int SubscriptionCount
    {
        get
        {
            _lock.Wait();
            try
            {
                return _subscriptions.Count;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// Liste der subscribed Event-Typen (für Debugging).
    /// </summary>
    public IReadOnlyList<string> SubscribedTypes
    {
        get
        {
            _lock.Wait();
            try
            {
                return _subscriptions.Keys.Select(t => t.Name).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// IAsyncDisposable Implementation - ruft UnsubscribeAllAsync auf.
    /// Ermöglicht: await using var tracker = new SubscriptionTracker(...);
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        try
        {
            await UnsubscribeAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SubscriptionTracker-{Subscriber}] Error during dispose", _subscriberId);
        }
        finally
        {
            _lock.Dispose();
        }
    }
}