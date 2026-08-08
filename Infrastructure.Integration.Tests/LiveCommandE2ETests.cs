using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Infrastructure;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 2 — der offene LIVE-Durchstich: ein echter Command durch das komplette System.
///
/// Fährt einen In-Process-Host mit GENAU der Host.Grpc-Verdrahtung hoch
/// (AddCqrsFramework + Pipelines + Pull-Pfad), schickt einen echten
/// <see cref="ErstelleImagePair"/>-Command über den <see cref="IAggregateDispatcher"/>
/// und prüft, dass die Historie in Postgres wächst.
///
/// Damit läuft die GANZE Kette produktiv:
///   Command → ImagePair-Aggregat → Event in "es" + Signal-Emit → Broker → Receiver →
///   Co-Commit-Adapter → Log-Read → Projektion → Historie-Eintrag in "rm".
///
/// Braucht Postgres (5432), Redis (6379) und Consul (8500) — das Dev-Setup.
/// </summary>
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
            // Connection-Defaults zeigen bereits auf localhost (Postgres/Redis/Consul laufen).
        });

        // Der ActorSystem-Build registriert Pipeline-Kinds → Pipeline-Services müssen da sein.
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);

        // Der zu prüfende Pull-Pfad (koppelt zugleich den Push-Subscriber ab).
        builder.Services.AddGeneratedPullPaths();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            // Diagnose Phase 5: ist der Deps-Sink registriert?
            host.Services.GetService<Core.IReadModelDepsSink>()
                .Should().NotBeNull("IReadModelDepsSink muss für den Pull-Deps-Index registriert sein");

            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var historieRead = host.Services.GetRequiredService<IImagePairHistorieReadStore>();
            var imagePairRead = host.Services.GetRequiredService<IImagePairReadStore>();

            var pairId = Guid.NewGuid();
            dispatcher.Dispatch(new CommandEnvelope
            {
                AggregateId = pairId,
                AggregateType = "ImagePair",   // MUSS mit dem ClusterKind übereinstimmen
                Modus = new CommandModus.Client(0),
                Payload = new ErstelleImagePair(
                    pairId,
                    PairKey: "PAIR-LIVE",
                    ProduziertAm: DateTimeOffset.UtcNow,
                    AufgenommenAm: DateTimeOffset.UtcNow,
                    UrsprungsPfad: "/live/e2e")
            });

            // Command → Event+Signal → Broker → Receiver → Adapter → Co-Commit ist asynchron.
            await WaitAsync(
                async () => (await historieRead.GetByPairIdAsync(pairId))?.Eintraege.Count >= 1,
                TimeSpan.FromSeconds(30),
                "Historie ist nach dem Command nicht über den Pull-Pfad gewachsen.");

            var historie = await historieRead.GetByPairIdAsync(pairId);
            historie.Should().NotBeNull();
            historie!.Eintraege.Should().Contain(e => e.EreignisTyp == nameof(ImagePairErstellt));

            // (B0) Auch die ImagePairProjection läuft jetzt über den Pull-Pfad (neuer Co-Commit-Store):
            //   dasselbe Event materialisiert ihr Read-Model in "rm" mit dem richtigen PairKey.
            await WaitAsync(
                async () => (await imagePairRead.FindByIdAsync(pairId)) is { PairKey: "PAIR-LIVE" },
                TimeSpan.FromSeconds(30),
                "ImagePairProjection ist nach dem Command nicht über den Pull-Co-Commit-Pfad materialisiert.");

            // Phase 5: der Deps-Index (rm:) wird auch auf dem PULL-Pfad geführt (früher fiel er weg).
            var redis = host.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
            var depsKey = $"rm:imagepair-historie:{pairId}";
            await WaitAsync(
                async () => (await redis.GetDatabase().HashGetAllAsync(depsKey)).Length > 0,
                TimeSpan.FromSeconds(10),
                "Deps-Index (rm:) wurde auf dem Pull-Pfad nicht geführt.");
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
