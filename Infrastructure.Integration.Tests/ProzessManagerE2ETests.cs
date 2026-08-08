using Abstractions;
using Domain.Auftrag;
using Domain.Konto;
using Domain.Pipeline.Infrastructure;
using Domain.Sammelauftrag;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;        // ProzessBeendet, GeneratedProzesse
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 5 (Neubau) — der generische Prozess-Manager LIVE gegen einen echten Single-Node-Cluster
/// (Postgres/Consul/Redis). Beweist die ganze Kette signal-getrieben: EIN Fach-Command → Auslöse-Event →
/// KorrelationsRouter startet den Manager → der faltet sein Marking aus den Konto-Streams, feuert
/// fire-and-forget die aktivierten Transitionen → Ziel-Events wecken ihn neu → bis Abgeschlossen bzw.
/// (bei Ablehnung) Fehlgeschlagen mit Kompensation. Zusätzlich: Fan-out/dynamische Breite (Sammelüberweisung).
///
/// Braucht Docker (Dev-Setup). Immer SEQUENZIELL (xunit.runner.json).
/// </summary>
public class ProzessManagerE2ETests
{
    private static (IHost Host, IAggregateDispatcher Dispatcher, IEventStoreRepository Store) Boot()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "phase5-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "phase5-manager-" + Guid.NewGuid().ToString("N")[..8];
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        builder.Services.AddGeneratedProzesse();   // generisches Manager-Kind + Korrelations-Router

        var host = builder.Build();
        return (host,
            host.Services.GetRequiredService<IAggregateDispatcher>(),
            host.Services.GetRequiredService<IEventStoreRepository>());
    }

    private static CommandEnvelope Env(Guid id, string typ, ICommand payload)
        => new() { AggregateId = id, AggregateType = typ, Modus = new CommandModus.Client(0), Payload = payload };

    private static async Task<bool> ProzessBeendetMit(IEventStoreRepository store, Guid korr, bool erfolg)
    {
        var log = await store.ReadStreamAsync(korr, 0, default);
        return log.Any(e => e.Payload is ProzessBeendet b && b.Erfolg == erfolg);
    }

    [Fact]
    public async Task Ein_Fach_Command_treibt_die_Ueberweisung_signal_getrieben_bis_Abgeschlossen()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var quelle = Guid.NewGuid();
            var ziel = Guid.NewGuid();
            var auftrag = Guid.NewGuid();

            dispatcher.Dispatch(Env(quelle, "Konto", new EroeffneKonto(quelle, 100)));
            dispatcher.Dispatch(Env(ziel, "Konto", new EroeffneKonto(ziel, 0)));

            // NUR dieser eine Fach-Command: BeauftrageUeberweisung → UeberweisungBeauftragt → Router startet
            // den Manager → 3 Transitionen signal-getrieben → Abgeschlossen.
            dispatcher.Dispatch(Env(auftrag, "Ueberweisungsauftrag", new BeauftrageUeberweisung(auftrag, quelle, ziel, 30)));

            var korr = ProzessId.Für("UeberweisungsProzess", auftrag, 1);
            await WaitAsync(() => ProzessBeendetMit(store, korr, erfolg: true),
                TimeSpan.FromSeconds(45), "Der Prozess ist nicht signal-getrieben bis Abgeschlossen gelaufen.");

            (await store.LoadStateAsync<Konto>(quelle))!.Saldo.Should().Be(70);
            (await store.LoadStateAsync<Konto>(ziel))!.Saldo.Should().Be(30);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Gesperrtes_Zielkonto_fuehrt_live_zu_Kompensation_und_Fehlgeschlagen()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var quelle = Guid.NewGuid();
            var ziel = Guid.NewGuid();
            var auftrag = Guid.NewGuid();

            dispatcher.Dispatch(Env(quelle, "Konto", new EroeffneKonto(quelle, 100)));
            dispatcher.Dispatch(Env(ziel, "Konto", new EroeffneKonto(ziel, 0, Gesperrt: true)));
            dispatcher.Dispatch(Env(auftrag, "Ueberweisungsauftrag", new BeauftrageUeberweisung(auftrag, quelle, ziel, 30)));

            var korr = ProzessId.Für("UeberweisungsProzess", auftrag, 1);
            await WaitAsync(() => ProzessBeendetMit(store, korr, erfolg: false),
                TimeSpan.FromSeconds(45), "Der Fehlerfall ist nicht bis Fehlgeschlagen gelaufen.");

            var q = await store.LoadStateAsync<Konto>(quelle);
            q!.Saldo.Should().Be(100, "nichts gebucht");
            q.Reserviert.Should().Be(0, "die Reservierung wurde ausgeglichen");
            (await store.LoadStateAsync<Konto>(ziel))!.Saldo.Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Sammelueberweisung_faechert_live_an_alle_Ziele_aus_und_bucht_nach_allen()
    {
        var (host, dispatcher, store) = Boot();
        await host.StartAsync();
        try
        {
            var quelle = Guid.NewGuid();
            var ziele = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var auftrag = Guid.NewGuid();

            dispatcher.Dispatch(Env(quelle, "Konto", new EroeffneKonto(quelle, 100)));
            foreach (var z in ziele) dispatcher.Dispatch(Env(z, "Konto", new EroeffneKonto(z, 0)));

            dispatcher.Dispatch(Env(auftrag, "Sammelueberweisungsauftrag",
                new BeauftrageSammelueberweisung(auftrag, quelle, ziele, 20)));

            var korr = ProzessId.Für("SammelueberweisungsProzess", auftrag, 1);
            await WaitAsync(() => ProzessBeendetMit(store, korr, erfolg: true),
                TimeSpan.FromSeconds(60), "Die Sammelüberweisung ist nicht bis Abgeschlossen gelaufen.");

            // Fan-out: alle 3 Ziele je 20 gutgeschrieben; Quelle 100 − 60 (reserviert+gebucht) = 40.
            foreach (var z in ziele)
                (await store.LoadStateAsync<Konto>(z))!.Saldo.Should().Be(20, "jedes Ziel genau einmal gutgeschrieben");
            var q = await store.LoadStateAsync<Konto>(quelle);
            q!.Saldo.Should().Be(40);
            q.Reserviert.Should().Be(0, "die Reservierung wurde nach allen Gutschriften gebucht");
        }
        finally { await host.StopAsync(); }
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await condition()) return; } catch { /* Aggregat/Stream evtl. noch nicht da */ }
            await Task.Delay(250);
        }
        throw new TimeoutException(message);
    }
}
