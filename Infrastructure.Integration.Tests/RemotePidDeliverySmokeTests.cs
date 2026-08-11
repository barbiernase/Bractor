using Abstractions;
using Domain.Pipeline.Infrastructure;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Multi-Node Iter.2 — VORAB-SMOKE-TEST der einen offenen Annahme: eine **lokale PID** eines Actors
/// auf Node B, von Node A aus per <c>context.Send(pid, message)</c> angesprochen, stellt cross-node
/// SERIALISIERT zu (Proto-PID-Location-Transparency).
///
/// Das ist EXAKT der Mechanismus, auf dem der PubSub-Broker fußt: <c>BrokerShardActor.OnPublish</c>
/// stellt via <c>context.Send(subscriberPid, envelope)</c> zu — der Subscriber ist ein node-lokal
/// gespawnter <c>SignalReceiverActor</c>/<c>EventProxyActor</c>. Bevor Iteration 2 die PubSub-Typen
/// serialisierbar macht, muss diese Grundannahme stehen: sonst wäre der ganze „PID beibehalten,
/// nur serialisierbar machen"-Ansatz falsch.
///
/// Trick: <c>Wake</c> ist bereits ein Wire-Typ (Iteration 1) → der Test braucht KEINEN neuen
/// Produktionscode, er isoliert nur den Transport. Node B spawnt eine Probe lokal (Root.Spawn,
/// wie die echten Receiver); Node A sendet der Probe-PID ein <c>Wake</c>; die Probe muss es empfangen.
///
/// Braucht Postgres (5432), Redis (6379), Consul (8500). Sequentiell.
/// </summary>
public class RemotePidDeliverySmokeTests
{
    [Fact]
    public async Task Wire_Message_an_remote_PID_wird_cross_node_zugestellt()
    {
        var clusterName = "p8-remotepid-" + Guid.NewGuid().ToString("N")[..8];

        using var hostA = BuildNode(clusterName);
        using var hostB = BuildNode(clusterName);

        await hostA.StartAsync();
        await hostB.StartAsync();
        try
        {
            var systemA = hostA.Services.GetRequiredService<ActorSystem>();
            var systemB = hostB.Services.GetRequiredService<ActorSystem>();

            // Warten bis die Topologie konvergiert (Endpoints beidseitig offen).
            await WaitAsync(
                () => Task.FromResult(systemA.Cluster().MemberList.GetAllMembers().Length >= 2
                                   && systemB.Cluster().MemberList.GetAllMembers().Length >= 2),
                TimeSpan.FromSeconds(30),
                "Cluster hat nicht auf zwei Member konvergiert.");

            // Probe LOKAL auf Node B spawnen — genau wie die echten Receiver (Root.Spawn, keine Identity).
            var empfangen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var probePid = systemB.Root.Spawn(Props.FromProducer(() => new WakeProbeActor(empfangen)));

            // Die PID trägt die Adresse von Node B. Von Node A aus gesendet MUSS Proto sie über die
            // Leitung routen und das Wake dabei mit dem Wire-Serializer serialisieren.
            probePid.Address.Should().NotBe(systemA.Address, "die Probe lebt auf Node B, nicht A");

            systemA.Root.Send(probePid, new Wake(VomPoll: true));

            var vomPoll = await empfangen.Task.WaitAsync(TimeSpan.FromSeconds(15));
            vomPoll.Should().BeTrue("die Probe auf Node B muss das cross-node gesendete Wake empfangen haben");
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
        }
    }

    private sealed class WakeProbeActor : IActor
    {
        private readonly TaskCompletionSource<bool> _empfangen;
        public WakeProbeActor(TaskCompletionSource<bool> empfangen) => _empfangen = empfangen;

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Wake w)
                _empfangen.TrySetResult(w.VomPoll);
            return Task.CompletedTask;
        }
    }

    private static IHost BuildNode(string clusterName)
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "p8-rp-" + Guid.NewGuid().ToString("N"));
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
