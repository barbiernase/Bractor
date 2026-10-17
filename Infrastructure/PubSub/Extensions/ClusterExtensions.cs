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
        await MitWiederholungAsync(c => cluster.RequestAsync<Ack>(managerIdentity, new Activate(), c), $"Broker {messageType.Name} aktivieren", ct);
    }

    /// <summary>Wie lange eine Anfrage an den Broker höchstens wiederholt wird, bevor sie laut scheitert.</summary>
    public static readonly TimeSpan WiederholungsFrist = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Eine Anfrage an einen virtuellen Broker-Actor, wiederholt mit Backoff bis <see cref="WiederholungsFrist"/> (P1-3). Ein gerade hart beendeter
    /// Knoten steht noch einige Sekunden in der Mitgliederliste (Consul: TTL 3 s), der Cluster legt Aktivierungen auf ihn, und die Anfrage läuft in
    /// den Timeout; sobald die Mitgliedschaft stimmt, gelingt sie. Auch für einen Knoten, der in einem Cluster gerade gestorben ist.
    /// </summary>
    public static async Task MitWiederholungAsync(Func<CancellationToken, Task> anfrage, string was, CancellationToken ct)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var pause = TimeSpan.FromMilliseconds(250);
        for (var versuch = 1; ; versuch++)
        {
            try
            {
                await anfrage(ct);
                if (versuch > 1) Console.WriteLine($"  ✓ {was} im {versuch}. Versuch");
                return;
            }
            catch (Exception ex) when (ex is TimeoutException or DeadLetterException && !ct.IsCancellationRequested
                                       && System.Diagnostics.Stopwatch.GetElapsedTime(start) < WiederholungsFrist)
            {
                Console.WriteLine($"  … {was}: {ex.GetType().Name} — Mitgliedschaft noch nicht eingeschwungen, nächster Versuch in {pause.TotalMilliseconds:0} ms");
                await Task.Delay(pause, ct);
                if (pause < TimeSpan.FromSeconds(2)) pause *= 2;
            }
        }
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