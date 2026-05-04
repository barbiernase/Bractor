namespace Abstractions.Options;

/// <summary>
/// Konfiguration für den Proto.Actor Cluster
/// </summary>
public class ClusterOptions
{
    /// <summary>
    /// Name des Clusters. Alle Nodes mit demselben Namen bilden einen Cluster.
    /// </summary>
    public string ClusterName { get; set; } = "cqrs-cluster";
    
    /// <summary>
    /// Host-Adresse die anderen Cluster-Mitgliedern mitgeteilt wird.
    /// </summary>
    public string AdvertisedHost { get; set; } = "localhost";
    
    /// <summary>
    /// Port für gRPC-Kommunikation. 0 = automatische Zuweisung.
    /// </summary>
    public int Port { get; set; } = 0;
    
    /// <summary>
    /// Typ des Cluster-Providers (Consul, Kubernetes, etc.)
    /// </summary>
    public ClusterProviderType ProviderType { get; set; } = ClusterProviderType.Consul;
    
    /// <summary>
    /// Adresse des Consul-Servers (nur relevant wenn ProviderType = Consul)
    /// </summary>
    public string ConsulAddress { get; set; } = "localhost:8500";
}

/// <summary>
/// Unterstützte Cluster-Provider
/// </summary>
public enum ClusterProviderType
{
    /// <summary>
    /// HashiCorp Consul für Service Discovery
    /// </summary>
    Consul,
    
    /// <summary>
    /// Kubernetes-native Service Discovery
    /// </summary>
    Kubernetes,
    
    /// <summary>
    /// Einzelner Node ohne Clustering (für Entwicklung/Tests)
    /// </summary>
    SingleNode
}