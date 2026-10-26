namespace Infrastructure.PubSub.Extensions;

using Infrastructure.Lebenszyklus;
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
    public static Task ActivateBrokerAsync(
        this Cluster cluster,
        Type messageType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return new Bote(cluster).FrageMitWiederholungAsync<Ack>(BrokerIdentity.Manager(messageType), new Activate(),
            $"Broker {messageType.Name} aktivieren", ct);
    }

    /// <summary>Wie lange eine Anfrage an den Broker höchstens wiederholt wird, bevor sie laut scheitert.</summary>
    public static readonly TimeSpan WiederholungsFrist = TimeSpan.FromSeconds(60);

    /// <summary>Die Frist eines Versuchs.</summary>
    public static readonly TimeSpan VersuchsFrist = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Eine Frage an einen virtuellen Broker-Actor, wiederholt mit Backoff bis <see cref="WiederholungsFrist"/> (P1-3): ein gerade hart beendeter
    /// Knoten steht noch einige Sekunden in der Mitgliederliste (Consul: TTL 3 s), der Cluster legt Aktivierungen auf ihn, und der Versuch bleibt
    /// ohne Antwort; sobald die Mitgliedschaft stimmt, gelingt er. Jeder Versuch hat seine Frist (<see cref="VersuchsFrist"/>); bleibt jeder ohne
    /// Antwort, scheitert die Frage laut (<see cref="TimeoutException"/>).
    /// </summary>
    public static async Task<T> FrageMitWiederholungAsync<T>(this Bote bote, ClusterIdentity ziel, object nachricht, string was, CancellationToken ct)
        where T : class
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var pause = TimeSpan.FromMilliseconds(250);
        for (var versuch = 1; ; versuch++)
        {
            if (await bote.FrageAsync<T>(ziel, nachricht, VersuchsFrist, ct) is { } antwort)
            {
                if (versuch > 1) Console.WriteLine($"  ✓ {was} im {versuch}. Versuch");
                return antwort;
            }
            ct.ThrowIfCancellationRequested();
            if (System.Diagnostics.Stopwatch.GetElapsedTime(start) >= WiederholungsFrist)
                throw new TimeoutException($"{was}: keine Antwort in {WiederholungsFrist.TotalSeconds:0} s");
            Console.WriteLine($"  … {was}: keine Antwort — Mitgliedschaft noch nicht eingeschwungen, nächster Versuch in {pause.TotalMilliseconds:0} ms");
            await Task.Delay(pause, ct);
            if (pause < TimeSpan.FromSeconds(2)) pause *= 2;
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
