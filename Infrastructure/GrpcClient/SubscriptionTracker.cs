using Infrastructure.PubSub;
using Proto;
using Proto.Cluster;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Verwaltet PubSub-Subscriptions für eine gRPC-Verbindung.
/// 
/// KEIN ACTOR - normales Objekt!
/// 
/// Lebt im Scope der Connect()-Methode und wird im finally-Block aufgeräumt.
/// Viel einfacher als Actor-basierte Subscription-Verwaltung.
/// 
/// Lifecycle:
/// 1. Erstellt bei Connect()
/// 2. SubscribeAsync() bei SubscribeRequest vom Client
/// 3. UnsubscribeAsync() bei UnsubscribeRequest vom Client
/// 4. UnsubscribeAllAsync() im finally-Block bei Disconnect
/// </summary>
public class SubscriptionTracker : IAsyncDisposable
{
    private readonly Cluster _cluster;
    private readonly PID _proxyPid;
    private readonly string _subscriberId;
    
    // Nur EventType als Key - kein AggregateId-Filter (MVP: Client filtert selbst)
    private readonly Dictionary<Type, BrokerSubscription> _subscriptions = new();
    
    // Lock für Thread-Safety (mehrere async Operationen möglich)
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private bool _disposed = false;

    public SubscriptionTracker(Cluster cluster, PID proxyPid, string subscriberId)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _proxyPid = proxyPid ?? throw new ArgumentNullException(nameof(proxyPid));
        _subscriberId = subscriberId ?? throw new ArgumentNullException(nameof(subscriberId));
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
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Wildcard '*' not supported in MVP");
            return false;
        }

        var eventType = EventTypeResolver.ResolveEventType(eventTypeName);
        if (eventType == null)
        {
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Unknown event type: {eventTypeName}");
            return false;
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Bereits subscribed?
            if (_subscriptions.ContainsKey(eventType))
            {
                Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Already subscribed to {eventTypeName}");
                return true; // Kein Fehler, nur noop
            }

            // Neue Subscription erstellen
            var subscription = new BrokerSubscription(_cluster, _subscriberId, _proxyPid);
            await subscription.SubscribeAsync(eventType, ct);
            
            _subscriptions[eventType] = subscription;
            
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Subscribed to {eventTypeName}");
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
        var eventType = EventTypeResolver.ResolveEventType(eventTypeName);
        if (eventType == null)
        {
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Unknown event type: {eventTypeName}");
            return false;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (!_subscriptions.TryGetValue(eventType, out var subscription))
            {
                Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Not subscribed to {eventTypeName}");
                return false;
            }

            await subscription.UnsubscribeAsync(eventType, ct);
            _subscriptions.Remove(eventType);
            
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Unsubscribed from {eventTypeName}");
            return true;
        }
        finally
        {
            _lock.Release();
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

            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Unsubscribing from {_subscriptions.Count} types...");

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
                    Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Error unsubscribing from {eventType.Name}: {ex.Message}");
                }
            }
            
            _subscriptions.Clear();
            
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] All subscriptions cleared");

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
            Console.WriteLine($"[SubscriptionTracker-{_subscriberId}] Error during dispose: {ex.Message}");
        }
        finally
        {
            _lock.Dispose();
        }
    }
}