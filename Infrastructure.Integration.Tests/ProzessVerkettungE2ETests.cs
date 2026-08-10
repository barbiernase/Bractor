using Abstractions;
using Domain.Antrag;
using Domain.Pipeline.Infrastructure;
using Domain.Vorgang;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;        // ProzessBeendet, GeneratedProzesse
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// PROZESS-VERKETTUNG live (Postgres/Consul/Redis): ein Prozess-Ende startet den nächsten Prozess.
///
/// Prozess A (<see cref="GenehmigungsProzess"/>) endet mit dem PERSISTIERTEN Domänen-Event
/// <see cref="VorgangGenehmigt"/>. Prozess B (<see cref="AktivierungsProzess"/>) ist auf genau dieses Event
/// als Auslöser gebunden → der Korrelations-Router startet B als EIGENE Instanz und aktiviert den Vorgang.
///
/// Damit ist der ganze Verkettungs-Fluss belegt: EIN Fach-Command → A genehmigt → das VorgangGenehmigt-Signal
/// löst B aus → B aktiviert. Braucht das Dev-Setup; immer SEQUENZIELL.
/// </summary>
public class ProzessVerkettungE2ETests
{
    private static (IHost Host, IAggregateDispatcher Dispatcher, IEventStoreRepository Store) Boot()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "verkettung-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "verkettung-" + Guid.NewGuid().ToString("N")[..8];
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

    private static CommandEnvelope Env(Guid id, string typ, ICommand payload)
        => new() { AggregateId = id, AggregateType = typ, Modus = new CommandModus.Client(0), Payload = payload };

    private static async Task<bool> Beendet(IEventStoreRepository store, Guid korr, bool erfolg)
        => (await store.ReadStreamAsync(korr, 0, default)).Any(e => e.Payload is ProzessBeendet b && b.Erfolg == erfolg);

    [Fact]
    public async Task Das_Ende_von_Prozess_A_startet_verkettet_Prozess_B()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var antrag = Guid.NewGuid();
            var vorgang = Guid.NewGuid();   // eigene, distinkte Stream-Id fürs Vorgang-Aggregat

            // DER Auslöser — der Rest läuft von selbst.
            dispatcher.Dispatch(Env(antrag, "Antrag", new StelleAntrag(antrag, vorgang)));

            // Prozess A (Genehmigung) läuft bis VorgangGenehmigt.
            var korrA = ProzessId.Für("GenehmigungsProzess", antrag, 1);
            await WaitAsync(() => Beendet(store, korrA, erfolg: true),
                TimeSpan.FromSeconds(60), "Prozess A (Genehmigung) ist nicht sauber terminiert.");
            (await store.LoadStateAsync<Vorgang>(vorgang))!.Genehmigt.Should().BeTrue();

            // Prozess B (Verkettung): das VorgangGenehmigt-Signal (Vorgang-Stream, Version 1) startet den
            //   Aktivierungs-Prozess als eigene Instanz → der Vorgang wird aktiviert.
            await WaitAsync(
                async () => (await store.LoadStateAsync<Vorgang>(vorgang))?.Aktiviert == true,
                TimeSpan.FromSeconds(60),
                "Der verkettete Aktivierungs-Prozess hat den Vorgang nicht aktiviert.");

            var korrB = ProzessId.Für("AktivierungsProzess", vorgang, 1);
            await WaitAsync(() => Beendet(store, korrB, erfolg: true),
                TimeSpan.FromSeconds(60), "Der verkettete Prozess B ist nicht sauber terminiert.");
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
