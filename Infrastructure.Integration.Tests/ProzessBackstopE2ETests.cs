using Abstractions;
using Domain.Auftrag;
using Domain.Konto;
using Domain.Reaktion;                 // WirkeReaktion, ReaktionGewirkt (Noop-Decider)
using Domain.Pipeline.Infrastructure;
using FluentAssertions;
using Infrastructure.Aggregate;      // KommandoVerarbeitet (Inbox-Marke)
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;         // ProzessGestartet, ProzessBeendet, GeneratedProzesse
using Infrastructure.Projections.Generated;   // AddGeneratedPullPaths
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Schritt 1 des Audit-Fixes (docs/wurzel-1-outbox-skizze-v2.md §3 + Fix K2) LIVE gegen echtes
/// Postgres/Consul/Redis. Beweist zwei Dinge, die vorher fehlten:
///
///  • <b>Marke auf Noop (K2):</b> Ein idempotenter (AnyVersion-)Command, der beim Ziel ein NOOP ist (der
///    Decider liefert nichts — z.B. eine schon verarbeitete Reaktion), hinterlässt jetzt trotzdem eine durable
///    <see cref="KommandoVerarbeitet"/>-Marke. Ohne sie bliebe die zugehörige Prozess-Transition ewig „pending"
///    → der Manager (und der Backstop) feuerten sie endlos → Livelock, nie ProzessBeendet.
///
///  • <b>§3-Backstop:</b> Ein OFFENER Prozess, den kein Signal mehr weckt (Start-Signal verloren simuliert:
///    Auslöser-Event direkt in den Store geschrieben, ohne Actor/Signal), wird allein vom durablen Offen-Index
///    + Backstop-Scan bis <c>ProzessBeendet</c> getrieben.
///
/// Braucht Docker (Dev-Setup). Immer SEQUENZIELL (xunit.runner.json).
/// </summary>
public class ProzessBackstopE2ETests
{
    private static (IHost Host, IAggregateDispatcher Dispatcher, IEventStoreRepository Store, IProzessOffenIndex Offen) Boot()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "backstop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "backstop-" + Guid.NewGuid().ToString("N")[..8];
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        builder.Services.AddGeneratedProzesse();

