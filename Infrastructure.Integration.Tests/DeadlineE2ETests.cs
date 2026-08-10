using Abstractions;
using Domain.Erinnerung;
using Domain.Pipeline.Infrastructure;
using Infrastructure.Deadlines;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Projections.Generated;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Deadline-Primitiv LIVE (Postgres/Consul/Redis): eine fällige Frist stellt ihren Command zu. Ein In-Process-
/// Host mit <see cref="DeadlineExtensions.AddDeadlines"/> plant eine bereits überfällige Frist; der
/// DB-Uhr-getriebene Scheduler feuert <see cref="FristFaellig"/> an das <see cref="Erinnerung"/>-Aggregat, das
/// die Erinnerung genau einmal verbucht. Braucht das Dev-Setup; immer SEQUENZIELL.
/// </summary>
public class DeadlineE2ETests
{
    [Fact]
    public async Task Eine_faellige_Frist_stellt_ihren_Command_ans_Zielaggregat_zu()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "deadline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "deadline-" + Guid.NewGuid().ToString("N")[..8];
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        builder.Services.AddDeadlines(f => new FristFaellig(f.ZielAggregatId, f.Kontext));

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var plan = host.Services.GetRequiredService<IFristplan>();
            var clock = host.Services.GetRequiredService<IDbClock>();
            var store = host.Services.GetRequiredService<IEventStoreRepository>();

            var ziel = Guid.NewGuid();
            var jetzt = await clock.JetztAsync();
            // Bereits fällig → der nächste Scheduler-Tick (≤5s) feuert sofort.
            await plan.PlaneAsync(new Frist(Guid.NewGuid(), jetzt.AddSeconds(-5), ziel, "erinnere-mich"));

            await WaitAsync(
                async () => (await store.LoadStateAsync<Erinnerung>(ziel))?.Erinnert == true,
                TimeSpan.FromSeconds(60),
                "Die fällige Frist hat den FristFaellig-Command nicht ans Erinnerung-Aggregat zugestellt.");

            (await store.LoadStateAsync<Erinnerung>(ziel))!.Kontext.Should().Be("erinnere-mich");
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
            try { if (await condition()) return; } catch { }
            await Task.Delay(250);
        }
        throw new TimeoutException(message);
    }
}
