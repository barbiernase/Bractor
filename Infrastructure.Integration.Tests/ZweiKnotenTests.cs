using Abstractions;
using Domain.ImagePair;
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
/// Zwei volle CQRS-Hosts (zwei ActorSystems) in einem Consul-Cluster: was zwischen Knoten reist, reist serialisiert über die Leitung.
/// <list type="bullet">
///   <item><b>Command-Dispatch über die Identität:</b> <c>PartitionIdentityLookup</c> platziert jede Aggregat-Identität auf genau einem
///     Knoten; von zwölf Ids landet praktisch sicher eine fremd ((1/2)^12). Anfrage (<c>CommandEnvelope</c>) und Antwort
///     (<c>CommandResult</c> mit polymorpher Event-Liste) müssen über den Wire-Serializer gehen — ohne ihn scheitert jeder fremde Hop.</item>
///   <item><b>Zustellung an eine lokale PID eines anderen Knotens:</b> der Mechanismus des PubSub-Brokers (<c>context.Send(subscriberPid, …)</c>
///     an node-lokal gespawnte Empfänger).</item>
/// </list>
/// Eigenes Event-Schema (<c>it_zwei_knoten</c>). Braucht Postgres, Redis, Consul.
/// </summary>
public class ZweiKnotenTests
{
    [Fact]
    public async Task Command_Dispatch_und_lokale_PID_reisen_serialisiert_zwischen_zwei_Knoten()
    {
        var clusterName = "it-zwei-knoten-" + Guid.NewGuid().ToString("N")[..8];
        using var hostA = BaueKnoten(clusterName);
        using var hostB = BaueKnoten(clusterName);
        await hostA.StartAsync();
        await hostB.StartAsync();
        try
        {
            var systemA = hostA.Services.GetRequiredService<ActorSystem>();
            var systemB = hostB.Services.GetRequiredService<ActorSystem>();
            await WarteAsync(() => systemA.Cluster().MemberList.GetAllMembers().Length >= 2
                                   && systemB.Cluster().MemberList.GetAllMembers().Length >= 2,
                "Cluster hat nicht auf zwei Member konvergiert.");

            // ── Command-Dispatch über die Identität ──
            var ids = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();
            foreach (var id in ids)
            {
                var result = await ErstelleAsync(systemA.Cluster(), id);
                result.Should().NotBeNull($"Command für {id} muss (ggf. auf dem anderen Knoten) zugestellt werden");
                result!.Success.Should().BeTrue();
                result.NewVersion.Should().Be(1, "nur das Fakt — die Quittung steht in der Inbox-Tabelle, nicht im Stream");
                result.Events.Should().ContainSingle().Which.Should().BeOfType<ImagePairErstellt>();
            }
            var wiederholung = await ErstelleAsync(systemA.Cluster(), ids[0], expectedVersion: 0);
            wiederholung!.Success.Should().BeFalse("ein warmer (evtl. fremder) Actor prüft die Client-Version");

            // ── Zustellung an eine lokale PID auf Knoten B ──
            var empfangen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pid = systemB.Root.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is Wake) empfangen.TrySetResult(true);
                return Task.CompletedTask;
            }));
            pid.Address.Should().NotBe(systemA.Address);
            systemA.Root.Send(pid, new Wake());
            (await empfangen.Task.WaitAsync(TimeSpan.FromSeconds(15))).Should().BeTrue();
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
        }
    }

    private static async Task<CommandResult?> ErstelleAsync(Cluster cluster, Guid id, int expectedVersion = 0)
    {
        var envelope = new CommandEnvelope
        {
            AggregateId = id,
            AggregateType = "ImagePair",
            Modus = new CommandModus.Client(expectedVersion),
            Payload = new ErstelleImagePair(id, PairKey: "PAIR-ZK-" + id.ToString("N")[..6], ProduziertAm: DateTimeOffset.UtcNow,
                AufgenommenAm: DateTimeOffset.UtcNow, UrsprungsPfad: "/it/zwei-knoten"),
        };
        var identity = ClusterIdentity.Create(id.ToString(), "ImagePair");
        for (var versuch = 0; versuch < 5; versuch++)   // die Topologie braucht evtl. ein paar Anläufe
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (await cluster.RequestAsync<CommandResult>(identity, envelope, cts.Token) is { } r) return r;
        }
        return null;
    }

    private static IHost BaueKnoten(string clusterName)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = clusterName;
            opts.EventStoreSchema = "it_zwei_knoten";
        });
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        return builder.Build();
    }

    private static async Task WarteAsync(Func<bool> bedingung, string meldung)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try { if (bedingung()) return; } catch { /* noch nicht bereit */ }
            await Task.Delay(250);
        }
        throw new TimeoutException(meldung);
    }
}
