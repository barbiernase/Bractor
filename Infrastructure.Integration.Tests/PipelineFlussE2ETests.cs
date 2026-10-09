using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Abstractions;
using Domain.Bildaufbereitung;
using Domain.ImagePair;
using Domain.Pipeline.Infrastructure;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.GrpcClient;
using Infrastructure.Pipeline;
using Infrastructure.Projections.Generated;
using Infrastructure.Prozess;
using Infrastructure.Quellen;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — PIPELINE ALS FLUSS gegen echtes Marten/Consul/Redis (docs/konzept-editor-pipelines.md §14.5): der Bildeingang der Domäne
/// läuft über die ganze Kette —
///
///   Quell-Nachricht ──QuellEingang (StartStream je Kennung)──▶ Dirigent (ProzessManager-Actor je Vorgang)
///     ──Auftrag──▶ Vermittler (Actor je Funktion) ◀──Pull── FunktionsAbholer (C#) bzw. Python-Worker (gRPC)
///     ──Ergebnis genau einmal im Ausführungs-Stream──▶ Dirigent ──Commands──▶ ImagePair-Aggregat.
///
/// Beweiskraft: das Paar bekommt BildVerfuegbar mit einem Pfad, den NUR die Funktionskette erzeugt (Verkleinern → Ausgleichen); die
/// Funktions-Ergebnisse stehen als Events im Log (Marten), der Manager-Log endet mit ProzessBeendet(Erfolg). Dieselbe Datei zweimal
/// aufgenommen startet genau einen Vorgang. Im zweiten Test rechnet die Verkleinerung ein Python-Worker, der sich über gRPC anmeldet
/// und Aufträge holt — sein Kennzeichen („_py") muss im Fakt landen.
/// </summary>
public class PipelineFlussE2ETests
{
    private sealed class Verkleinerung : IBildVerkleinerung
    {
        public Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild a, IAusfuehrung x)
            => Task.FromResult<OneOf<BildVerkleinert, BildNichtLesbar>>(new BildVerkleinert(a.QuellPfad + "_cs.png", 683, a.Hoehe));
    }

    private sealed class Ausgleich : IHistogrammAusgleich
    {
        public Task<OneOf<HistogrammAusgeglichen, BildNichtLesbar>> RufeAsync(GleicheHistogrammAus a, IAusfuehrung x)
            => Task.FromResult<OneOf<HistogrammAusgeglichen, BildNichtLesbar>>(
                new HistogrammAusgeglichen(a.QuellPfad + "_aus.png", 683, 512, DateTimeOffset.UtcNow));
    }

    /// <summary>Die Host-Verdrahtung wie Host.Grpc: Framework, Prozesse/Flüsse, Funktionen — die Bildaufbereitung als Fakes.</summary>
    private static void Verdrahte(IServiceCollection s, string watchDir, string cluster, bool grpc, bool verkleinerungExtern)
    {
        s.AddCqrsFramework(opts => { opts.EnableGrpc = grpc; opts.ClusterName = cluster; });
        s.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
        GeneratedPipelines.RegisterAllPipelines(s);
        s.AddGeneratedProzesse();
        if (verkleinerungExtern) s.AddExterneFunktion<IBildVerkleinerung>();
        else s.AddFunktion<IBildVerkleinerung, Verkleinerung>(slots: 2);
        s.AddFunktion<IHistogrammAusgleich, Ausgleich>(slots: 2);
        s.AddFunktion<IDateinameDeutung, Domain.Pipeline.ImageProcessing.ImagePairDateinameDeutung>(slots: 4);
        s.AddFunktion<Domain.Pipeline.Datensatz.IRangeSuche, Domain.Pipeline.Datensatz.RangeSuche>();
        s.AddFunktion<Domain.Pipeline.Datensatz.IMitgliederEinfrieren, Domain.Pipeline.Datensatz.MitgliederEinfrieren>();
    }

    private static DateiErkannt Datei(string watchDir, DateTimeOffset t, BildVersion v)
    {
        var name = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_{v.ToString().ToUpperInvariant()}.tiff";
        return new DateiErkannt(Path.Combine(watchDir, name), name, 4711, t);
    }

    [Fact]
    public async Task Bildeingang_Quelle_bis_Fakt_ueber_Dirigent_Vermittler_und_Abholer()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "fluss-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, watchDir, "fluss-e2e-" + Guid.NewGuid().ToString("N")[..8], grpc: false, verkleinerungExtern: false);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var eingang = QuellEingang.Live(host.Services);
            var t = new DateTimeOffset(2026, 10, 8, 12, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
            var datei = Datei(watchDir, t, BildVersion.Dc0);
            var paar = ImagePairFileName.Resolve(datei.Dateiname)!.AggregateId;

            var vorgang = await eingang.NimmAufAsync(datei, default);
            (await eingang.NimmAufAsync(datei with { }, default)).Should().Be(vorgang, "dieselbe Datei startet genau einen Vorgang");

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(60),
                "Das Paar hat kein BildVerfuegbar bekommen — die Kette Quelle → Dirigent → Funktionen → Command lief nicht durch.");

            var stream = await es.ReadStreamAsync(paar, 0, default);
            stream.Should().ContainSingle(e => e.Payload is ImagePairErstellt);
            var bild = stream.Select(e => e.Payload).OfType<BildVerfuegbar>().Single();
            bild.Pfad.Should().EndWith("_cs.png_aus.png", "der Pfad entsteht nur durch Verkleinern (C#) und Ausgleichen — in dieser Reihenfolge");

            // Der Dirigent: Vorgang erfolgreich beendet (Manager-Log), die Funktions-Ergebnisse stehen im Log.
            var korrelation = ProzessId.Für(nameof(Bildeingang), vorgang, 1);
            await Warte(async () => (await es.ReadStreamAsync(korrelation, 0, default)).Any(e => e.Payload is ProzessBeendet), TimeSpan.FromSeconds(30),
                "Der Dirigent hat den Vorgang nicht beendet.");
            (await es.ReadStreamAsync(korrelation, 0, default)).Select(e => e.Payload).OfType<ProzessBeendet>().Single().Erfolg.Should().BeTrue();

            var marten = host.Services.GetRequiredService<IDocumentStore>();
            await using var session = marten.QuerySession();
            var typen = (await session.Events.QueryAllRawEvents().Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-5)).ToListAsync())
                .Select(e => e.Data).ToList();
            typen.OfType<ImagePairDateiGedeutet>().Should().Contain(g => g.PaarId == paar, "das Ergebnis der Deutung liegt im Ausführungs-Stream");
            typen.OfType<BildVerkleinert>().Should().Contain(v => v.Pfad == datei.Pfad + "_cs.png");
            typen.OfType<HistogrammAusgeglichen>().Should().Contain(h => h.Pfad == datei.Pfad + "_cs.png_aus.png");
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    [Fact]
    public async Task Bildeingang_mit_Python_Worker_der_die_Verkleinerung_ueber_gRPC_anbietet()
    {
        var python = Environment.GetEnvironmentVariable("CQRS_PYTHON") ?? "/opt/miniconda3/envs/gpu_env/bin/python";
        File.Exists(python).Should().BeTrue($"Python für den Worker fehlt ({python}) — CQRS_PYTHON setzen");
        var skript = Path.Combine(RepoWurzel(), "Infrastructure.Integration.Tests", "Python", "funktions_worker.py");

        var watchDir = Path.Combine(Path.GetTempPath(), "fluss-py-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var port = FreierPort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port, l => l.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        Verdrahte(builder.Services, watchDir, "fluss-py-" + Guid.NewGuid().ToString("N")[..8], grpc: true, verkleinerungExtern: true);
        var app = builder.Build();
        app.MapCqrsGrpcService();
        await app.StartAsync();

        var ausgabe = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var worker = new Process
        {
            StartInfo = new ProcessStartInfo(python, $"\"{skript}\" {port}")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(skript)!,
            },
        };
        worker.OutputDataReceived += (_, e) => { if (e.Data != null) ausgabe.Enqueue(e.Data); };
        worker.ErrorDataReceived += (_, e) => { if (e.Data != null) ausgabe.Enqueue(e.Data); };
        try
        {
            worker.Start();
            worker.BeginOutputReadLine();
            worker.BeginErrorReadLine();
            await Warte(() => Task.FromResult(ausgabe.Any(z => z.Contains("Funktionen angeboten"))), TimeSpan.FromSeconds(60),
                "Der Python-Worker hat sich nicht angemeldet:\n" + string.Join("\n", ausgabe));

            var es = app.Services.GetRequiredService<IEventStoreRepository>();
            var t = new DateTimeOffset(2026, 10, 9, 8, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
            var datei = Datei(watchDir, t, BildVersion.Dc2);
            var paar = ImagePairFileName.Resolve(datei.Dateiname)!.AggregateId;
            await QuellEingang.Live(app.Services).NimmAufAsync(datei, default);

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(90),
                "Kein BildVerfuegbar — der Python-Worker hat den Auftrag nicht geholt/beantwortet:\n" + string.Join("\n", ausgabe));

            var bild = (await es.ReadStreamAsync(paar, 0, default)).Select(e => e.Payload).OfType<BildVerfuegbar>().Single();
            bild.Pfad.Should().Be(datei.Pfad + "_py.png_aus.png", "die Verkleinerung hat der Python-Worker gerechnet (gRPC: Angebot → Auftrag → Ergebnis)");
            ausgabe.Should().Contain(z => z.StartsWith("RUFE " + datei.Pfad), "der Worker hat genau diesen Auftrag ausgeführt");
        }
        finally
        {
            try { if (!worker.HasExited) worker.Kill(entireProcessTree: true); } catch { /* egal */ }
            await app.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    /// <summary>
    /// Der Trainings-Wächter (Warten am Strom gegen ein Zeitlimit) live: TrainingBegonnen startet den Fluss, das Ende im SELBEN Strom
    /// (vom Dirigenten nachgelesen, geweckt über den §3-Backstop) beendet den Vorgang erfolgreich — ohne Markierung, ohne Frist.
    /// </summary>
    [Fact]
    public async Task Trainings_Waechter_endet_still_wenn_das_Training_im_Strom_endet()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "fluss-tw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, watchDir, "fluss-tw-" + Guid.NewGuid().ToString("N")[..8], grpc: false, verkleinerungExtern: false);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var lauf = Guid.NewGuid();
            void Sende(ICommand c) => dispatcher.Dispatch(new CommandEnvelope
            { AggregateId = lauf, AggregateType = "Trainingslauf", Modus = new CommandModus.Emittiert(), Payload = c });

            Sende(new Domain.Trainingslauf.StarteTraining(lauf, Guid.NewGuid(), 1, new Domain.Trainingslauf.Hyperparameter(1, 0.1, 8, "r", 1)));
            await Warte(async () => (await es.ReadStreamAsync(lauf, 0, default)).Count >= 1, TimeSpan.FromSeconds(30), "Training nicht angelegt");
            Sende(new Domain.Trainingslauf.MeldeTrainingBegonnen(lauf));
            await Warte(async () => (await es.ReadStreamAsync(lauf, 0, default)).Any(e => e.Payload is Domain.Trainingslauf.TrainingBegonnen),
                TimeSpan.FromSeconds(30), "TrainingBegonnen fehlt");
            var begonnen = (await es.ReadStreamAsync(lauf, 0, default)).First(e => e.Payload is Domain.Trainingslauf.TrainingBegonnen);
            var korrelation = ProzessId.Für(nameof(Domain.Trainingslauf.TrainingWaechter), lauf, begonnen.AggregateVersion);
            await Warte(async () => (await es.ReadStreamAsync(korrelation, 0, default)).Any(e => e.Payload is ProzessGestartet),
                TimeSpan.FromSeconds(60), "Der Wächter-Fluss ist nicht gestartet (Auf<TrainingBegonnen>).");

            Sende(new Domain.Trainingslauf.MeldeTrainingAbgeschlossen(lauf, "/modelle/m.pt", new Domain.Trainingslauf.Endmetriken(0.1, 0.9)));
            await Warte(async () => (await es.ReadStreamAsync(korrelation, 0, default)).Any(e => e.Payload is ProzessBeendet),
                TimeSpan.FromSeconds(60), "Der Wächter hat das Ende im Strom nicht gesehen (Warten + Backstop).");

            (await es.ReadStreamAsync(korrelation, 0, default)).Select(e => e.Payload).OfType<ProzessBeendet>().Single().Erfolg.Should().BeTrue();
            (await es.ReadStreamAsync(lauf, 0, default)).Should().NotContain(e => e.Payload is Domain.Trainingslauf.TrainingHaengengeblieben,
                "das Ende kam vor dem Zeitlimit — keine Markierung");
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    /// <summary>
    /// Der Datensatz-Resolver live: FuegeRangeHinzu → RangeAngefordert → ƒ Range-Suche mit der ECHTEN Lese-Fähigkeit
    /// (ISearchImagePairs über einen Fähigkeits-Bereich) → NimmRangeAuf an DENSELBEN Datensatz (Id aus dem Strom der Quelle).
    /// </summary>
    [Fact]
    public async Task Datensatz_Range_wird_mit_echter_Lese_Faehigkeit_aufgeloest()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "fluss-ds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, watchDir, "fluss-ds-" + Guid.NewGuid().ToString("N")[..8], grpc: false, verkleinerungExtern: false);
        builder.Services.AddGeneratedPullPaths();   // die ImagePair-Projektion füllt das Read-Model, das die Suche liest
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            void Sende(Guid id, string typ, ICommand c) => dispatcher.Dispatch(new CommandEnvelope
            { AggregateId = id, AggregateType = typ, Modus = new CommandModus.Emittiert(), Payload = c });

            var paar = Guid.NewGuid();
            Sende(paar, "ImagePair", new ErstelleImagePair(paar, "PAIR-RANGE-" + paar.ToString("N")[..6], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/range"));
            using (var bereich = host.Services.CreateScope())
            {
                var finde = bereich.ServiceProvider.GetRequiredService<Domain.Projections.IFindImagePair>();
                await Warte(async () => await finde.FindByIdAsync(paar) is not null, TimeSpan.FromSeconds(30), "Read-Model des Paars fehlt");
            }

            var ds = Guid.NewGuid();
            Sende(ds, "Datensatz", new Domain.Datensatz.ErstelleDatensatz(ds, "Range-E2E"));
            await Warte(async () => (await es.ReadStreamAsync(ds, 0, default)).Count >= 1, TimeSpan.FromSeconds(30), "Datensatz nicht angelegt");
            Sende(ds, "Datensatz", new Domain.Datensatz.FuegeRangeHinzu(ds, new Domain.Datensatz.RangeKriterien()));

            await Warte(async () => (await es.ReadStreamAsync(ds, 0, default)).Any(e => e.Payload is Domain.Datensatz.PaareAufgenommen),
                TimeSpan.FromSeconds(60), "Die Range wurde nicht aufgelöst (Fluss DatensatzRangeAufloesung).");
            var aufgenommen = (await es.ReadStreamAsync(ds, 0, default)).Select(e => e.Payload).OfType<Domain.Datensatz.PaareAufgenommen>().Single();
            aufgenommen.ImagePairIds.Should().Contain(paar, "die Suche lief gegen das echte Read-Model");
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
        }
    }

    private static int FreierPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static string RepoWurzel()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "CqrsSolution.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("CqrsSolution.sln nicht gefunden");
    }

    private static async Task Warte(Func<Task<bool>> bedingung, TimeSpan timeout, string meldung)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try { if (await bedingung()) return; } catch { /* Store/Cluster evtl. noch nicht bereit */ }
            await Task.Delay(250);
        }
        throw new TimeoutException(meldung);
    }
}
