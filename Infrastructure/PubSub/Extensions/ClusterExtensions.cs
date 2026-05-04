namespace Infrastructure.PubSub.Extensions;

using Infrastructure.PubSub.Actors;
using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

public static class ClusterExtensions
{
    /// <summary>
    /// Registriert die Broker-Kinds im Cluster
    /// </summary>
    public static ClusterConfig WithBrokerKinds(
        this ClusterConfig config, 
        ILoggerFactory? loggerFactory = null)
    {
        var shardLogger = loggerFactory?.CreateLogger<BrokerShardActor>();
        var managerLogger = loggerFactory?.CreateLogger<BrokerManagerActor>();
        
        return config
            .WithClusterKind(
                PubSubConfiguration.ShardKind,
                Props.FromProducer(() => new BrokerShardActor(shardLogger)))
            .WithClusterKind(
                PubSubConfiguration.ManagerKind,
                Props.FromProducer(() => new BrokerManagerActor(managerLogger)));
    }

    /// <summary>
    /// Aktiviert den Broker für einen Message-Typ
    /// </summary>
    public static async Task ActivateBrokerAsync(
        this Cluster cluster, 
        Type messageType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        
        var managerIdentity = BrokerIdentity.Manager(messageType);
        await cluster.RequestAsync<Ack>(managerIdentity, new Activate(), ct);
    }

    /// <summary>
    /// Aktiviert den Broker für einen Message-Typ
    /// </summary>
    public static Task ActivateBrokerAsync<T>(
        this Cluster cluster,
        CancellationToken ct = default)
    {
        return cluster.ActivateBrokerAsync(typeof(T), ct);
    }

    /// <summary>
    /// Aktiviert Broker für mehrere Message-Typen
    /// </summary>
    public static async Task ActivateBrokersAsync(
        this Cluster cluster,
        IEnumerable<Type> messageTypes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageTypes);
        
        foreach (var type in messageTypes)
        {
            await cluster.ActivateBrokerAsync(type, ct);
        }
    }
}