        var host = builder.Build();
        return (host,
            host.Services.GetRequiredService<IAggregateDispatcher>(),
            host.Services.GetRequiredService<IEventStoreRepository>(),
            host.Services.GetRequiredService<IProzessOffenIndex>());
    }

    private static CommandEnvelope Env(Guid id, string typ, ICommand payload)
        => new() { AggregateId = id, AggregateType = typ, ExpectedVersion = 0, Payload = payload };

    private static async Task<int> MarkenFuer(IEventStoreRepository store, Guid stream, Guid vorgang)
    {
        var log = await store.ReadStreamAsync(stream, 0, default);
        return log.Count(e => e.Payload is KommandoVerarbeitet k && k.CommandId == vorgang);
    }

    // ── Fix K2: ein idempotenter Noop hinterlässt eine Marke (ohne Domänen-Effekt) ────────────────────

    [Fact]
    public async Task Idempotenter_Noop_Command_hinterlaesst_eine_Marke_ohne_Wirkung()
    {
        var (host, dispatcher, store, _) = Boot();
        await host.StartAsync();
        try
        {
            var empf = Guid.NewGuid();
            var reaktion = Guid.NewGuid();

            // 1. WirkeReaktion (AnyVersion, vorgang1): wirkt → ReaktionGewirkt; ReaktionId ist nun im State.
            var vorgang1 = Guid.NewGuid();
            dispatcher.Dispatch(new CommandEnvelope
            {
                CommandId = vorgang1, AggregateId = empf, AggregateType = "Reaktionsempfaenger",
                ExpectedVersion = CommandEnvelope.AnyVersion,
                Payload = new WirkeReaktion(empf, reaktion, "erste")
            });
            await WaitAsync(async () => (await store.ReadStreamAsync(empf, 0, default)).Any(e => e.Payload is ReaktionGewirkt),
                TimeSpan.FromSeconds(30), "Die erste Reaktion wurde nicht wirksam.");

            // 2. Gleiche ReaktionId, aber NEUE CommandId (vorgang2): die Framework-Inbox greift NICHT (neue
            //    CommandId) → der Decider läuft → ReaktionId schon verarbeitet → NOOP (leeres Yield). Mit dem
            //    Fix hinterlässt der Noop-Pfad trotzdem genau eine Marke für vorgang2.
            var vorgang2 = Guid.NewGuid();
            dispatcher.Dispatch(new CommandEnvelope
            {
                CommandId = vorgang2, AggregateId = empf, AggregateType = "Reaktionsempfaenger",
                ExpectedVersion = CommandEnvelope.AnyVersion,
                Payload = new WirkeReaktion(empf, reaktion, "zweite (Noop)")
            });

            await WaitAsync(async () => await MarkenFuer(store, empf, vorgang2) == 1,
                TimeSpan.FromSeconds(30),
                "Der Noop hinterließ KEINE KommandoVerarbeitet-Marke — ohne sie bliebe eine Prozess-Transition ewig pending (Livelock).");

            var log = await store.ReadStreamAsync(empf, 0, default);
            log.Count(e => e.Payload is ReaktionGewirkt).Should().Be(1,
                "der Noop hatte keinen Domänen-Effekt — nur die erste Reaktion wirkte");
        }
        finally { await host.StopAsync(); }
    }

    // ── §3-Backstop: ein orphaned offener Prozess läuft allein aus dem Offen-Index bis Abgeschlossen ──

    [Fact]
    public async Task Backstop_treibt_einen_orphaned_offenen_Prozess_bis_ProzessBeendet()
    {
        var (host, dispatcher, store, offen) = Boot();
        await host.StartAsync();
        try
        {
            var quelle = Guid.NewGuid();
            var ziel = Guid.NewGuid();
            var auftrag = Guid.NewGuid();

            dispatcher.Dispatch(Env(quelle, "Konto", new EroeffneKonto(quelle, 100)));
            dispatcher.Dispatch(Env(ziel, "Konto", new EroeffneKonto(ziel, 0)));
            await WaitAsync(async () => (await store.LoadStateAsync<Konto>(quelle))?.Saldo == 100
                                     && (await store.LoadStateAsync<Konto>(ziel)) != null,
                TimeSpan.FromSeconds(30), "Konten wurden nicht angelegt.");

            // „Start-Signal verloren" simulieren: das Auslöse-Event DIREKT in den Store schreiben (kein Actor,
            // KEIN Signal) → der Korrelations-Router erfährt nie davon, würde den Prozess also nie starten.
            await store.AppendEventsAsync(auftrag, 0,
                new IEvent[] { new UeberweisungBeauftragt(quelle, ziel, 30) },
                aggregateType: "Ueberweisungsauftrag");

            // Der Prozess ist „gestartet" (Entscheidung durabel) und im Offen-Index vermerkt — aber es existiert
            // KEINE lebende Weckung mehr. Genau der Zustand nach verlorener Selbst-Weckung/Redeploy.
            var korr = ProzessId.Für("UeberweisungsProzess", auftrag, 1);
            await store.AppendEventsAsync(korr, 0,
                new IEvent[] { new ProzessGestartet("UeberweisungsProzess", auftrag, 1) },
                aggregateType: "ProzessManager");
            await offen.MarkiereOffenAsync(korr, "UeberweisungsProzess", default);

            // NICHTS manuell wecken. Allein der Backstop-Scan (15s) über den Offen-Index darf den Prozess
            // wiederbeleben und bis zum Ende treiben.
            await WaitAsync(async () =>
            {
                var log = await store.ReadStreamAsync(korr, 0, default);
                return log.Any(e => e.Payload is ProzessBeendet b && b.Erfolg);
            }, TimeSpan.FromSeconds(60), "Der Backstop hat den orphaned Prozess nicht bis ProzessBeendet getrieben.");

            (await store.LoadStateAsync<Konto>(quelle))!.Saldo.Should().Be(70, "reserviert + gebucht");
            (await store.LoadStateAsync<Konto>(ziel))!.Saldo.Should().Be(30, "gutgeschrieben");

            // Nach dem Ende ist die Korrelation aus dem Offen-Index entfernt (ProzessBeendet → MarkiereBeendet).
            (await offen.ListeOffeneAsync(default)).Should().NotContain(korr,
                "der Offen-Index wird beim ProzessBeendet geräumt");
        }
        finally { await host.StopAsync(); }
    }

    // ── Offen-Index-Vertrag: setzen → gelistet, räumen → nicht mehr gelistet ──────────────────────────

    [Fact]
    public async Task Offen_Index_listet_offene_und_raeumt_beendete()
    {
        var (host, _, _, offen) = Boot();
        await host.StartAsync();
        try
        {
            var korr = Guid.NewGuid();
            await offen.MarkiereOffenAsync(korr, "IrgendeinProzess", default);
            (await offen.ListeOffeneAsync(default)).Should().Contain(korr);

            await offen.MarkiereBeendetAsync(korr, default);
            (await offen.ListeOffeneAsync(default)).Should().NotContain(korr);
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
