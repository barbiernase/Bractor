using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Abstractions;
using Domain.ImagePair;
using Bildvorschau.Katalog;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.GrpcClient;
using Infrastructure.Projections.Generated;
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
/// Ebene 2 — die ABLÄUFE der Beispieldomäne gegen echtes Marten/Consul: Aggregate entscheiden,
/// Funktionen in Graphen tragen die Kanten, Fristen tragen die Zeit — ohne Dirigent. Der Bildeingang läuft über den ganzen Linienimport —
///
///   Datei im Verzeichnis ──ƒ Dateiwaechter (Katalog, Zustand gesichert)──DateiGefunden──▶ ƒ Bildeinlesen ──BildEingelesen──┬─▶ ƒ Bildverkleinerung
///     ──BildVerkleinert──▶ ƒ Kontrastausgleich (OpenCV im Host) ──VorschauErstellt──┐                                                       │
///     ƒ Bildeingang (Join, gleichbleibend) ◀────────────────────────────────────────┴───────────────────────────────────────────────────────┘
///     ──NimmBildAuf──▶ ImagePair („anlegen oder ergänzen“) ──ImagePairKomplett──▶ der Klassifizierer reagiert (ein Client, der ihn verkörpert).
///
/// Beweiskraft: das Paar bekommt BildVerfuegbar mit einer Vorschau, die NUR die Bildaufbereitung erzeugt; die Spur führt zum Lauf der Quelle; eine
/// Datei ist nach einem Neustart nicht noch einmal ein Lauf. Ein Python-Worker, der den Klassifizierer verkörpert, reagiert auf jedes komplette Paar
/// und beurteilt es über gRPC — genau einmal im Log, mit dem Fakt als Ursache.
/// </summary>
public class AblaufE2ETests
{
    /// <summary>Die Host-Verdrahtung wie Host.Grpc: Framework (samt Graphen und Uhr), Projektionen, je Graph eine Instanz, Technik mit den Werten ihrer Knoten.</summary>
    private static void Verdrahte(IServiceCollection s, string ausgabe, string cluster, bool grpc, string? eingang = null)
    {
        s.AddCqrsFramework(opts => { opts.EnableGrpc = grpc; opts.ClusterName = cluster; });
        s.AddGeneratedPullPaths();   // die Projektionen füllen die Read-Models, die die Datensatz-Funktionen lesen
        KnotenKonfig Bild(bool mitPfad)
        {
            var k = new KnotenKonfig { Wiederholungen = 1 };
            if (mitPfad) k.Werte[nameof(BildaufbereitungKonfig.AusgabePfad)] = ausgabe;
            return k;
        }
        var instanzen = GraphInstanzen.JeGraphEine(Infrastructure.Komposition.Anwendung.Funktionen.Graphen, new Dictionary<string, KnotenKonfig>
        {
            [nameof(Bildeinlesen)] = Bild(false),
            [nameof(Bildverkleinerung)] = Bild(true),
            [nameof(Kontrastausgleich)] = Bild(true),
        }.Concat(eingang is null ? [] : [new KeyValuePair<string, KnotenKonfig>(nameof(Core.Katalog.Dateiwaechter), Waechter(eingang))])
            .ToDictionary(x => x.Key, x => x.Value));
        s.AddSingleton(instanzen);
        TechnikExtensions.BindeTechnik(s, instanzen);
    }

    /// <summary>Der Dateiwächter des Linienimports auf <paramref name="eingang"/> (*.tiff, schnell: Scan alle 200 ms, fertig nach 300 ms Ruhe).</summary>
    private static KnotenKonfig Waechter(string eingang)
    {
        var k = new KnotenKonfig();
        k.Werte["Pfad"] = eingang;
        k.Werte["Muster:0"] = "*.tiff";
        k.Werte["Intervall"] = "00:00:00.200";
        k.Werte["Fenster"] = "00:00:00.300";
        return k;
    }

    /// <summary>Ein echtes, kleines TIFF (OpenCV liest es) — geschrieben in <paramref name="verzeichnis"/>; Name und Pfad.</summary>
    private static (string Name, string Pfad) Datei(string verzeichnis, DateTimeOffset t, BildVersion v)
    {
        var name = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_{v.ToString().ToUpperInvariant()}.tiff";
        var pfad = Path.Combine(verzeichnis, name);
        Directory.CreateDirectory(verzeichnis);
        using (var bild = new Mat(48, 64, MatType.CV_8UC3, new Scalar(30, 120, 200))) Cv2.ImWrite(pfad, bild);
        return (name, pfad);
    }

