namespace Abstractions.Options;

/// <summary>
/// Konfiguration für das PubSub-System
/// </summary>
public class PubSubOptions
{
    /// <summary>
    /// Ob PubSub aktiviert ist
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Anzahl der Shards pro Message-Typ.
    /// Mehr Shards = bessere Parallelisierung, aber mehr Overhead.
    /// </summary>
    public int ShardCount { get; set; } = 3;
    
    /// <summary>
    /// ClusterKind-Name für den Broker-Manager
    /// </summary>
    public string ManagerKindName { get; set; } = "BrokerManager";
    
    /// <summary>
    /// ClusterKind-Name für die Broker-Shards
    /// </summary>
    public string ShardKindName { get; set; } = "BrokerShard";
    
    /// <summary>
    /// Ob Events automatisch nach dem Speichern im EventStore publiziert werden sollen
    /// </summary>
    public bool AutoPublishEvents { get; set; } = true;
}