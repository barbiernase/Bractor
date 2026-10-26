using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;
using Infrastructure.Pruefstand.BatchKonsum;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

namespace Infrastructure.Pruefstand.Lebenszyklus;

/// <summary>
/// Die Befunde der Actor-Praxis (Plan Pub/Sub-Neustrukturierung §1.7) an den echten Actors in einem Cluster im Speicher (Mitgliedschaft
/// in-memory, ohne Store — Log und Tracker sind die Seams des Batch-Konsums):
/// <list type="bullet">
/// <item>der Batch-Leser gibt seinen Bereich beim Stopp nicht frei, solange der Durchlauf ihn nutzt (vorher: freigegeben im <c>Stopped</c>);</item>
/// <item>der Stream-Adapter gibt seinen Bereich nach der Passivierung frei (vorher: nie) und beantwortet eine gescheiterte Weckung ehrlich mit
///   „nicht aufgeholt“ (vorher: keine Antwort, der Poll lief in seine Frist);</item>
/// <item>eine Frage an einen Actor, der nie antwortet, läuft in ihre Frist — Proto.Cluster wiederholt sie sonst ohne Ende.</item>
/// </list>
/// Zeitkritisch (echte Uhr).
/// </summary>
[Collection(Zeitkritisch.Name)]
[Trait("Kategorie", "Garantie")]
public class LebenszyklusImClusterTests
{
    private static readonly TimeSpan Geduld = TimeSpan.FromSeconds(10);

    private static async Task<ActorSystem> ClusterAsync(params ClusterKind[] kinds)
    {
        var config = ClusterConfig.Setup("pruefstand-" + Guid.NewGuid().ToString("N")[..8],
            new TestProvider(new TestProviderOptions(), new InMemAgent()), new PartitionIdentityLookup());
        foreach (var k in kinds) config = config.WithClusterKind(k);
        var system = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost()).WithCluster(config);
        await system.Cluster().StartMemberAsync();
        return system;
    }

    [Fact]
    public async Task Der_Batch_Leser_gibt_seinen_Bereich_erst_frei_wenn_der_Durchlauf_endet()
    {
        var log = new PartitionsLog();
        log.Haenge(PartitionsLog.StreamIn(0, 1), 1);
        var fabrik = new ZaehlFabrik();
        var laeuft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tor = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool? benutzbar = null;

        var kind = new ClusterKind("batch-probe", Props.FromProducer(() => new BatchLeserActor((partition, bereich) =>
        {
            var tracker = new SpeicherTracker(new SpeicherKonsum());
            return new BatchLeser(log, tracker, "probe", partition, new Konsum(1, 500), null, async (e, _) =>
            {
                laeuft.TrySetResult();
                await tor.Task;                                    // ein Effekt, der nicht auf das Token hört (etwa ein laufender Befehl)
                try { bereich.Hole<ZaehlFabrik.Bereich>(); benutzbar = true; }
                catch (ObjectDisposedException) { benutzbar = false; }
                tracker.Wende(e.AggregateId, e.AggregateVersion);
            });
        }, fabrik)));
        var system = await ClusterAsync(kind);
        try
        {
            var partition = ClusterIdentity.Create("0", "batch-probe");
            (await new Bote(system.Cluster()).FrageAsync<WakeAck>(partition, new Wake(), Geduld, default)).Should().NotBeNull();
            await laeuft.Task.WaitAsync(Geduld);

            var pid = await system.Cluster().GetAsync(partition, CancellationToken.None);
            var stopp = system.Root.StopAsync(pid!);
            await Task.Delay(300);
            fabrik.Frei.Task.IsCompleted.Should().BeFalse("der Durchlauf nutzt den Bereich noch");

            tor.SetResult();
            await stopp.WaitAsync(Geduld);
            await fabrik.Frei.Task.WaitAsync(Geduld);
            benutzbar.Should().BeTrue("freigegeben wird nach dem Durchlauf, nicht während er den Bereich nutzt");
        }
        finally { await system.Cluster().ShutdownAsync(); }
    }

    [Fact]
    public async Task Der_Stream_Adapter_gibt_seinen_Bereich_nach_der_Passivierung_frei()
    {
        var fabrik = new ZaehlFabrik();
        var passivierung = new Passivierung(TimeSpan.Zero, TimeSpan.FromMilliseconds(300), TimeSpan.Zero);
        var kind = new ClusterKind("pull-probe", Props.FromProducer(() => new SignalAdapterActor(new PartitionsLog(),
            _ => ("probe", null, (_, _, _) => Task.CompletedTask), fabrik, passivierung: passivierung)));
        var system = await ClusterAsync(kind);
        try
        {
            var ack = await new Bote(system.Cluster()).FrageAsync<WakeAck>(ClusterIdentity.Create(Guid.NewGuid().ToString(), "pull-probe"),
                new Wake(), Geduld, default);
            ack.Should().Be(new WakeAck(Aufgeholt: true));

            await fabrik.Frei.Task.WaitAsync(Geduld);
            (fabrik.Geoeffnet, fabrik.Freigegeben).Should().Be((1, 1));
        }
        finally { await system.Cluster().ShutdownAsync(); }
    }

    [Fact]
    public async Task Eine_gescheiterte_Weckung_des_Stream_Adapters_wird_ehrlich_als_nicht_aufgeholt_beantwortet()
    {
        var stream = Guid.NewGuid();
        var log = new PartitionsLog();
        log.Haenge(stream, 1);
        var fabrik = new ZaehlFabrik();
        var kind = new ClusterKind("pull-probe", Props.FromProducer(() => new SignalAdapterActor(log,
            _ => ("probe", null, (_, _, _) => throw new InvalidOperationException("Dispatch kaputt")), fabrik)));
        var system = await ClusterAsync(kind);
        try
        {
            var uhr = Stopwatch.StartNew();
            var ack = await new Bote(system.Cluster()).FrageAsync<WakeAck>(ClusterIdentity.Create(stream.ToString(), "pull-probe"),
                new Wake(), Geduld, default);

            ack.Should().Be(new WakeAck(Aufgeholt: false), "der Poll hält seine Marke, statt in die Frist zu laufen");
            uhr.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            await fabrik.Frei.Task.WaitAsync(Geduld);              // der Neustart nach dem Fehler gibt den Bereich der alten Instanz frei
        }
        finally { await system.Cluster().ShutdownAsync(); }
    }

    /// <summary>Ein Actor, der jede Nachricht annimmt und nie antwortet.</summary>
    private sealed class Schweiger : RahmenActor
    {
        protected override Task BehandleAsync(IContext context) => Task.CompletedTask;
    }

    [Fact]
    public async Task Eine_Frage_an_einen_Actor_der_nie_antwortet_laeuft_in_ihre_Frist()
    {
        var system = await ClusterAsync(new ClusterKind("schweiger", Props.FromProducer(() => new Schweiger())));
        try
        {
            var uhr = Stopwatch.StartNew();
            var antwort = await new Bote(system.Cluster()).FrageAsync<WakeAck>(ClusterIdentity.Create("s", "schweiger"), new Wake(),
                TimeSpan.FromSeconds(1), default);

            antwort.Should().BeNull();
            uhr.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)).And.BeLessThan(TimeSpan.FromSeconds(5));
        }
        finally { await system.Cluster().ShutdownAsync(); }
    }
}