    private static DateTimeOffset Zufallszeit(int tag) =>
        new DateTimeOffset(2026, 10, tag, 8, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));

    [Fact]
    public async Task Bildeingang_vom_Dateiwaechter_bis_Fakt_ueber_den_Linienimport()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "ablauf-e2e-" + Guid.NewGuid().ToString("N"));
        var ausgabe = Path.Combine(verzeichnis, "vorschau");
        var eingang = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingang);
        var builder = Host.CreateApplicationBuilder();
        Verdrahte(builder.Services, ausgabe, "ablauf-e2e-" + Guid.NewGuid().ToString("N")[..8], grpc: false, eingang);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var t = Zufallszeit(8);
            var dc0 = Datei(eingang, t, BildVersion.Dc0);
            Datei(eingang, t, BildVersion.Dc2);
            var paar = Aufnahmename.Resolve(dc0.Name)!.Paar;

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairKomplett), TimeSpan.FromSeconds(60),
                "Das Paar wurde nicht komplett — Dateiwächter → Einlesen → (Verkleinern → Kontrast) ∥ Bildeingang → ImagePair lief nicht durch.");
            await Task.Delay(TimeSpan.FromSeconds(2));

            var stream = await es.ReadStreamAsync(paar, 0, default);
            stream.Should().ContainSingle(e => e.Payload is ImagePairErstellt);
            stream.Should().ContainSingle(e => e.Payload is ImagePairKomplett);
            var bilder = stream.Select(e => e.Payload).OfType<BildVerfuegbar>().ToList();
            bilder.Select(b => b.Version).Should().BeEquivalentTo(new[] { BildVersion.Dc0, BildVersion.Dc2 }, "je Datei genau ein Lauf");
            bilder.Single(b => b.Version == BildVersion.Dc0).Pfad.Should().Be(Path.Combine(ausgabe, Path.GetFileNameWithoutExtension(dc0.Name) + "_vorschau.png"),
                "die Vorschau entsteht nur durch die Bildaufbereitung (OpenCV im Host)");
            File.Exists(bilder[0].Pfad).Should().BeTrue();
            bilder.Single(b => b.Version == BildVersion.Dc0).Meta.HoehePixel.Should().Be(Bildverkleinerung.VorschauHoehe);

            // Spur: der Command trägt den letzten Knoten; seine Ursache ist der Lauf der Quelle (Instanz und Nummer) — ein Quell-Command steht nie im Log.
            var bildEnv = stream.First(e => e.Payload is BildVerfuegbar);
            bildEnv.Wirker.Should().Be("decide:ImagePair/NimmBildAuf");
            bildEnv.Ueber.Should().Be("knoten:Linienimport/Aufnehmen", "der Absender des Commands ist der Knoten, der ihn ausgab (W9)");
            Spur.ZerlegeUrsache(bildEnv.Ursache, out var quelle, out _).Should().BeTrue();
            quelle.Should().Be(Herkunft.Ableite("quelle:Linienimport"), "die Ursache ist der Lauf der Quelle der Instanz");
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* egal */ }
        }
    }

    [Fact]
    public async Task Auf_ein_komplettes_Paar_reagiert_ein_Python_Worker_ueber_gRPC_genau_einmal()
    {
        // Der Interpreter der Sim-Umgebung (Umgebung:Python, sonst CQRS_PYTHON/python3) — dieselbe Regel wie die Werkbank.
        var python = Infrastructure.Umgebung.UmgebungLader.Lade(new ConfigurationManager(), "sim-main", []).Python();
        (python == "python3" || File.Exists(python)).Should().BeTrue($"Python für den Worker fehlt ({python}) — Umgebung:Python oder CQRS_PYTHON setzen");
        // Der Stub-Klassifizierer (KlassifikationsWorker, generierte Basis): er reagiert auf ImagePairKomplett und urteilt „KeineAnomalie“.
        var skript = Path.Combine(RepoWurzel(), Projektstruktur.PythonWorker(Projektstruktur.StandardWurzel), "run_stub.py");

        var verzeichnis = Path.Combine(Path.GetTempPath(), "ablauf-py-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(verzeichnis);
        var port = FreierPort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port, l => l.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        var eingangPy = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingangPy);
        Verdrahte(builder.Services, Path.Combine(verzeichnis, "vorschau"), "ablauf-py-" + Guid.NewGuid().ToString("N")[..8], grpc: true, eingangPy);
        var app = builder.Build();
        app.MapCqrsGrpcService();
        await app.StartAsync();

        var ausgabe = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var worker = new Process
        {
            StartInfo = new ProcessStartInfo(python, $"\"{skript}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(skript)!,
                Environment =
                {
                    ["CQRS_GRPC_HOST"] = "127.0.0.1", ["CQRS_GRPC_PORT"] = port.ToString(), ["PYTHONUNBUFFERED"] = "1",
                    ["CQRS_REAKTION_STAND"] = Path.Combine(verzeichnis, "reaktionen"),   // genau einmal: der Worker liest das Log ab seinem Stand
                },
            },
        };
        worker.OutputDataReceived += (_, e) => { if (e.Data != null) ausgabe.Enqueue(e.Data); };
        worker.ErrorDataReceived += (_, e) => { if (e.Data != null) ausgabe.Enqueue(e.Data); };
        try
        {
            worker.Start();
            worker.BeginOutputReadLine();
            worker.BeginErrorReadLine();
            await Warte(() => Task.FromResult(ausgabe.Any(z => z.Contains("laufen ab Position"))), TimeSpan.FromSeconds(60),
                "Die Reaktionen des Python-Workers laufen nicht:\n" + string.Join("\n", ausgabe));

            var es = app.Services.GetRequiredService<IEventStoreRepository>();
            var t = Zufallszeit(9);
            var dc0 = Datei(eingangPy, t, BildVersion.Dc0);
            Datei(eingangPy, t, BildVersion.Dc2);
            var paar = Aufnahmename.Resolve(dc0.Name)!.Paar;

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildPaarDurchKiKlassifiziert), TimeSpan.FromSeconds(90),
                "Kein BildPaarDurchKiKlassifiziert — der Python-Worker hat auf das komplette Paar nicht reagiert:\n" + string.Join("\n", ausgabe));
            await Task.Delay(TimeSpan.FromSeconds(2));
            var stream = await es.ReadStreamAsync(paar, 0, default);
            stream.Count(e => e.Payload is BildPaarDurchKiKlassifiziert).Should().Be(1, "die Reaktion auf ein Fakt wirkt genau einmal");
            var urteil = stream.Single(e => e.Payload is BildPaarDurchKiKlassifiziert);
            urteil.UserId.Should().Be("Klassifizierer", "die Reaktion gibt der Akteur hinein");
            var komplett = stream.Single(e => e.Payload is ImagePairKomplett);
            urteil.Ursache.Should().Be(Spur.Ursache(paar, komplett.AggregateVersion), "die Ursache des Urteils ist das Fakt, auf das der Worker reagierte");
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
        Verdrahte(builder.Services, Path.GetTempPath(), "ablauf-frist-" + Guid.NewGuid().ToString("N")[..8], grpc: false);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            var lauf = Guid.NewGuid();
            void Sende(IAggregatCommand c) => dispatcher.Dispatch(new CommandEnvelope
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
    /// Die Range als Reaktion eines Clients (Schritt 6): FuegeRangeHinzu → RangeAngefordert (ein Fakt, kein Auftrag) → der ReaktionsWorker hört es
    /// über gRPC, sucht die Paare über die echte Query (SucheImagePairs gegen das Read-Model) → NimmRangeAuf an DENSELBEN Datensatz (der Stream des
    /// Events).
    /// </summary>
    [Fact]
    public async Task Datensatz_Range_loest_ein_reagierender_Client_ueber_eine_Query_auf()
    {
        var port = FreierPort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port, l => l.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        Verdrahte(builder.Services, Path.GetTempPath(), "ablauf-ds-" + Guid.NewGuid().ToString("N")[..8], grpc: true);
        var host = builder.Build();
        host.MapCqrsGrpcService();
        await host.StartAsync();
        using var ende = new CancellationTokenSource();
        Task? reaktionen = null;
        try
        {
            reaktionen = Domain.Client.Worker.Reaktionslauf.LaufeAsync($"http://127.0.0.1:{port}", null,
                new global::Client.Infrastructure.Reaktionen.ReaktionsStandImSpeicher(), "ablauf-ds", ende.Token, takt: TimeSpan.FromMilliseconds(200));
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var dispatcher = host.Services.GetRequiredService<IAggregateDispatcher>();
            void Sende(string typ, IAggregatCommand c) => dispatcher.Dispatch(new CommandEnvelope
            { AggregateType = typ, Modus = new CommandModus.Emittiert(), Payload = c });

            var paar = Guid.NewGuid();
            Sende("ImagePair", new NimmBildAuf(new(paar), "PAIR-RANGE-" + paar.ToString("N")[..6], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/range",
                "DC0", "range.tiff", 1, 1, 1, DateTimeOffset.UtcNow, "/range.png"));
            using (var bereich = host.Services.CreateScope())
            {
                var finde = bereich.ServiceProvider.GetRequiredService<Domain.Projections.ImagePair.IFindImagePair>();
                await Warte(async () => await finde.FindByIdAsync(new(paar)) is not null, TimeSpan.FromSeconds(30), "Read-Model des Paars fehlt");
            }

            var ds = Guid.NewGuid();
            Sende("Datensatz", new Domain.Datensatz.ErstelleDatensatz(new(ds), "Range-E2E"));
            await Warte(async () => (await es.ReadStreamAsync(ds, 0, default)).Count >= 1, TimeSpan.FromSeconds(30), "Datensatz nicht angelegt");
            Sende("Datensatz", new Domain.Datensatz.FuegeRangeHinzu(new(ds), new Domain.Datensatz.RangeKriterien()));

            await Warte(async () => (await es.ReadStreamAsync(ds, 0, default)).Any(e => e.Payload is Domain.Datensatz.PaareAufgenommen),
                TimeSpan.FromSeconds(60), "Die Range wurde nicht aufgelöst (Reaktion des ReaktionsWorkers)" + (reaktionen.IsFaulted ? ": " + reaktionen.Exception : "."));
            var aufgenommen = (await es.ReadStreamAsync(ds, 0, default)).Select(e => e.Payload).OfType<Domain.Datensatz.PaareAufgenommen>().Single();
            aufgenommen.ImagePairIds.Should().Contain(new Id<Domain.ImagePair.ImagePair>(paar), "die Suche lief gegen das echte Read-Model");
        }
        finally
        {
            await ende.CancelAsync();
            if (reaktionen != null) try { await reaktionen; } catch { /* beendet */ }
            await host.StopAsync();
        }
    }

    /// <summary>
    /// P1 (docs/04 §4.7, Entscheidungen 8 und 9): der Dateiwächter des Katalogs gegen ein Temp-Verzeichnis — eine Datei führt in genau einem Lauf bis
    /// BildVerfuegbar; nach einem Neustart (neuer Host, dieselbe Datenbank) hat der Wächter seinen gesicherten Zustand und meldet nichts erneut. Die
    /// Quellinstanz läuft genau einmal im Cluster (mit der Sperre der Instanz).
    /// </summary>
    [Trait("Kategorie", "Garantie")]
    [Fact]
    public async Task Dateiwaechter_meldet_eine_Datei_genau_einmal_auch_nach_Neustart()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "quelle-e2e-" + Guid.NewGuid().ToString("N"));
        var eingangsOrdner = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingangsOrdner);
        IHost Baue()
        {
            var builder = Host.CreateApplicationBuilder();
            Verdrahte(builder.Services, Path.Combine(verzeichnis, "vorschau"), "quelle-e2e-" + Guid.NewGuid().ToString("N")[..8], grpc: false, eingangsOrdner);
            return builder.Build();
        }

        var t = Zufallszeit(12);
        var datei = Datei(Path.Combine(verzeichnis, "vorlage"), t, BildVersion.Dc0);
        var paar = Aufnahmename.Resolve(datei.Name)!.Paar;
        // Der Zustand gilt je Instanz über Neustarts — auch über die Tests dieser Klasse hinweg (geteilte DB, dieselbe Instanz „Linienimport“):
        //   erwartet wird darum der Stand vor dem Test plus ein Lauf.
        long vorher = 0;
        try
        {
            using (var host = Baue())
            {
                await host.StartAsync();
                try
                {
                    vorher = (await host.Services.GetRequiredService<IZustandsAblage>().LadeAsync("Linienimport", "Eingang", default))?.Nummer ?? 0;
                    var es = host.Services.GetRequiredService<IEventStoreRepository>();
                    File.Copy(datei.Pfad, Path.Combine(eingangsOrdner, datei.Name));
                    await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(60),
                        "Die Datei führte nicht bis BildVerfuegbar — Dateiwächter → Bildaufbereitung lief nicht durch.");
                    var ablage = host.Services.GetRequiredService<IZustandsAblage>();
                    await Warte(async () => (await ablage.LadeAsync("Linienimport", "Eingang", default))?.Nummer == vorher + 1, TimeSpan.FromSeconds(30),
                        "Der Zustand des Wächters wurde nach dem fertigen Lauf nicht gesichert.");
                }
                finally { await host.StopAsync(); }
            }

            using (var host = Baue())
            {
                await host.StartAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));   // viele Scans über das Fertig-Fenster hinaus
                    var es = host.Services.GetRequiredService<IEventStoreRepository>();
                    (await es.ReadStreamAsync(paar, 0, default)).Count(e => e.Payload is ImagePairErstellt).Should().Be(1);
                    (await es.ReadStreamAsync(paar, 0, default)).Count(e => e.Payload is BildVerfuegbar).Should().Be(1,
                        "der gesicherte Zustand kennt die Datei — kein zweiter Lauf");
                    (await host.Services.GetRequiredService<IZustandsAblage>().LadeAsync("Linienimport", "Eingang", default))!.Nummer.Should().Be(vorher + 1);
                }
                finally { await host.StopAsync(); }
            }
        }
        finally
        {
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* egal */ }
        }
    }

    /// <summary>
    /// Entscheidung 10: dieselbe Blaupause läuft beliebig oft — zwei Instanzen des Linienimports auf zwei Verzeichnissen (je eine Datei des Paars) führen
    /// beide bis zum Paar; jede Instanz hat ihren eigenen Wächter-Zustand.
    /// </summary>
    [Fact]
    public async Task Zwei_Instanzen_desselben_Graphen_auf_zwei_Verzeichnissen()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "zwei-linien-" + Guid.NewGuid().ToString("N"));
        var ausgabe = Path.Combine(verzeichnis, "vorschau");
        KnotenKonfig Bild(bool mitPfad)
        {
            var k = new KnotenKonfig { Wiederholungen = 1 };
            if (mitPfad) k.Werte[nameof(BildaufbereitungKonfig.AusgabePfad)] = ausgabe;
            return k;
        }
        GraphInstanzKonfig Linie(string eingang) => new()
        {
            Graph = "Linienimport", Rechner = "linien",   // eine Quelle läuft im Cluster auf ihrem Rechner
            Knoten = { ["Eingang"] = Waechter(eingang), ["Aufbereitung.Verkleinern"] = Bild(true), ["Aufbereitung.Kontrast"] = Bild(true) },
        };
        var linie1 = Path.Combine(verzeichnis, "linie1");
        var linie2 = Path.Combine(verzeichnis, "linie2");
        Directory.CreateDirectory(linie1);
        Directory.CreateDirectory(linie2);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = "zwei-linien-" + Guid.NewGuid().ToString("N")[..8];
            opts.Rechner = "linien";
        });
        // Eigene Instanz-Namen je Lauf: ihr Zustand bleibt in der geteilten DB über den Test hinaus.
        var (n1, n2) = ("linie1-" + Guid.NewGuid().ToString("N")[..6], "linie2-" + Guid.NewGuid().ToString("N")[..6]);
        var instanzen = GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig> { [n1] = Linie(linie1), [n2] = Linie(linie2) },
            Infrastructure.Komposition.Anwendung.Funktionen.Graphen);
        builder.Services.AddSingleton(instanzen);
        TechnikExtensions.BindeTechnik(builder.Services, instanzen);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var t = Zufallszeit(14);
            var dc0 = Datei(linie1, t, BildVersion.Dc0);
            Datei(linie2, t, BildVersion.Dc2);
            var paar = Aufnahmename.Resolve(dc0.Name)!.Paar;
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairKomplett), TimeSpan.FromSeconds(60),
                "Das Paar wurde nicht komplett — je Linie ein Wächter, je Datei ein Lauf.");
            var ablage = host.Services.GetRequiredService<IZustandsAblage>();
            await Warte(async () => await ablage.LadeAsync(n1, "Eingang", default) is { Nummer: 1 } && await ablage.LadeAsync(n2, "Eingang", default) is { Nummer: 1 },
                TimeSpan.FromSeconds(30), "jede Instanz sichert ihren eigenen Wächter-Zustand");
        }
        finally
        {
            await host.StopAsync();
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
