using Abstractions;
using Domain.Konto;
using Domain.Pipeline.Infrastructure;   // AddDomainPipelineServices
using FluentAssertions;
using Infrastructure.Aggregate;         // AggregateRehydrator
using Infrastructure.Extensions;
using Infrastructure.Pipeline;          // GeneratedPipelines
using Infrastructure.Persistence;       // GeneratedSnapshotSchema
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// S4-Live-Tor, die vollständige Schleife (docs/snapshot-konzept.md §9): ein ECHTER Cluster-Actor wird über
/// einen kleinen Schwellwert getrieben, schreibt einen Snapshot nach Postgres, und eine spätere Rehydration
/// seedet NACHWEISLICH aus dem Snapshot (SnapshotVersion > 0, Tail statt Voll-Replay) — mit korrektem Endzustand.
///
/// Threshold hier = 3 (Tests setzen ihn klein; produktiv 200). Braucht Postgres/Redis/Consul (Dev-Setup).
/// </summary>
public class SnapshotLiveE2ETests
{
    [Fact]
    public async Task Actor_schreibt_Snapshot_und_spaetere_Rehydration_seedet_daraus()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "snap-live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "snap-live-" + Guid.NewGuid().ToString("N")[..8];
            opts.SnapshotThreshold = 3;    // klein: der Snapshot-Pfad soll billig auslösen
        });
        // Der ActorSystem-Build registriert Pipeline-Kinds → Pipeline-Services müssen da sein (wie Saga-Tests).
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var store      = host.Services.GetRequiredService<IEventStoreRepository>();
            var snapshots  = host.Services.GetRequiredService<ISnapshotStore>();
            var factory    = host.Services.GetRequiredService<IAggregateHandlerFactory>();

            var kontoId = Guid.NewGuid();

            // Drei Commands auf DENSELBEN Stream (AnyVersion = Envelope-Default) → Saldo 1000 → 1010 → 1030.
            dispatcher.Dispatch(Env(kontoId, new EroeffneKonto(kontoId, 1000)));
            dispatcher.Dispatch(Env(kontoId, new SchreibeGut(kontoId, 10)));
            dispatcher.Dispatch(Env(kontoId, new SchreibeGut(kontoId, 20)));

            // 1. Warten, bis alle drei angekommen sind — Voll-Replay (ohne Snapshot) zeigt 1030.
            //    ⚠ Flaky-Hinweis: dieser Schritt prüft PERSISTIERTE Events (Append, vor jedem Publish). Er
            //    hängt gelegentlich bimodal (~1s grün ODER Boot-Hang > Timeout) — Ursache ist die Consul-
            //    Cluster-Formation / erste Actor-Aktivierung bei Cold-Boot, NICHT die Command-Verarbeitung.
            //    Ein Timeout kann einen Boot-Hang nicht heilen; siehe CLAUDE.md (Suite sequentiell/isoliert).
            await WaitAsync(async () =>
                (await AggregateRehydrator.LoadAsync<Konto>(kontoId, null, store, factory, null, default))
                    .State.Saldo == 1030m,
                TimeSpan.FromSeconds(30), "Die drei Konto-Commands sind nicht bis Saldo 1030 gelaufen (Cluster-Boot-Hang?).");

            // 2. Warten, bis der (fire-and-forget) Snapshot in Postgres liegt. Out-of-band + best-effort
            //    (Invariante 6): sobald Schritt 1 durch ist (Cluster warm), landet der Write schnell.
            await WaitAsync(async () =>
                await snapshots.TryLoadAsync<Konto>(kontoId, default) is { Version: > 0 },
                TimeSpan.FromSeconds(15), "Der Actor hat keinen Snapshot nach Postgres geschrieben.");

            var snap = await snapshots.TryLoadAsync<Konto>(kontoId, default);
            snap.Should().NotBeNull();
            snap!.Version.Should().BeGreaterThan(0);
            snap.State.Saldo.Should().BeLessThan(1030m);        // TEIL-Snapshot (vor dem letzten Command)
            snap.SchemaVersion.Should().Be(GeneratedSnapshotSchema.VersionOf<Konto>());

            // 3. Read-Pfad: Rehydration MIT Snapshot seedet aus ihm (SnapshotVersion == Snapshot-Version)
            //    und der Tail vervollständigt zum korrekten Endzustand.
            var mitSnapshot = await AggregateRehydrator.LoadAsync<Konto>(
                kontoId, snapshots, store, factory, GeneratedSnapshotSchema.VersionOf<Konto>(), default);

            mitSnapshot.SnapshotVersion.Should().Be(snap.Version);   // NICHT ab 0 gefaltet
            mitSnapshot.State.Saldo.Should().Be(1030m);              // Snapshot + Tail == voll
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    private static CommandEnvelope Env(Guid id, ICommand payload)
        => new() { AggregateId = id, AggregateType = "Konto", Modus = new CommandModus.Emittiert(), Payload = payload };

    private static async Task WaitAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await condition()) return; } catch { /* Store evtl. noch nicht bereit */ }
            await Task.Delay(200);
        }
        throw new TimeoutException(message);
    }
}
