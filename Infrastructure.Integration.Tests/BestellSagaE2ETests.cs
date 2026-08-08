using Abstractions;
using Domain.Bestellung;
using Domain.Lager;
using Domain.Pipeline.Infrastructure;
using Domain.Versand;
using Domain.Zahlung;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;        // ProzessBeendet, GeneratedProzesse
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Bestell-Saga (DIAMANT) LIVE gegen den echten Cluster (Postgres/Consul/Redis): EIN Fach-Command
/// <c>GibBestellungAuf</c> → der Manager treibt zwei parallele Zweige (Lager reservieren, Konto belasten),
/// vereint sie am Join und versendet. Der Fehlerfall (Konto ungedeckt) gleicht den reservierten Bestand aus
/// und versendet nicht. Braucht Docker; immer SEQUENZIELL.
/// </summary>
public class BestellSagaE2ETests
{
    private static (IHost Host, IAggregateDispatcher Dispatcher, IEventStoreRepository Store) Boot()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "bestell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "bestell-" + Guid.NewGuid().ToString("N")[..8];
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
        => new() { AggregateId = id, AggregateType = typ, ExpectedVersion = 0, Payload = payload };

    private static async Task<bool> Beendet(IEventStoreRepository store, Guid korr, bool erfolg)
        => (await store.ReadStreamAsync(korr, 0, default)).Any(e => e.Payload is ProzessBeendet b && b.Erfolg == erfolg);

    [Fact]
    public async Task Ein_Fach_Command_reserviert_belastet_und_versendet_live()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var artikel = Guid.NewGuid();
            var kunde = Guid.NewGuid();
            var versand = Guid.NewGuid();
            var auftrag = Guid.NewGuid();

            dispatcher.Dispatch(Env(artikel, "Lager", new RichteLagerEin(artikel, 10)));
            dispatcher.Dispatch(Env(kunde, "Zahlungskonto", new RichteZahlungskontoEin(kunde, 100)));
            dispatcher.Dispatch(Env(auftrag, "Bestellauftrag",
                new GibBestellungAuf(auftrag, versand, kunde, artikel, 3, 50)));

            var korr = ProzessId.Für("BestellProzess", auftrag, 1);
            await WaitAsync(() => Beendet(store, korr, erfolg: true),
                TimeSpan.FromSeconds(60), "Die Bestellung ist nicht bis versendet gelaufen.");

            (await store.LoadStateAsync<Lager>(artikel))!.Bestand.Should().Be(7);
            (await store.LoadStateAsync<Zahlungskonto>(kunde))!.Saldo.Should().Be(50);
            (await store.LoadStateAsync<Versand>(versand))!.Versandt.Should().BeTrue();
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ungedecktes_Konto_gleicht_live_aus_und_versendet_nicht()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var artikel = Guid.NewGuid();
            var kunde = Guid.NewGuid();
            var versand = Guid.NewGuid();
            var auftrag = Guid.NewGuid();

            dispatcher.Dispatch(Env(artikel, "Lager", new RichteLagerEin(artikel, 10)));
            dispatcher.Dispatch(Env(kunde, "Zahlungskonto", new RichteZahlungskontoEin(kunde, 30)));  // zu wenig
            dispatcher.Dispatch(Env(auftrag, "Bestellauftrag",
                new GibBestellungAuf(auftrag, versand, kunde, artikel, 3, 50)));

            var korr = ProzessId.Für("BestellProzess", auftrag, 1);
            await WaitAsync(() => Beendet(store, korr, erfolg: false),
                TimeSpan.FromSeconds(60), "Der Fehlerfall ist nicht bis Fehlgeschlagen gelaufen.");

            (await store.LoadStateAsync<Lager>(artikel))!.Bestand.Should().Be(10, "die Reservierung wurde ausgeglichen");
            (await store.LoadStateAsync<Zahlungskonto>(kunde))!.Saldo.Should().Be(30, "nie belastet");
            (await store.LoadStateAsync<Versand>(versand)).Should().BeNull("der Versand lief nie");
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
