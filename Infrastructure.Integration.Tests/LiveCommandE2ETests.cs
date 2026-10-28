using Abstractions;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Domain.Projections.ImagePair;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 2 — der offene LIVE-Durchstich: ein echter Command durch das komplette System.
///
/// Fährt einen In-Process-Host mit GENAU der Host.Grpc-Verdrahtung hoch
/// (AddCqrsFramework + Pipelines + Pull-Pfad), schickt einen echten
/// <see cref="NimmBildAuf"/>-Command über den <see cref="IAggregateDispatcher"/>
/// und prüft, dass die Historie in Postgres wächst.
///
/// Damit läuft die GANZE Kette produktiv:
///   Command → ImagePair-Aggregat → Event in "es" + Signal-Emit → Broker → Receiver →
///   Co-Commit-Adapter → Log-Read → Projektion → Historie-Eintrag in "rm".
///
/// Braucht Postgres (5432) und Consul (8500) — das Dev-Setup.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class LiveCommandE2ETests
{
    [Fact]
    public async Task Echter_Command_laesst_die_Historie_ueber_den_Pull_Pfad_wachsen()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "phase2-live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;                       // kein gRPC-Endpoint nötig
            opts.ClusterName = "phase2-live-" + Guid.NewGuid().ToString("N")[..8];
            // Connection-Defaults zeigen bereits auf localhost (Postgres/Consul laufen).
        });


        // Der zu prüfende Pull-Pfad (koppelt zugleich den Push-Subscriber ab).
        builder.Services.AddGeneratedPullPaths();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            // Stores sind SCOPED (eine Instanz je Fähigkeits-Bereich) → aus einem eigenen Bereich lesen.
            using var bereich = host.Services.CreateScope();
            var historieRead = bereich.ServiceProvider.GetRequiredService<IGetHistorie>();
            var imagePairRead = bereich.ServiceProvider.GetRequiredService<IFindImagePair>();

            var pairId = Guid.NewGuid();
            dispatcher.Dispatch(new CommandEnvelope
            {
                AggregateType = "ImagePair",   // MUSS mit dem ClusterKind übereinstimmen
                Modus = new CommandModus.Client(0),
                Payload = new NimmBildAuf(new(pairId), "PAIR-LIVE", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/live/e2e", BildVersion.Dc0, new BildMeta("dc0.tiff", 1, 1, 1, DateTimeOffset.UtcNow), "/live/e2e" + ".png")
            });

            // Command → Event+Signal → Broker → Receiver → Adapter → Co-Commit ist asynchron.
            await WaitAsync(
                async () => (await historieRead.GetByPairIdAsync(pairId))?.Eintraege.Count >= 1,
                TimeSpan.FromSeconds(30),
                "Historie ist nach dem Command nicht über den Pull-Pfad gewachsen.");

            var historie = await historieRead.GetByPairIdAsync(pairId);
            historie.Should().NotBeNull();
            historie!.Eintraege.Should().Contain(e => e.EreignisTyp == nameof(ImagePairErstellt));

            // (B0) Auch die ImagePairProjektion läuft jetzt über den Pull-Pfad (neuer Co-Commit-Store):
            //   dasselbe Event materialisiert ihr Read-Model in "rm" mit dem richtigen PairKey.
            await WaitAsync(
                async () => (await imagePairRead.FindByIdAsync(pairId)) is { PairKey: "PAIR-LIVE" },
                TimeSpan.FromSeconds(30),
                "ImagePairProjektion ist nach dem Command nicht über den Pull-Co-Commit-Pfad materialisiert.");

            // Plan Phase 6: der Stand je Id, den eine Antwort trägt, kommt aus dem Fortschritt der Projektion (Guard je Stream, mit den
            //   Wirkungen in einer Transaktion) — hier das eine Event des Paars.
            var staende = host.Services.GetRequiredService<IReadModelDepsReader>();
            var stand = await staende.ReadAsync("imagepair-historie", new[] { pairId.ToString() });
            stand.Should().ContainSingle(d => d.Id == pairId && d.Version == 1, "die Projektion hat das Paar bis Version 1 verarbeitet");
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
            try { if (await condition()) return; } catch { /* Store evtl. noch nicht bereit */ }
            await Task.Delay(200);
        }
        throw new TimeoutException(message);
    }
}
