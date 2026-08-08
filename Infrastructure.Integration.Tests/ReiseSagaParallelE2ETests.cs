using Abstractions;
using Domain.Flug;
using Domain.Hotel;
using Domain.Pipeline.Infrastructure;
using Domain.Reise;
using Domain.Reiseauftrag;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Reisebuchungs-Saga unter NEBENLÄUFIGKEIT — viele Prozess-Instanzen gleichzeitig (je eigene Korrelation
/// aus je eigenem Auftrags-Stream). Zwei Fragen:
///   (1) unabhängig-parallel: N Sagas mit disjunkten Ziel-Aggregaten laufen sich nicht in die Quere;
///   (2) CROSS / Contention: N Sagas greifen auf DASSELBE Flug-Kontingent zu (Kapazität &lt; N) → der
///       Single-Writer serialisiert, genau K bestätigen, der Rest scheitert + kompensiert, KEIN Oversell.
/// Live gegen Postgres/Consul/Redis; braucht Docker; immer SEQUENZIELL (der Runner schaltet Parallelität ab).
/// </summary>
public class ReiseSagaParallelE2ETests
{
    private static (IHost Host, IAggregateDispatcher Dispatcher, IEventStoreRepository Store) Boot()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "reise-par-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "reisepar-" + Guid.NewGuid().ToString("N")[..8];
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

    // null = noch kein Ausgang, sonst Erfolg/Fehlschlag der Instanz.
    private static async Task<bool?> Ergebnis(IEventStoreRepository store, Guid korr)
    {
        var stream = await store.ReadStreamAsync(korr, 0, default);
        var beendet = stream.Select(e => e.Payload).OfType<ProzessBeendet>().FirstOrDefault();
        return beendet?.Erfolg;
    }

    [Fact]
    public async Task Viele_unabhaengige_Sagas_laufen_parallel_ohne_Interferenz()
    {
        const int N = 6;
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var sagas = Enumerable.Range(0, N).Select(_ => new
            {
                Flug = Guid.NewGuid(),
                Hotel = Guid.NewGuid(),
                Kunde = Guid.NewGuid(),
                Reise = Guid.NewGuid(),
                Auftrag = Guid.NewGuid(),
            }).ToList();

            // Alle Kontingente einrichten, dann alle Aufträge NEBENLÄUFIG feuern.
            foreach (var s in sagas)
            {
                dispatcher.Dispatch(new RichteFlugEin(s.Flug, 10));
                dispatcher.Dispatch(new RichteHotelEin(s.Hotel, 10));
            }
            Parallel.ForEach(sagas, s =>
                dispatcher.Dispatch(new BucheReise(s.Auftrag, s.Reise, s.Flug, s.Hotel, s.Kunde, 3, 2)));

            var korrs = sagas.Select(s => ProzessId.Für("ReiseProzess", s.Auftrag, 1)).ToList();
            await WaitAsync(async () =>
            {
                var res = await Task.WhenAll(korrs.Select(k => Ergebnis(store, k)));
                return res.All(r => r == true);
            }, TimeSpan.FromSeconds(90), "Nicht alle unabhängigen Sagas sind bestätigt.");

            foreach (var s in sagas)
            {
                (await store.LoadStateAsync<Flugkontingent>(s.Flug))!.Plaetze.Should().Be(7);
                (await store.LoadStateAsync<Hotelkontingent>(s.Hotel))!.Zimmer.Should().Be(8);
                (await store.LoadStateAsync<Reise>(s.Reise))!.Bestaetigt.Should().BeTrue();
            }
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Cross_geteiltes_Flugkontingent_serialisiert_ohne_Oversell()
    {
        const int N = 6;              // 6 Reisen konkurrieren …
        const int Kapazitaet = 4;     // … um nur 4 Flugplätze (Hotel im Überfluss)
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var flug = Guid.NewGuid();     // EIN geteiltes Flugkontingent
            var hotel = Guid.NewGuid();    // EIN geteiltes Hotelkontingent (reichlich)
            dispatcher.Dispatch(new RichteFlugEin(flug, Kapazitaet));
            dispatcher.Dispatch(new RichteHotelEin(hotel, 100));

            var sagas = Enumerable.Range(0, N).Select(_ => new
            {
                Kunde = Guid.NewGuid(),
                Reise = Guid.NewGuid(),
                Auftrag = Guid.NewGuid(),
            }).ToList();

            // Jede Saga will 1 Platz + 1 Zimmer aus den GETEILTEN Kontingenten — nebenläufig gefeuert.
            Parallel.ForEach(sagas, s =>
                dispatcher.Dispatch(new BucheReise(s.Auftrag, s.Reise, flug, hotel, s.Kunde, 1, 1)));

            var korrs = sagas.Select(s => ProzessId.Für("ReiseProzess", s.Auftrag, 1)).ToList();
            // Warten, bis JEDE Instanz einen Ausgang hat (true ODER false).
            await WaitAsync(async () =>
            {
                var res = await Task.WhenAll(korrs.Select(k => Ergebnis(store, k)));
                return res.All(r => r.HasValue);
            }, TimeSpan.FromSeconds(120), "Nicht alle konkurrierenden Sagas sind terminal.");

            var ergebnisse = await Task.WhenAll(korrs.Select(k => Ergebnis(store, k)));
            var bestaetigt = ergebnisse.Count(r => r == true);
            var gescheitert = ergebnisse.Count(r => r == false);

            bestaetigt.Should().Be(Kapazitaet, "genau so viele, wie Flugplätze da waren, dürfen bestätigen");
            gescheitert.Should().Be(N - Kapazitaet, "der Rest scheitert am ausgebuchten Flug + kompensiert");

            var flugEnd = (await store.LoadStateAsync<Flugkontingent>(flug))!;
            flugEnd.Plaetze.Should().Be(0, "alle Plätze vergeben, KEIN Oversell (nie negativ)");

            // Die kompensierten Hotel-Zimmer sind zurückgegeben: netto nur so viele belegt wie bestätigt.
            (await store.LoadStateAsync<Hotelkontingent>(hotel))!.Zimmer.Should().Be(100 - bestaetigt,
                "die Zimmer der gescheiterten Zweige wurden per Kompensation freigegeben");

            // Genau die bestätigten Reisen sind bestätigt.
            var bestaetigteReisen = 0;
            foreach (var s in sagas)
                if ((await store.LoadStateAsync<Reise>(s.Reise))?.Bestaetigt == true) bestaetigteReisen++;
            bestaetigteReisen.Should().Be(Kapazitaet);
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
