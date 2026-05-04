namespace Infrastructure.PubSub;

using Abstractions;
using Infrastructure.PubSub.Messages;
using Proto;
using Proto.Cluster;

/// <summary>
/// Hilfsklasse für Subscriptions
/// </summary>
public class BrokerSubscription
{
    private readonly Cluster _cluster;
    private readonly string _subscriberId;
    private readonly PID _subscriberPid;
    private readonly List<Type> _subscribedTypes = new();

    public BrokerSubscription(Cluster cluster, string subscriberId, PID subscriberPid)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _subscriberId = subscriberId ?? throw new ArgumentNullException(nameof(subscriberId));
        _subscriberPid = subscriberPid ?? throw new ArgumentNullException(nameof(subscriberPid));
    }

    /// <summary>
    /// Abonniert einen Message-Typ
    /// </summary>
    public Task SubscribeAsync<T>(CancellationToken ct = default) 
        where T : IMessagePayload
    {
        return SubscribeAsync(typeof(T), ct);
    }

    /// <summary>
    /// Abonniert einen Message-Typ
    /// </summary>
    public async Task SubscribeAsync(Type messageType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        
        var shardIdentity = BrokerIdentity.ShardFor(messageType, _subscriberId);
        var message = new Subscribe(_subscriberId, _subscriberPid);
        
        await _cluster.RequestAsync<Ack>(shardIdentity, message, ct);
        
        if (!_subscribedTypes.Contains(messageType))
            _subscribedTypes.Add(messageType);
    }

    /// <summary>
    /// Abonniert mehrere Message-Typen
    /// </summary>
    public async Task SubscribeAsync(IEnumerable<Type> messageTypes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageTypes);
        
        foreach (var type in messageTypes)
        {
            await SubscribeAsync(type, ct);
        }
    }

    /// <summary>
    /// Beendet ein Abonnement
    /// </summary>
    public Task UnsubscribeAsync<T>(CancellationToken ct = default) 
        where T : IMessagePayload
    {
        return UnsubscribeAsync(typeof(T), ct);
    }

    /// <summary>
    /// Beendet ein Abonnement
    /// </summary>
    public async Task UnsubscribeAsync(Type messageType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        
        var shardIdentity = BrokerIdentity.ShardFor(messageType, _subscriberId);
        var message = new Unsubscribe(_subscriberId);
        
        await _cluster.RequestAsync<Ack>(shardIdentity, message, ct);
        
        _subscribedTypes.Remove(messageType);
    }

    /// <summary>
    /// Beendet alle Abonnements
    /// </summary>
    public async Task UnsubscribeAllAsync(CancellationToken ct = default)
    {
        foreach (var type in _subscribedTypes.ToList())
        {
            await UnsubscribeAsync(type, ct);
        }
    }

    /// <summary>
    /// Liste der abonnierten Typen
    /// </summary>
    public IReadOnlyList<Type> SubscribedTypes => _subscribedTypes;
}