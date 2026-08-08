using Abstractions;
using Domain.Flug;
using Domain.Hotel;
using Domain.Pipeline.Infrastructure;
using Domain.Reise;
using Domain.Reiseauftrag;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;        // ProzessBeendet, GeneratedProzesse
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Reisebuchungs-Saga (DIAMANT) LIVE gegen den echten Cluster (Postgres/Consul/Redis): EIN Fach-Command
/// <c>BucheReise</c> → der Manager treibt zwei parallele Zweige (Flug reservieren, Hotel reservieren),
/// vereint sie am Join und bestätigt die Reise. Braucht Docker; immer SEQUENZIELL. Muster 1:1 aus
/// <c>BestellSagaE2ETests</c>.
/// </summary>
public class ReiseSagaE2ETests
{
    private static (IHost Host, IAggregateDispatcher Dispatcher, IEventStoreRepository Store) Boot()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "reise-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "reise-" + Guid.NewGuid().ToString("N")[..8];
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        builder.Services.AddGeneratedProzesse();
        var host = builder.Build();
        return (host,
            host.Services.GetRequiredService<IAggregateDispatcher>(),
            host.Services.GetRequiredService<IEventStoreRepository>());
    }

    private static async Task<bool> Beendet(IEventStoreRepository store, Guid korr, bool erfolg)
        => (await store.ReadStreamAsync(korr, 0, default)).Any(e => e.Payload is ProzessBeendet b && b.Erfolg == erfolg);

    [Fact]
    public async Task Ein_Fach_Command_reserviert_flug_und_hotel_und_bestaetigt_live()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var flug = Guid.NewGuid();
            var hotel = Guid.NewGuid();
            var kunde = Guid.NewGuid();
            var reise = Guid.NewGuid();
            var auftrag = Guid.NewGuid();

            // Sprechender Dispatch OHNE Magic String: nur der Command — AggregateType leitet sich aus der
            // generierten Map ab, AggregateId trägt der Command selbst.
            dispatcher.Dispatch(new RichteFlugEin(flug, 10));
            dispatcher.Dispatch(new RichteHotelEin(hotel, 10));
            dispatcher.Dispatch(new BucheReise(auftrag, reise, flug, hotel, kunde, 3, 2));

            var korr = ProzessId.Für("ReiseProzess", auftrag, 1);
            await WaitAsync(() => Beendet(store, korr, erfolg: true),
                TimeSpan.FromSeconds(60), "Die Reisebuchung ist nicht bis bestätigt gelaufen.");

            (await store.LoadStateAsync<Flugkontingent>(flug))!.Plaetze.Should().Be(7);
            (await store.LoadStateAsync<Hotelkontingent>(hotel))!.Zimmer.Should().Be(8);
            (await store.LoadStateAsync<Reise>(reise))!.Bestaetigt.Should().BeTrue();
        }
        finally { await host.StopAsync(); }
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
