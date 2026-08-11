using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Infrastructure.Serialization;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote.GrpcNet;

namespace Infrastructure.Pruefstand.MultiNode;

/// <summary>
/// Phase 4 — der eigentliche Cross-Node-Beweis: eine Nachricht wird auf Node B serialisiert, reist über
/// eine ECHTE gRPC-Node-Grenze, wird auf Node A deserialisiert, behandelt, und die Antwort reist
/// serialisiert zurück. Genau das war vorher kaputt (kein registrierter Serializer für die interne Ebene).
///
/// Infra-frei: zwei <see cref="ActorSystem"/>s in EINEM Testprozess, je eigener gRPC-Port
/// (<c>BindToLocalhost</c> → ephemer), zusammengeführt über den <see cref="TestProvider"/> mit geteiltem
/// <see cref="InMemAgent"/> — kein Consul, kein Postgres. Der Transport (Proto.Remote/gRPC) ist echt,
/// die Serialisierung damit auch. Beide Nodes registrieren den <see cref="InternalPlaneSerializer"/> wie
/// der Host.
///
/// Weil eine lokale Aktivierung NICHT serialisiert (in-process), ist der Kern der Prüfung: mindestens ein
/// Vorgang wurde REMOTE (auf Node A) behandelt — nur dann sind <see cref="CommandEnvelope"/> UND
/// <see cref="CommandResult"/> real über den Draht gegangen.
/// </summary>
public class ZweiNodeSerialisierungTests
{
    private const string Kind = "EchoKonto";

    /// <summary>Test-Kind: nimmt einen <see cref="CommandEnvelope"/>, spiegelt Payload + Modus in ein
    /// <see cref="CommandResult"/> und stempelt die Adresse des behandelnden Nodes ein (Remote-Hop-Marker).</summary>
    private sealed class EchoActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is CommandEnvelope env)
            {
                var betrag = env.Payload switch
                {
                    EroeffneKonto e => e.StartSaldo,
                    SchreibeGut g => g.Betrag,
                    _ => -1m,
                };
                var version = env.Modus is CommandModus.Client c ? c.ExpectedVersion : -1;
                context.Respond(new CommandResult
                {
                    Success = true,
                    AggregateId = env.AggregateId,
                    NewVersion = version,
                    ErrorMessage = context.System.Address,   // Test-Carrier: welcher Node hat behandelt?
                    Events = new List<IEvent> { new KontoEroeffnet(betrag, false) },
                });
            }
            return Task.CompletedTask;
        }
    }

    private static Cluster Node(InMemAgent agent, string clusterName)
    {
        var system = new ActorSystem();
        var remoteConfig = GrpcNetRemoteConfig.BindToLocalhost();   // ephemerer freier Port
        remoteConfig.Serialization.RegisterSerializer(
            InternalPlaneSerializer.SerializerId,
            InternalPlaneSerializer.Priority,
            new InternalPlaneSerializer());
        var clusterConfig = ClusterConfig
            .Setup(clusterName, new TestProvider(new TestProviderOptions(), agent), new PartitionIdentityLookup())
            .WithClusterKind(Kind, Props.FromProducer(() => new EchoActor()));
        system.WithRemote(remoteConfig).WithCluster(clusterConfig);
        return system.Cluster();
    }

    private static CommandEnvelope Env(Guid id, int i) => new()
    {
        AggregateId = id,
        AggregateType = Kind,
        Modus = new CommandModus.Client(i),
        Payload = new EroeffneKonto(id, 100m + i),
    };

    private static async Task<CommandResult?> Sende(Cluster von, Guid id, int i)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await von.RequestAsync<CommandResult>(
            ClusterIdentity.Create(id.ToString(), Kind), Env(id, i), cts.Token);
    }

    [Fact]
    public async Task CommandEnvelope_reist_ueber_die_echte_gRPC_Node_Grenze_und_zurueck()
    {
        var agent = new InMemAgent();
        var name = "twonode-" + Guid.NewGuid().ToString("N")[..8];
        var clusterA = Node(agent, name);
        var clusterB = Node(agent, name);
        await clusterA.StartMemberAsync();
        await clusterB.StartMemberAsync();
        var adresseA = clusterA.System.Address;
        try
        {
            // ── Readiness: von B aus senden, bis ein Vorgang auf A landet (Topologie mit 2 Membern steht,
            //    Remote-Routing funktioniert). Zugleich der erste echte Cross-Node-Round-Trip. ──
            await WaitAsync(async () =>
            {
                var r = await Sende(clusterB, Guid.NewGuid(), 0);
                return r?.ErrorMessage == adresseA;
            }, TimeSpan.FromSeconds(30), "Kein Remote-Hop nach A — 2-Member-Topologie/Routing nicht erreicht.");

            // ── Messschleife: viele Vorgänge von B; Korrektheit prüfen + Remote-Hops zählen. ──
            var adressen = new HashSet<string>();
            const int N = 40;
            for (int i = 0; i < N; i++)
            {
                var id = Guid.NewGuid();
                var res = await Sende(clusterB, id, i);

                res.Should().NotBeNull();
                res!.Success.Should().BeTrue();
                res.AggregateId.Should().Be(id);
                res.NewVersion.Should().Be(i, "CommandModus.Client(i) muss verlustfrei über den Draht");
                res.Events.Should().ContainSingle()
                    .Which.Should().BeOfType<KontoEroeffnet>()
                    .Which.StartSaldo.Should().Be(100m + i, "ICommand-Payload muss verlustfrei über den Draht");
                adressen.Add(res.ErrorMessage!);
            }

            // Der Beweis: mindestens ein Vorgang wurde auf Node A behandelt → CommandEnvelope UND
            // CommandResult sind real über gRPC serialisiert gereist (nicht nur in-process).
            adressen.Should().Contain(adresseA,
                "mindestens ein Vorgang muss über die echte Node-Grenze serialisiert worden sein");
        }
        finally
        {
            await clusterA.ShutdownAsync();
            await clusterB.ShutdownAsync();
        }
    }

    private static async Task WaitAsync(Func<Task<bool>> cond, TimeSpan timeout, string msg)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await cond()) return; }
            catch (OperationCanceledException) { /* Cluster noch nicht routbar → weiter */ }
            await Task.Delay(150);
        }
        throw new TimeoutException(msg);
    }
}
