using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Infrastructure;
using Domain.Projections;
using Domain.Reaktion;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 3 (3c) — das Tor LIVE: ein Event löst über eine REAKTION einen Command auf
/// einem ANDEREN Aggregat aus.
///
/// Kette: ErstelleImagePair → ImagePair-Aggregat → ImagePairErstellt (Broadcast) →
/// ImagePairReaktion (yieldet WirkeReaktion) → Output-Routing im Actor → WirkeReaktion
/// an den Reaktionsempfaenger → ReaktionGewirkt. Der Empfänger dedupliziert per Noop-
/// Decider (die „verpufft"-Hälfte ist am Decider bewiesen, s. Prüfstand Phase3).
///
/// Braucht Postgres/Redis/Consul (Dev-Setup).
/// </summary>
public class ReaktionE2ETests
{
    [Fact]
    public async Task Event_loest_Reaktion_Command_auf_Fremd_Aggregat_aus()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "phase3-reaktion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "phase3-reaktion-" + Guid.NewGuid().ToString("N")[..8];
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var eventStore = host.Services.GetRequiredService<IEventStoreRepository>();

            var pairId = Guid.NewGuid();
            dispatcher.Dispatch(new CommandEnvelope
            {
                AggregateId = pairId,
                AggregateType = "ImagePair",
                ExpectedVersion = 0,
                Payload = new ErstelleImagePair(
                    pairId, "PAIR-REAKTION", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/live/reaktion")
            });

            // Warten, bis die Reaktion beim Fremd-Aggregat gewirkt hat.
            await WaitAsync(
                async () =>
                {
                    var empf = await eventStore.LoadStateAsync<Reaktionsempfaenger>(ImagePairReaktion.EmpfaengerId);
                    return empf != null && empf.VerarbeiteteReaktionen.Contains(pairId);
                },
                TimeSpan.FromSeconds(30),
                "Die Reaktion ist nicht als Command beim Fremd-Aggregat (Reaktionsempfaenger) angekommen.");

            var empfaenger = await eventStore.LoadStateAsync<Reaktionsempfaenger>(ImagePairReaktion.EmpfaengerId);
            empfaenger!.VerarbeiteteReaktionen.Should().Contain(
                pairId, "das auslösende ImagePair hat genau eine Reaktion gewirkt");
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await condition()) return; } catch { /* Aggregat evtl. noch nicht da */ }
            await Task.Delay(200);
        }
        throw new TimeoutException(message);
    }
}
