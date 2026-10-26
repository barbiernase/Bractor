using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Bildaufbereitung;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.GrpcClient;
using Infrastructure.Projections.Generated;
using Infrastructure.Quellen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — die ABLÄUFE der Beispieldomäne gegen echtes Marten/Consul/Redis (Plan Pub/Sub-Neustrukturierung Phase 4): Aggregate entscheiden,
/// Funktionen tragen die Kanten Event → Command, Fristen tragen die Zeit — ohne Dirigent. Der Bildeingang läuft über die ganze Kette —
///
///   Quell-Nachricht ──QuellEingang (StartStream je Kennung, Commit gemeldet)──▶ ƒ IBildaufbereitung (OpenCV im Host bzw. Python-Worker über
///     gRPC, Ausgabe festgehalten) ──BildAufbereitet──▶ ƒ Bildeingang (gleichbleibend) ──NimmBildAuf──▶ ImagePair („anlegen oder ergänzen“).
///
/// Beweiskraft: das Paar bekommt BildVerfuegbar mit einer Vorschau, die NUR die Bildaufbereitung erzeugt; die Spur führt lückenlos bis zur Quelle;
/// dieselbe Datei zweimal steht genau einmal im Log. Im zweiten Test rechnet ein Python-Worker, der die Funktion über gRPC anbietet — sein
/// Kennzeichen („_py") muss im Fakt landen.
/// </summary>
public class AblaufE2ETests
{
    /// <summary>Die Host-Verdrahtung wie Host.Grpc: Framework (samt Funktionen und Uhr), Projektionen, Dienste mit Konfig.</summary>
    private static void Verdrahte(IServiceCollection s, string ausgabe, string cluster, bool grpc, FunktionsLaufort bildaufbereitung)
    {
        s.AddCqrsFramework(opts => { opts.EnableGrpc = grpc; opts.ClusterName = cluster; });
        s.AddGeneratedPullPaths();   // die Projektionen füllen die Read-Models, die die Datensatz-Funktionen lesen
        s.AddSingleton(new BausteinBindung(nameof(IBildaufbereitung), bildaufbereitung, Slots: 2, Wiederholungen: 1));
        var konfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{Dienstvertrag.KonfigAbschnitt}:{nameof(BildaufbereitungKonfig)}:{nameof(BildaufbereitungKonfig.AusgabePfad)}"] = ausgabe,
        }).Build();
        Infrastructure.Dienste.DienstExtensions.BindeDienste(s, new Dictionary<string, DienstKonfig>(), konfig.GetSection(Dienstvertrag.KonfigAbschnitt),
            Infrastructure.Komposition.Anwendung.Dienste.Namen, Infrastructure.Komposition.Anwendung.Dienste.Implementierungen,
            Infrastructure.Komposition.Anwendung.Dienste.Braucht, Infrastructure.Komposition.Anwendung.Dienste.Konfigs);
    }

    /// <summary>Ein echtes, kleines TIFF (OpenCV liest es) im Eingangs-Verzeichnis.</summary>
    private static DateiErkannt Datei(string verzeichnis, DateTimeOffset t, BildVersion v)
    {
        var name = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_{v.ToString().ToUpperInvariant()}.tiff";
        var pfad = Path.Combine(verzeichnis, name);
        using (var bild = new Mat(48, 64, MatType.CV_8UC3, new Scalar(30, 120, 200))) Cv2.ImWrite(pfad, bild);
        return new DateiErkannt
        {
            Name = name, Pfad = pfad, RelativerPfad = name, Groesse = new FileInfo(pfad).Length, GeaendertAm = t, ErkanntAm = t,
            Kennung = $"{name}|{new FileInfo(pfad).Length}",
        };
    }

    private static DateTimeOffset Zufallszeit(int tag) =>
        new DateTimeOffset(2026, 10, tag, 8, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));

    [Fact]
    public async Task Bildeingang_Quelle_bis_Fakt_ueber_Bildaufbereitung_und_Bildeingang()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "ablauf-e2e-" + Guid.NewGuid().ToString("N"));
        var ausgabe = Path.Combine(verzeichnis, "vorschau");
        Directory.CreateDirectory(verzeichnis);
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, ausgabe, "ablauf-e2e-" + Guid.NewGuid().ToString("N")[..8], grpc: false, FunktionsLaufort.Host);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var eingang = QuellEingang.Live(host.Services);
            var t = Zufallszeit(8);
            var dc0 = Datei(verzeichnis, t, BildVersion.Dc0);
            var dc2 = Datei(verzeichnis, t, BildVersion.Dc2);
            var paar = ImagePairFileName.Resolve(dc0.Name)!.AggregateId.Wert;

            var vorgang = await eingang.NimmAufAsync(dc0, default);
            (await eingang.NimmAufAsync(dc0 with { }, default)).Should().Be(vorgang, "dieselbe Datei steht genau einmal im Log");
            await eingang.NimmAufAsync(dc2, default);

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairKomplett), TimeSpan.FromSeconds(60),
                "Das Paar wurde nicht komplett — die Kette Quelle → Bildaufbereitung → Bildeingang → ImagePair lief nicht durch.");
            await Task.Delay(TimeSpan.FromSeconds(2));

            var stream = await es.ReadStreamAsync(paar, 0, default);
            stream.Should().ContainSingle(e => e.Payload is ImagePairErstellt);
            stream.Should().ContainSingle(e => e.Payload is ImagePairKomplett);
            var bilder = stream.Select(e => e.Payload).OfType<BildVerfuegbar>().ToList();
            bilder.Select(b => b.Version).Should().BeEquivalentTo(new[] { BildVersion.Dc0, BildVersion.Dc2 });
            bilder.Single(b => b.Version == BildVersion.Dc0).Pfad.Should().Be(Path.Combine(ausgabe, Path.GetFileNameWithoutExtension(dc0.Name) + "_vorschau.png"),
                "die Vorschau entsteht nur durch die Bildaufbereitung (OpenCV im Host)");
            File.Exists(bilder[0].Pfad).Should().BeTrue();
            bilder.Single(b => b.Version == BildVersion.Dc0).Meta.HoehePixel.Should().Be(OpenCvBildaufbereitung.VorschauHoehe);

            // Spur: von BildVerfuegbar läuft eine lückenlose Ursache-Kette über beide Funktionen bis zur Quelle; jeder Eintrag trägt seinen Wirker.
            var bildEnv = stream.First(e => e.Payload is BildVerfuegbar);
            bildEnv.Wirker.Should().Be("decide:ImagePair/NimmBildAuf");
            bildEnv.Ueber.Should().Be("funktion:Bildeingang", "der Absender des Commands ist die Funktion, die ihn ausgab");
            var kette = new List<EventEnvelope> { bildEnv };
            var schritt = bildEnv;
            while (schritt.Ursache is { } ursache && kette.Count < 20)
            {
                Spur.ZerlegeUrsache(ursache, out var s, out var v).Should().BeTrue($"'{ursache}' ist eine Ursache");
                schritt = (await es.ReadStreamAsync(s, (int)v, default)).First(e => e.AggregateVersion == v);
                kette.Add(schritt);
            }
            schritt.Payload.Should().BeOfType<DateiErkannt>("die Kette endet an der Quelle: " + string.Join(" ← ", kette.Select(e => e.Wirker)));
            schritt.Wirker.Should().Be("quelle:DateiErkannt");
            kette.Should().Contain(e => e.Payload is BildAufbereitet && e.Wirker == "funktion:IBildaufbereitung", "die Aufbereitung ist festgehalten");

            // Genau so viel Log je Datei: die Quell-Nachricht, die festgehaltene Aufbereitung (Ausgabe + Abschluss), kein Prozess-Stream.
            var festgehalten = await es.ReadStreamAsync(FunktionsIds.Festhalten(nameof(IBildaufbereitung), vorgang, 1), 0, default);
            festgehalten.Select(e => e.Payload.GetType().Name).Should().Equal(nameof(BildAufbereitet), nameof(AusgabeFestgehalten));
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* egal */ }
        }
    }

    [Fact]
    public async Task Bildeingang_mit_Python_Worker_der_die_Bildaufbereitung_ueber_gRPC_traegt()
    {
        // Der Interpreter der Sim-Umgebung (Umgebung:Python, sonst CQRS_PYTHON/python3) — dieselbe Regel wie die Werkbank.
        var python = Infrastructure.Umgebung.UmgebungLader.Lade(new ConfigurationManager(), "sim-main", []).Python();
        (python == "python3" || File.Exists(python)).Should().BeTrue($"Python für den Worker fehlt ({python}) — Umgebung:Python oder CQRS_PYTHON setzen");
        // Der GENERISCHE Worker (konzept-werkbank §12.3 E13) mit der Tabelle aus dem Codegen; die Test-Implementierung (funktionen/bildaufbereitung.py,
        //   Vorschau „_py") liegt per --pfad vor der des Projekts.
        var skript = Path.Combine(RepoWurzel(), Projektstruktur.PythonWorker(Projektstruktur.StandardWurzel), "funktions_worker.py");
        var testImpls = Path.Combine(RepoWurzel(), "Infrastructure.Integration.Tests", "Python");

        var verzeichnis = Path.Combine(Path.GetTempPath(), "ablauf-py-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(verzeichnis);
        var port = FreierPort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port, l => l.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        Verdrahte(builder.Services, Path.Combine(verzeichnis, "vorschau"), "ablauf-py-" + Guid.NewGuid().ToString("N")[..8], grpc: true,
            FunktionsLaufort.Extern);
        var app = builder.Build();
        app.MapCqrsGrpcService();
        await app.StartAsync();

        var ausgabe = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var worker = new Process
        {
            StartInfo = new ProcessStartInfo(python, $"\"{skript}\" --funktionen IBildaufbereitung --slots 2 --pfad \"{testImpls}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(skript)!,
                Environment = { ["CQRS_GRPC_HOST"] = "127.0.0.1", ["CQRS_GRPC_PORT"] = port.ToString() },
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
            var datei = Datei(verzeichnis, Zufallszeit(9), BildVersion.Dc2);
            var paar = ImagePairFileName.Resolve(datei.Name)!.AggregateId.Wert;
            await QuellEingang.Live(app.Services).NimmAufAsync(datei, default);

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(90),
                "Kein BildVerfuegbar — der Python-Worker hat den Eingang nicht geholt/beantwortet:\n" + string.Join("\n", ausgabe));

            var bild = (await es.ReadStreamAsync(paar, 0, default)).Select(e => e.Payload).OfType<BildVerfuegbar>().Single();
            bild.Pfad.Should().Be(datei.Pfad + "_py.png", "die Bildaufbereitung hat der Python-Worker gerechnet (gRPC: Angebot → Eingang → Antwort)");
            ausgabe.Should().Contain(z => z.StartsWith("AUF " + datei.Pfad), "der Worker hat genau diesen Eingang beantwortet");
        }
        finally
        {
            try { if (!worker.HasExited) worker.Kill(entireProcessTree: true); } catch { /* egal */ }
            await app.StopAsync();
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* egal */ }
        }
    }

    /// <summary>
    /// Der Trainings-Wächter ist eine FRIST des Aggregats: der Beginn entscheidet sie mit (Framework-Event im Stream des Laufs, Fälligkeit in derselben
    /// Transaktion). Ein Ende davor macht sie wirkungslos; das Feuern selbst prüft das Verhaltens-Netz mit der Uhr der Sim.
    /// </summary>
    [Fact]
    public async Task Der_Beginn_eines_Trainings_setzt_die_Waechterfrist_im_Stream_des_Laufs()
    {
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, Path.GetTempPath(), "ablauf-frist-" + Guid.NewGuid().ToString("N")[..8], grpc: false, FunktionsLaufort.Host);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var lauf = Guid.NewGuid();
            void Sende(ICommand c) => dispatcher.Dispatch(new CommandEnvelope
            { AggregateType = "Trainingslauf", Modus = new CommandModus.Emittiert(), Payload = c });

            Sende(new Domain.Trainingslauf.StarteTraining(new(lauf), new(Guid.NewGuid()), 1, new Domain.Trainingslauf.Hyperparameter(1, 0.1, 8, "r", 1)));
            await Warte(async () => (await es.ReadStreamAsync(lauf, 0, default)).Count >= 1, TimeSpan.FromSeconds(30), "Training nicht angelegt");
            Sende(new Domain.Trainingslauf.MeldeTrainingBegonnen(new(lauf)));
            await Warte(async () => (await es.ReadStreamAsync(lauf, 0, default)).Any(e => e.Payload is FristGesetzt), TimeSpan.FromSeconds(30),
                "Der Beginn hat keine Frist gesetzt.");

            var frist = (await es.ReadStreamAsync(lauf, 0, default)).Select(e => e.Payload).OfType<FristGesetzt>().Single();
            frist.Befehl.Should().BeOfType<Domain.Trainingslauf.MarkiereAlsHaengengeblieben>().Which.AggregateId.Wert.Should().Be(lauf);
            frist.Nach.Should().Be(TimeSpan.FromHours(6));
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>
    /// Der Datensatz-Resolver live: FuegeRangeHinzu → RangeAngefordert → ƒ RangeAufloesung mit der ECHTEN Lese-Fähigkeit (ISearchImagePairs über
    /// einen Fähigkeits-Bereich) → NimmRangeAuf an DENSELBEN Datensatz (der Stream des Eingangs). Die Ausgabe ist festgehalten (die Funktion liest).
    /// </summary>
    [Fact]
    public async Task Datensatz_Range_wird_mit_echter_Lese_Faehigkeit_aufgeloest()
    {
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, Path.GetTempPath(), "ablauf-ds-" + Guid.NewGuid().ToString("N")[..8], grpc: false, FunktionsLaufort.Host);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            void Sende(string typ, ICommand c) => dispatcher.Dispatch(new CommandEnvelope
            { AggregateType = typ, Modus = new CommandModus.Emittiert(), Payload = c });

            var paar = Guid.NewGuid();
            Sende("ImagePair", new NimmBildAuf(new(paar), "PAIR-RANGE-" + paar.ToString("N")[..6], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/range",
                BildVersion.Dc0, new BildMeta("range.tiff", 1, 1, 1, DateTimeOffset.UtcNow), "/range.png"));
            using (var bereich = host.Services.CreateScope())
            {
                var finde = bereich.ServiceProvider.GetRequiredService<Domain.Projections.ImagePair.IFindImagePair>();
                await Warte(async () => await finde.FindByIdAsync(paar) is not null, TimeSpan.FromSeconds(30), "Read-Model des Paars fehlt");
            }

            var ds = Guid.NewGuid();
            Sende("Datensatz", new Domain.Datensatz.ErstelleDatensatz(new(ds), "Range-E2E"));
            await Warte(async () => (await es.ReadStreamAsync(ds, 0, default)).Count >= 1, TimeSpan.FromSeconds(30), "Datensatz nicht angelegt");
            Sende("Datensatz", new Domain.Datensatz.FuegeRangeHinzu(new(ds), new Domain.Datensatz.RangeKriterien()));

            await Warte(async () => (await es.ReadStreamAsync(ds, 0, default)).Any(e => e.Payload is Domain.Datensatz.PaareAufgenommen),
                TimeSpan.FromSeconds(60), "Die Range wurde nicht aufgelöst (Funktion RangeAufloesung).");
            var aufgenommen = (await es.ReadStreamAsync(ds, 0, default)).Select(e => e.Payload).OfType<Domain.Datensatz.PaareAufgenommen>().Single();
            aufgenommen.ImagePairIds.Should().Contain(new Id<Domain.ImagePair.ImagePair>(paar), "die Suche lief gegen das echte Read-Model");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>
    /// P1 (docs/konzept-werkbank.md §3.5): der echte Datei-Treiber gegen ein Temp-Verzeichnis — eine Datei steht genau einmal im Log und führt bis
    /// BildVerfuegbar; nach einem Neustart (neuer Host, derselbe Log) gleicht der Treiber gebündelt mit dem Log ab und meldet nichts erneut. Läuft im
    /// Default-Laufmodus EinmalImCluster (Lease über die Cluster-Identität quelle/DateiErkannt).
    /// </summary>
    [Trait("Kategorie", "Garantie")]
    [Fact]
    public async Task Datei_Treiber_meldet_eine_Datei_genau_einmal_auch_nach_Neustart()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "quelle-e2e-" + Guid.NewGuid().ToString("N"));
        var eingangsOrdner = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingangsOrdner);
        IHost Baue()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Quellen:DateiErkannt:Pfad"] = eingangsOrdner,
                ["Quellen:DateiErkannt:Muster:0"] = "*.tiff",
                ["Quellen:DateiErkannt:Intervall"] = "00:00:00.200",
                ["Quellen:DateiErkannt:Fertig:Fenster"] = "00:00:00.300",
            });
            Verdrahte(builder.Services, Path.Combine(verzeichnis, "vorschau"), "quelle-e2e-" + Guid.NewGuid().ToString("N")[..8], grpc: false,
                FunktionsLaufort.Host);
            builder.Services.AddQuellen(builder.Configuration);
            return builder.Build();
        }

        var t = Zufallszeit(12);
        var vorlage = Path.Combine(verzeichnis, "vorlage");
        Directory.CreateDirectory(vorlage);
        var datei = Datei(vorlage, t, BildVersion.Dc0);
        var paar = ImagePairFileName.Resolve(datei.Name)!.AggregateId.Wert;
        try
        {
            using (var host = Baue())
            {
                await host.StartAsync();
                try
                {
                    var es = host.Services.GetRequiredService<IEventStoreRepository>();
                    File.Copy(datei.Pfad, Path.Combine(eingangsOrdner, datei.Name));
                    await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(60),
                        "Die Datei führte nicht bis BildVerfuegbar — Treiber → QuellEingang → Funktionen lief nicht durch.");
                    host.Services.GetRequiredService<QuellenZustand>().Meldungen("DateiErkannt").Should().Be(1);
                }
                finally { await host.StopAsync(); }
            }

            using (var host = Baue())
            {
                await host.StartAsync();
                try
                {
                    var zustand = host.Services.GetRequiredService<QuellenZustand>();
                    await Warte(() => Task.FromResult(zustand.Alle.ContainsKey("DateiErkannt")), TimeSpan.FromSeconds(30),
                        "Der Treiber hat nach dem Neustart nicht gescannt (Lease nicht bekommen?).");
                    await Task.Delay(1500);   // mehrere Scans über die Fertig-Fenster hinaus
                    zustand.Meldungen("DateiErkannt").Should().Be(0, "der Start-Abgleich mit dem Log kennt die Datei schon");
                    var es = host.Services.GetRequiredService<IEventStoreRepository>();
                    (await es.ReadStreamAsync(paar, 0, default)).Count(e => e.Payload is ImagePairErstellt).Should().Be(1);
                }
                finally { await host.StopAsync(); }
            }
        }
        finally
        {
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* egal */ }
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
        while (d != null && d.GetFiles("*.sln").Length == 0) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Solution nicht gefunden");
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
