namespace Infrastructure.PubSub;

using Abstractions;
using Proto.Cluster;

/// <summary>
/// Typsichere Berechnung von Cluster Identities für Message-Broker
/// </summary>
public static class BrokerIdentity
{
    public static ClusterIdentity Manager(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return ClusterIdentity.Create(messageType.Name, PubSubConfiguration.ManagerKind);
    }
    
    public static ClusterIdentity Manager<T>() where T : IMessagePayload 
        => Manager(typeof(T));

    public static ClusterIdentity Shard(Type messageType, int index)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        if (index < 0 || index >= PubSubConfiguration.ShardCount)
            throw new ArgumentOutOfRangeException(nameof(index));
            
        return ClusterIdentity.Create(
            $"{messageType.Name}_{index}", 
            PubSubConfiguration.ShardKind
        );
    }
    
    public static ClusterIdentity Shard<T>(int index) where T : IMessagePayload 
        => Shard(typeof(T), index);

    public static ClusterIdentity ShardFor(Type messageType, string subscriberId)
    {
        if (string.IsNullOrWhiteSpace(subscriberId))
            throw new ArgumentException("SubscriberId cannot be empty", nameof(subscriberId));
            
        return Shard(messageType, GetShardIndex(subscriberId));
    }
    
    public static ClusterIdentity ShardFor<T>(string subscriberId) where T : IMessagePayload 
        => ShardFor(typeof(T), subscriberId);

    public static ClusterIdentity[] AllShards(Type messageType)
    {
        return Enumerable.Range(0, PubSubConfiguration.ShardCount)
            .Select(i => Shard(messageType, i))
            .ToArray();
    }
    
    public static ClusterIdentity[] AllShards<T>() where T : IMessagePayload 
        => AllShards(typeof(T));

    public static int GetShardIndex(string subscriberId)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in subscriberId)
                hash = hash * 31 + c;
            return Math.Abs(hash) % PubSubConfiguration.ShardCount;
        }
    }

    public static (string TypeName, int Index) ParseShardIdentity(string identity)
    {
        var pos = identity.LastIndexOf('_');
        if (pos < 0)
            throw new ArgumentException($"Invalid shard identity: {identity}");
            
        return (identity[..pos], int.Parse(identity[(pos + 1)..]));
    }
}