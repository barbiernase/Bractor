using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Infrastructure;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Akteur-Vertrag §9.7 — „doppelt zugestellt ≠ doppelt wirksam" für eine Zusage von AUSSEN, gegen echtes Marten/Consul/Redis.
///
/// Der Python-Klassifizierer antwortet auf <see cref="ImagePairKomplett"/> mit <see cref="KlassifiziereBildPaarDurchKi"/> und nennt dabei
/// das Event (Stream + Version + Typ) und den Index seiner Ausgabe. Der gRPC-Service leitet daraus die CommandId ab
/// (<see cref="AkteurVertragsPruefung.CommandId"/>) und schickt im Emittiert-Modus. Hier wird genau das über den echten Dispatcher
/// gefahren: dieselbe Antwort zweimal (Reconnect/Replay des Events) → EIN Fakt; eine andere Ausgabe (Index 1) → ein zweiter.
///
/// Beweiskraft: der Decider des Paars würde jede Klassifikation annehmen (kein Guard gegen Wiederholung) — dass es bei einem Fakt
/// bleibt, kann nur die Inbox (KommandoVerarbeitet je CommandId) leisten, und die greift nur bei gleicher Id.
/// </summary>
public class VertragsZusageE2ETests
{
    [Fact]
    public async Task Doppelt_zugestellte_Zusage_wirkt_genau_einmal()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "vertrag-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "vertrag-e2e-" + Guid.NewGuid().ToString("N")[..8];
        });
        builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(builder.Services);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var pair = Guid.NewGuid();
            var korrelation = Guid.NewGuid().ToString();

            void Sende(ICommand cmd, CommandModus modus, Guid? id = null) => dispatcher.Dispatch(new CommandEnvelope
            {
                CommandId = id ?? Guid.NewGuid(),
                AggregateId = pair,
                AggregateType = "ImagePair",
                Modus = modus,
                CorrelationId = korrelation,
                UserId = "Klassifizierer",
                Payload = cmd,
            });
            async Task<IReadOnlyList<EventEnvelope>> Stream() => await es.ReadStreamAsync(pair, 0, default);

            // Paar komplett machen (wie KameraSystem → BildEingangPipeline).
            var meta = new BildMeta("a.png", 1, 1, 1, DateTimeOffset.UtcNow);
            Sende(new ErstelleImagePair(pair, "PAIR-VERTRAG", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/vertrag"), new CommandModus.Client(0));
            await Warte(async () => (await Stream()).Count >= 1, "ImagePairErstellt fehlt");
            Sende(new MeldeBildVerfuegbar(pair, BildVersion.Dc0, meta, "dc0.png"), new CommandModus.Emittiert());
            Sende(new MeldeBildVerfuegbar(pair, BildVersion.Dc2, meta, "dc2.png"), new CommandModus.Emittiert());
            await Warte(async () => (await Stream()).Any(e => e.Payload is ImagePairKomplett), "ImagePairKomplett fehlt");
            var komplett = (await Stream()).First(e => e.Payload is ImagePairKomplett);

            // Die Antwort des Klassifizierers — so, wie der gRPC-Service sie aus der Kausalität ableitet.
            Guid Id(int index) => AkteurVertragsPruefung.CommandId(korrelation, pair, komplett.AggregateVersion,
                nameof(ImagePairKomplett), index, typeof(KlassifiziereBildPaarDurchKi), pair);
            var antwort = new KlassifiziereBildPaarDurchKi(pair, Klassifikation.Anomalie);

            Sende(antwort, new CommandModus.Emittiert(), Id(0));
            Sende(antwort, new CommandModus.Emittiert(), Id(0));   // dasselbe Event noch einmal zugestellt → dieselbe Antwort
            Sende(antwort, new CommandModus.Emittiert(), Id(1));   // Kontrolle: eine ANDERE Ausgabe wirkt

            await Warte(async () => (await Stream()).Count(e => e.Payload is BildPaarDurchKiKlassifiziert) >= 2, "Klassifikationen fehlen");
            await Task.Delay(1500);   // Nachzügler abwarten: eine dritte Klassifikation dürfte jetzt nicht mehr kommen
            var klassifiziert = (await Stream()).Where(e => e.Payload is BildPaarDurchKiKlassifiziert).ToList();

            klassifiziert.Should().HaveCount(2, "Id(0) zweimal zugestellt = einmal wirksam, Id(1) ist eine eigene Ausgabe");
            klassifiziert.Should().OnlyContain(e => e.UserId == "Klassifizierer", "der Vertrags-Akteur reist als Header mit");
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    private static async Task Warte(Func<Task<bool>> bedingung, string meldung)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try { if (await bedingung()) return; } catch { /* Store/Cluster evtl. noch nicht bereit */ }
            await Task.Delay(200);
        }
        throw new TimeoutException(meldung);
    }
}
