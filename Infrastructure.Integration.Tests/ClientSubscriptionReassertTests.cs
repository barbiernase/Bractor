using Domain.Konto;
using FluentAssertions;
using Infrastructure.GrpcClient;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Extensions;
using Infrastructure.PubSub.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote.GrpcNet;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Multi-Node Weg B — Selbst-Heilung der Client-Targeted-Subscription (das Poll-Äquivalent).
///
/// Die Subscriber-Liste eines Broker-Shards ist in-memory: rebalanciert der Shard (Node-Ausfall,
/// Topologie-Änderung), ist die Subscription weg — und der Client-Targeted-Pfad hat KEINEN
/// Poll-Backstop wie die Projektionen. <see cref="SubscriptionTracker.ReassertAllAsync"/> schließt die
/// Lücke: der socket-haltende Node meldet seine Subscriptions periodisch erneut an.
///
/// Echtes Shard-Rebalance im Test zu erzwingen ist unpraktisch; wir simulieren den Verlust
/// deterministisch durch ein direktes <see cref="Unsubscribe"/> am Shard und prüfen, dass ein
/// Re-Assert die Subscription wiederherstellt. Braucht Consul (8500). Sequentiell.
/// </summary>
public class ClientSubscriptionReassertTests
{
    [Fact]
    public async Task Re_Assert_stellt_eine_am_Shard_verlorene_Subscription_wieder_her()
    {
        var system = new ActorSystem()
            .WithRemote(GrpcNetRemoteConfig.BindToLocalhost())
            .WithCluster(ClusterConfig.Setup(
                clusterName: "reassert-it-" + Guid.NewGuid().ToString("N")[..8],
                clusterProvider: new ConsulProvider(new ConsulProviderConfig()),
                identityLookup: new PartitionIdentityLookup()).WithBrokerKinds());

        var cluster = system.Cluster();
        await cluster.StartMemberAsync();
        try
        {
            var eventType = typeof(KontoEroeffnet);
            var subscriberId = "reassert-probe-" + Guid.NewGuid().ToString("N")[..8];
            var probePid = system.Root.Spawn(Props.FromProducer(() => new NoopProbe()));

            var tracker = new SubscriptionTracker(cluster, probePid, subscriberId);
            (await tracker.SubscribeAsync("KontoEroeffnet")).Should().BeTrue("Event-Typ muss auflösbar sein");

            // 1) Subscription ist am Shard sichtbar.
            await WaitAsync(
                async () => await SubscriberCount(cluster, eventType, subscriberId) == 1,
                TimeSpan.FromSeconds(15),
                "Subscribe ist am Shard nicht sichtbar geworden.");

            // 2) Shard-Verlust simulieren: direkt am Shard abmelden (== Registry nach Rebalance leer).
            var shard = BrokerIdentity.ShardFor(eventType, subscriberId);
            await cluster.RequestAsync<Ack>(shard, new Unsubscribe(subscriberId), CancellationToken.None);
            await WaitAsync(
                async () => await SubscriberCount(cluster, eventType, subscriberId) == 0,
                TimeSpan.FromSeconds(15),
                "Simuliertes Unsubscribe ist nicht wirksam geworden.");

            // 3) Re-Assert heilt: die Subscription ist wieder da.
            await tracker.ReassertAllAsync();
            await WaitAsync(
                async () => await SubscriberCount(cluster, eventType, subscriberId) == 1,
                TimeSpan.FromSeconds(15),
                "Re-Assert hat die verlorene Subscription nicht wiederhergestellt.");
        }
        finally
        {
            await cluster.ShutdownAsync();
        }
    }

    private sealed class NoopProbe : IActor
    {
        public Task ReceiveAsync(IContext context) => Task.CompletedTask;
    }

    private static async Task<int> SubscriberCount(Cluster cluster, Type eventType, string subscriberId)
    {
        var shard = BrokerIdentity.ShardFor(eventType, subscriberId);
        var resp = await cluster.RequestAsync<SubscriberCountResponse>(
            shard, new GetSubscriberCount(), CancellationToken.None);
        return resp?.Count ?? 0;
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await condition()) return; } catch { /* noch nicht bereit */ }
            await Task.Delay(150);
        }
        throw new TimeoutException(message);
    }
}
