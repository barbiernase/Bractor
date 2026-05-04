namespace Infrastructure.PubSub;

using Proto;

/// <summary>
/// Verwaltet Subscriber-Registrierungen für einen Shard
/// </summary>
public class SubscriberRegistry
{
    private readonly Dictionary<string, PID> _subscribers = new();

    public int Count => _subscribers.Count;
    
    public IEnumerable<PID> Subscribers => _subscribers.Values;

    public void Add(string subscriberId, PID subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriberId);
        ArgumentNullException.ThrowIfNull(subscriber);
        
        _subscribers[subscriberId] = subscriber;
    }

    public bool Remove(string subscriberId)
    {
        ArgumentNullException.ThrowIfNull(subscriberId);
        
        return _subscribers.Remove(subscriberId);
    }

    public bool Contains(string subscriberId) 
        => _subscribers.ContainsKey(subscriberId);

    /// <summary>
    /// Versucht einen Subscriber anhand seiner ID zu finden.
    /// Wird für Targeted Delivery verwendet.
    /// </summary>
    public bool TryGet(string subscriberId, out PID subscriber)
    {
        return _subscribers.TryGetValue(subscriberId, out subscriber!);
    }

    public void Clear() 
        => _subscribers.Clear();
}