using Abstractions;
using Domain.Konto;
using Domain.Pipeline.Infrastructure;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Multi-Node Iter.2 — das PubSub-Tor: ein Signal reist SERIALISIERT über zwei Nodes.
///
/// Node B abonniert einen Signal-Typ (Broker-Subscription mit seiner lokalen Probe-PID). Node A
/// publiziert via <see cref="BrokerPublisher"/> ein <see cref="SignalEnvelope"/> für diesen Typ. Weil
/// Publisher (A) und Subscriber-Probe (B) auf verschiedenen Nodes leben und der Broker-Shard auf genau
/// EINEM Node aktiviert, quert mindestens ein Hop die Leitung — die PubSub-Nachrichten (Subscribe/PID,
/// Publish/SignalEnvelope, die Zustellung an die remote Probe-PID) MÜSSEN serialisieren.
///
/// Beweiskraft: Vor Iteration 2 gab es keinen Serializer für diese Typen → jeder cross-node-Hop
/// schlüge fehl. Grün = PubSub-Signale reisen cross-node.
///
/// Braucht Postgres (5432), Redis (6379), Consul (8500). Sequentiell.
/// </summary>
public class TwoNodePubSubSignalTests
{
    [Fact]
    public async Task Signal_reist_serialisiert_von_Node_A_zum_Subscriber_auf_Node_B()
    {
        var clusterName = "p8-pubsub-" + Guid.NewGuid().ToString("N")[..8];

        using var hostA = BuildNode(clusterName);
        using var hostB = BuildNode(clusterName);

        await hostA.StartAsync();
        await hostB.StartAsync();
        try
        {
            var systemA = hostA.Services.GetRequiredService<ActorSystem>();
            var systemB = hostB.Services.GetRequiredService<ActorSystem>();
            var clusterA = systemA.Cluster();
            var clusterB = systemB.Cluster();

            await WaitAsync(
                () => Task.FromResult(clusterA.MemberList.GetAllMembers().Length >= 2
                                   && clusterB.MemberList.GetAllMembers().Length >= 2),
                TimeSpan.FromSeconds(30),
                "Cluster hat nicht auf zwei Member konvergiert.");

            // Probe LOKAL auf Node B (wie der echte SignalReceiverActor) + Subscription mit ihrer PID.
            var signalEmpfangen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var probePid = systemB.Root.Spawn(Props.FromProducer(() => new SignalProbeActor(signalEmpfangen)));

            var subscriberId = "p8-pubsub-probe-" + Guid.NewGuid().ToString("N")[..8];
            var signalType = typeof(StateChangeViaKontoEroeffnet);
            var subscription = new BrokerSubscription(clusterB, subscriberId, probePid);
            await subscription.SubscribeAsync(signalType);

            // Subscription cluster-weit sichtbar? (Von Node A aus gefragt — quert die Leitung, wenn der
            // Shard fremd ist; beweist, dass das Subscribe+PID cross-node ankam.)
            await WaitAsync(
                async () => await SubscriberCount(clusterA, signalType, subscriberId) >= 1,
                TimeSpan.FromSeconds(20),
                "Subscription von Node B ist auf dem Shard nicht sichtbar geworden.");

            // Publish von Node A (das täte sonst der Aggregat-Actor nach dem Commit).
            await new BrokerPublisher(clusterA).PublishAsync(new SignalEnvelope
            {
                Signal = new StateChangeViaKontoEroeffnet(Guid.NewGuid(), 1),
            });

            var empfangen = await signalEmpfangen.Task.WaitAsync(TimeSpan.FromSeconds(20));
            empfangen.Should().BeTrue("die Probe auf Node B muss das cross-node publizierte Signal empfangen haben");
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
        }
    }

    private sealed class SignalProbeActor : IActor
    {
        private readonly TaskCompletionSource<bool> _empfangen;
        public SignalProbeActor(TaskCompletionSource<bool> empfangen) => _empfangen = empfangen;

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is SignalEnvelope)
                _empfangen.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private static async Task<int> SubscriberCount(Cluster cluster, Type signalType, string subscriberId)
    {
        var shard = BrokerIdentity.ShardFor(signalType, subscriberId);
        var resp = await cluster.RequestAsync<SubscriberCountResponse>(
            shard, new GetSubscriberCount(), CancellationToken.None);
        return resp?.Count ?? 0;
    }

    private static IHost BuildNode(string clusterName)
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "p8-ps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = clusterName;
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();

        return builder.Build();
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await condition()) return; } catch { /* noch nicht bereit */ }
            await Task.Delay(250);
        }
        throw new TimeoutException(message);
    }
}
