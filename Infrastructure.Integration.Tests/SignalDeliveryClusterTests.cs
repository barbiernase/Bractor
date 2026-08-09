using Abstractions;
using Core;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging.Abstractions;
using Infrastructure.PubSub.Extensions;
using Infrastructure.PubSub.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote.GrpcNet;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 2 (2b-cluster / 2c) — das Signal reist über den ECHTEN Broker.
///
/// Fährt einen Single-Node-Cluster hoch (Consul + BindToLocalhost + BrokerKinds),
/// abonniert per <see cref="SignalReceiverActor"/> das Signal, publiziert es über den
/// <see cref="BrokerPublisher"/> und prüft, dass der Adapter die Projektion faltet und
/// das Ergebnis per Co-Commit in Postgres landet.
///
/// Damit ist der gesamte Pull-Pfad end-to-end belegt: Signal → Broker → Receiver →
/// Adapter → Log-Read → Effekt+Marke gemeinsam gültig. Einziger simulierter Teil ist
/// die Event-Quelle (InMemory) — der Aggregat-Emit selbst kommt in der Host-Verdrahtung.
///
/// Braucht laufendes Consul (localhost:8500) und Postgres (localhost:5432).
/// </summary>
public class SignalDeliveryClusterTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public SignalDeliveryClusterTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Signal_reist_ueber_den_Broker_zum_Receiver_und_faltet_die_Projektion()
    {
        var system = new ActorSystem()
            .WithRemote(GrpcNetRemoteConfig.BindToLocalhost())
            .WithCluster(ClusterConfig.Setup(
                clusterName: "phase2-it-" + Guid.NewGuid().ToString("N")[..8],
                clusterProvider: new ConsulProvider(new ConsulProviderConfig()),
                identityLookup: new PartitionIdentityLookup()).WithBrokerKinds());

        var cluster = system.Cluster();
        await cluster.StartMemberAsync();
        try
        {
            var streamId = Guid.NewGuid();
            var projId = "it-recv-" + Guid.NewGuid().ToString("N");

            var es = EventStoreMit(streamId, count: 1);
            var store = new Domain.Infrastructure.ImagePairHistorieStore(_fx.Store);
            var adapter = new ProjectionAdapter(es, store, null, projId, Dispatch(store));
            var receiver = new SignalReceiver((sid, ct) => adapter.WakeAsync(sid, ct));
            var signalTypes = SignalReceiver.SignalTypesFor(new[] { typeof(ImagePairInspiziert) });

            system.Root.Spawn(Props.FromProducer(() =>
                new SignalReceiverActor(projId, signalTypes, receiver)));

            // Warten bis die Subscription auf dem Shard registriert ist.
            await WaitAsync(
                async () => await SubscriberCount(
                    cluster, typeof(StateChangeViaImagePairInspiziert), projId) >= 1,
                TimeSpan.FromSeconds(15),
                "Receiver hat sich nicht rechtzeitig subscribed.");

            // Signal veröffentlichen (das täte sonst der Aggregat-Actor nach dem Commit).
            var publisher = new BrokerPublisher(cluster);
            await publisher.PublishAsync(new SignalEnvelope
            {
                Signal = new StateChangeViaImagePairInspiziert(streamId, 1)
            });

            // Zustellung + Verarbeitung sind asynchron → abwarten.
            await WaitAsync(
                async () => (await store.GetByPairIdAsync(streamId))?.Eintraege.Count == 1,
                TimeSpan.FromSeconds(15),
                "Projektion wurde nach dem Signal nicht gefaltet.");

            (await store.GetByPairIdAsync(streamId))!.Eintraege.Should().HaveCount(1);
            (await store.LastProcessedVersionAsync(projId, streamId, default)).Should().Be(1);
        }
        finally
        {
            await cluster.ShutdownAsync();
        }
    }

    // ─── Helfer ───

    private static async Task<int> SubscriberCount(Cluster cluster, Type signalType, string subscriberId)
    {
        var shard = BrokerIdentity.ShardFor(signalType, subscriberId);
        var resp = await cluster.RequestAsync<SubscriberCountResponse>(
            shard, new GetSubscriberCount(), CancellationToken.None);
        return resp?.Count ?? 0;
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException(message);
    }

    // Seedet die Quell-Events über ECHTES Marten (kein In-Memory-Store mehr); der Adapter liest sie
    // danach über dasselbe Postgres. Der Cluster liefert hier nur das Signal.
    private MartenEventStore EventStoreMit(Guid streamId, int count)
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        es.AppendEventsAsync(
            streamId, 0,
            Enumerable.Range(0, count).Select(_ => (IEvent)new ImagePairInspiziert()).ToList())
          .GetAwaiter().GetResult();
        return es;
    }

    private static Func<EventEnvelope, ProjectionWriter, Task> Dispatch(IImagePairHistorieWriteStore effektStore)
    {
        var projection = new ImagePairHistorieProjection(effektStore);
        return (e, writer) => projection.DispatchAsync(e, writer, _ => Task.CompletedTask);
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadStream.");
    }
}
