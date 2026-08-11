using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto.Cluster;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 8 — das Multi-Node-Tor: der Command-Dispatch reist SERIALISIERT über die Leitung.
///
/// Fährt ZWEI volle CQRS-Hosts (zwei ActorSystems) im selben Consul-Cluster hoch. Beide registrieren
/// den <c>ImagePair</c>-Aggregat-Kind und den Wire-Serializer (<see cref="Infrastructure.Serialization.CqrsWireSerializer"/>).
/// <c>PartitionIdentityLookup</c> platziert jede Aggregat-Identität auf GENAU einem Node (Single-Activation);
/// bei zwei Membern landet rund die Hälfte der Ids fremd. Wir senden eine Charge distinkter Ids von Node A
/// per <c>RequestAsync&lt;CommandResult&gt;</c>: die Anfrage (<c>CommandEnvelope</c>) UND die Antwort
/// (<c>CommandResult</c> inkl. polymorpher Event-Liste) müssen über die Leitung serialisieren.
///
/// Beweiskraft: Ohne registrierten Wire-Serializer schlägt jeder cross-node-Hop fehl (Proto findet keinen
/// Serializer für die rohen CLR-Records). Dass ALLE Ids einer Charge von N=12 lokal blieben, ist praktisch
/// ausgeschlossen ((1/2)^12) — grün = Serialisierung funktioniert cross-node.
///
/// Braucht Postgres (5432), Redis (6379), Consul (8500). Sequentiell laufen lassen.
/// </summary>
public class TwoNodeCommandDispatchTests
{
    [Fact]
    public async Task Command_Dispatch_reist_serialisiert_ueber_zwei_Nodes()
    {
        var clusterName = "p8-twonode-" + Guid.NewGuid().ToString("N")[..8];

        using var hostA = BuildNode(clusterName);
        using var hostB = BuildNode(clusterName);

        await hostA.StartAsync();
        await hostB.StartAsync();
        try
        {
            var clusterA = hostA.Services.GetRequiredService<Proto.ActorSystem>().Cluster();

            // Warten bis beide Member sichtbar sind (Topologie konvergiert).
            await WaitAsync(
                () => Task.FromResult(clusterA.MemberList.GetAllMembers().Length >= 2),
                TimeSpan.FromSeconds(30),
                "Cluster hat nicht auf zwei Member konvergiert.");

            const int n = 12;
            var ids = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToArray();

            foreach (var pairId in ids)
            {
                var result = await SendErstelleImagePairAsync(clusterA, pairId);

                result.Should().NotBeNull($"Command für {pairId} muss (ggf. cross-node) zugestellt werden.");
                result!.Success.Should().BeTrue($"Aggregat muss {pairId} erfolgreich anlegen.");
                result.NewVersion.Should().Be(1);
                result.Events.Should().ContainSingle()
                    .Which.Should().BeOfType<ImagePairErstellt>();
            }

            // Gegenprobe: ein zweiter Command auf dieselbe Id (Client-OCC gegen Version 1) belastet den
            // bereits aktivierten Actor erneut — deckt den Round-Trip auf einen WARMEN (evtl. fremden) Actor ab.
            var wiederholung = await SendErstelleImagePairAsync(clusterA, ids[0], expectedVersion: 0);
            wiederholung.Should().NotBeNull();
            wiederholung!.Success.Should().BeFalse("zweites Erstellen mit ExpectedVersion 0 muss an OCC scheitern.");
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
        }
    }

    private static async Task<CommandResult?> SendErstelleImagePairAsync(
        Cluster cluster, Guid pairId, int expectedVersion = 0)
    {
        var envelope = new CommandEnvelope
        {
            AggregateId = pairId,
            AggregateType = "ImagePair",   // MUSS mit dem ClusterKind übereinstimmen
            Modus = new CommandModus.Client(expectedVersion),
            Payload = new ErstelleImagePair(
                pairId,
                PairKey: "PAIR-P8-" + pairId.ToString("N")[..6],
                ProduziertAm: DateTimeOffset.UtcNow,
                AufgenommenAm: DateTimeOffset.UtcNow,
                UrsprungsPfad: "/p8/twonode"),
        };

        var identity = ClusterIdentity.Create(pairId.ToString(), "ImagePair");

        // Bounded Retry wie im Dispatcher: die Topologie braucht evtl. ein paar Anläufe.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await cluster.RequestAsync<CommandResult>(identity, envelope, cts.Token);
            if (result != null) return result;
        }
        return null;
    }

    private static IHost BuildNode(string clusterName)
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "p8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = clusterName;   // GLEICHER Name → ein Cluster, zwei Member
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
