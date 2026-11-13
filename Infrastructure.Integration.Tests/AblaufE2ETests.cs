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
/// Funktionen in Graphen tragen die Kanten, Fristen tragen die Zeit — ohne Dirigent. Der Bildeingang läuft über die Quelle einer Instanz der Kamera-App (sie
/// setzt den Linienrechner um, der das Kamerasystem verkörpert) und die ganze Bildaufbereitung —
///
///   Datei im Verzeichnis ──Dateiwaechter (Quelle Kamera der App-Instanz, Zustand gesichert)──DateiGefunden──▶ Übersetzung der Akteur-Seite
///     ──LiesBild (offener Eingang am Kopf, im Namen des KameraSystems)──▶ ƒ Einlesen ──▶ ƒ Verkleinern ──▶ ƒ Kontrast (OpenCV im Host) ──▶ ƒ Deuten
///     ──BildGedeutet──▶ Übersetzung der Domäne ──NimmBildAuf──▶ ImagePair („anlegen oder ergänzen“) ──ImagePairKomplett──▶ der Klassifizierer reagiert
///     (ein Client, der ihn verkörpert).
///
/// Beweiskraft: das Paar bekommt BildVerfuegbar mit einer Vorschau, die NUR die Bildaufbereitung erzeugt; die Spur führt zum Element des Eingangs; eine
/// Datei ist nach einem Neustart nicht noch einmal ein Lauf. Ein Python-Worker, der den Klassifizierer verkörpert, reagiert auf jedes komplette Paar
/// und beurteilt es über gRPC — genau einmal im Log, mit dem Fakt als Ursache.
/// </summary>
public class AblaufE2ETests
{
    /// <summary>Die App des Linienrechners (er verkörpert das Kamerasystem) und ihre Quelle, ein Dateiwächter (Domain.Akteure.KameraApp).</summary>
    private const string KameraApp = "KameraApp", Quelle = "Kamera";

    /// <summary>Der Rechner der Instanzen der Kamera-App (ihr Rechnerdienst läuft hier in-process).</summary>
    private const string Linienrechnername = "linien";

    /// <summary>
    /// Die Host-Verdrahtung wie Host.Grpc: Framework (samt Graphen und Uhr), Projektionen, je Graph eine Instanz und die Technik mit den Werten ihrer
    /// Knoten. Quellen laufen nicht hier, sondern im Rechnerdienst (<see cref="Lauf"/>).
    /// </summary>
    private static void Verdrahte(IServiceCollection s, string ausgabe, string cluster, bool grpc)
    {
        s.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = grpc;
            opts.ClusterName = cluster;
        });
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
        });
        s.AddSingleton(instanzen);
        TechnikExtensions.BindeTechnik(s, instanzen);
    }

    /// <summary>
    /// Ein Host mit gRPC (wie Host.Grpc) und daneben der Lauf der Quellen des Rechnerdiensts (in-process, über die Leitung wie Host.Rechner): je Verzeichnis
    /// eine Instanz der Kamera-App, ihre Quelle ein Dateiwächter. Ein Neustart des Rechnerdiensts ist ein neuer Lauf.
    /// </summary>
    private sealed class Lauf : IAsyncDisposable
    {
        public required WebApplication Host { get; init; }
        public required int Port { get; init; }
        public Client.Infrastructure.Quellen.Quellenlauf? Quellen { get; private set; }
        private CancellationTokenSource? _ende;
        private Task? _quellen;
        private ServiceProvider? _dienste;

        public static async Task<Lauf> StarteAsync(string ausgabe, string cluster, params (string Name, string Pfad)[] linien)
        {
            var port = FreierPort();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port, l => l.Protocols = HttpProtocols.Http2));
            builder.Services.AddGrpc();
            Verdrahte(builder.Services, ausgabe, cluster, grpc: true);
            var app = builder.Build();
            app.MapCqrsGrpcService();
            await app.StartAsync();
            var lauf = new Lauf { Host = app, Port = port };
            if (linien.Length > 0) lauf.StarteQuellen(linien);
            return lauf;
        }

        /// <summary>Startet den Lauf der Quellen (neu: ein Neustart des Rechnerdiensts — die Quellarten beginnen ohne Gedächtnis).</summary>
        public void StarteQuellen(params (string Name, string Pfad)[] linien)
        {
            var eingaenge = Eingaenge(linien);
            _dienste = new ServiceCollection().AddLogging().BindeTechnik(GraphInstanzen.Keine, eingaenge).BuildServiceProvider();
            var a = Infrastructure.Komposition.Anwendung.Aktuell;
            _ende = new CancellationTokenSource();
            Quellen = new Client.Infrastructure.Quellen.Quellenlauf(eingaenge, linien.ToDictionary(l => l.Name, l => Kamera(l.Pfad)), a.Akteure.AppVertraege,
                _dienste, a.Nutzlast!, antwortfrist: TimeSpan.FromSeconds(2));
            _quellen = Quellen.LaufeAsync($"http://127.0.0.1:{Port}", _ende.Token);
        }

        /// <summary>Hält den Lauf der Quellen an (der Rechnerdienst fällt aus).</summary>
        public async Task StoppeQuellenAsync()
        {
            if (_ende is null) return;
            await _ende.CancelAsync();
            try { if (_quellen is not null) await _quellen; } catch { /* beendet */ }
            if (_dienste is not null) await _dienste.DisposeAsync();
            _ende = null;
        }

        public async ValueTask DisposeAsync()
        {
            await StoppeQuellenAsync();
            await Host.StopAsync();
            await Host.DisposeAsync();
        }
    }

    /// <summary>
    /// Die Instanzen der Kamera-App (<c>KameraApp([Uebersetzt&lt;…&gt;] Dateiwaechter Kamera) : IApp&lt;Linienrechner&gt;</c>), geprüft wie im Rechnerdienst
    /// (<see cref="Infrastructure.Quellen.Eingaenge.Aus"/>): je Verzeichnis eine Instanz auf dem Rechner <see cref="Linienrechnername"/>, ihre Quelle
    /// ein Dateiwächter für das Kamerasystem.
    /// </summary>
    private static Infrastructure.Quellen.Eingaenge Eingaenge(params (string Name, string Pfad)[] linien)
    {
        if (linien.Length == 0) return Infrastructure.Quellen.Eingaenge.Keine;
        var a = Infrastructure.Komposition.Anwendung.Aktuell;
        return Infrastructure.Quellen.Eingaenge.Aus(linien.ToDictionary(l => l.Name, l => Kamera(l.Pfad)), a.Akteure.AppVertraege, a.Funktionen.Bausteine,
            new Uebersetzungen(a.Funktionen.Uebersetzungen), a.Akteure.Rechte, tokens: null);
    }

    /// <summary>Eine Instanz der Kamera-App, ihr Dateiwächter auf <paramref name="pfad"/> (*.tiff, schnell: Scan alle 200 ms, fertig nach 300 ms Ruhe).</summary>
    private static AppInstanzKonfig Kamera(string pfad) => new()
    {
        App = KameraApp, Rechner = Linienrechnername,
        Quellen = { [Quelle] = new QuelleKonfig { Werte = { ["Pfad"] = pfad, ["Muster:0"] = "*.tiff", ["Intervall"] = "00:00:00.200", ["Fenster"] = "00:00:00.300" } } },
    };

    /// <summary>Ein Instanzname je Lauf: der Zustand einer Quelle bleibt in der geteilten DB über den Test hinaus.</summary>
    private static string Linie(string name) => name + "-" + Guid.NewGuid().ToString("N")[..6];

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
    public async Task Bildeingang_vom_Dateiwaechter_eines_Eingangs_bis_Fakt_ueber_die_Bildaufbereitung()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "ablauf-e2e-" + Guid.NewGuid().ToString("N"));
        var ausgabe = Path.Combine(verzeichnis, "vorschau");
        var eingang = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingang);
        var linie = Linie("linie1");
        var lauf = await Lauf.StarteAsync(ausgabe, "ablauf-e2e-" + Guid.NewGuid().ToString("N")[..8], (linie, eingang));
        try
        {
            var es = lauf.Host.Services.GetRequiredService<IEventStoreRepository>();
            var t = Zufallszeit(8);
            var dc0 = Datei(eingang, t, BildVersion.Dc0);
            var dc2 = Datei(eingang, t, BildVersion.Dc2);
            var paar = Aufnahmename.Resolve(dc0.Name)!.Paar;

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairKomplett), TimeSpan.FromSeconds(60),
                "Das Paar wurde nicht komplett — Dateiwächter → LiesBild → Einlesen → Verkleinern → Kontrast → Deuten → ImagePair lief nicht durch.");
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

            // Spur: den Command gab die Übersetzung der Domäne aus dem Ausgang der Bildaufbereitung; seine Ursache ist das Element des Eingangs (Eingang
            //   und Kennung der Datei, Version 0) — was eine Quelle meldet, steht nie im Log.
            var bildEnv = stream.First(e => e.Payload is BildVerfuegbar);
            bildEnv.Wirker.Should().Be("decide:ImagePair/NimmBildAuf");
            bildEnv.Ueber.Should().Be(WirkerId.FuerUebersetzung("BildGedeutet_NimmBildAuf_1"),
                "der Absender des Commands ist die Übersetzung, die ihn aus BildGedeutet machte (W9)");
            bildEnv.UserId.Should().Be("KameraSystem", "die Kette trägt den Akteur ihres Auslösers — der Quelle (über die Leitung wie eine App)");
            Spur.ZerlegeUrsache(bildEnv.Ursache, out var quelle, out var version).Should().BeTrue();
            Guid Element((string Name, string Pfad) d)
            {
                var fi = new FileInfo(d.Pfad);
                return Kettenvertrag.Element(linie + "/" + Quelle, new DateiGefunden(d.Name, d.Pfad, d.Name, fi.Length,
                    new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero), default));
            }
            new[] { Element(dc0), Element(dc2) }.Should().Contain(quelle, "die Ursache ist das Element der Quelle — aus Instanz, Quelle und Kennung der Datei");
            version.Should().Be(0);
        }
        finally
        {
            await lauf.DisposeAsync();
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
        var eingangPy = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingangPy);
        var lauf = await Lauf.StarteAsync(Path.Combine(verzeichnis, "vorschau"), "ablauf-py-" + Guid.NewGuid().ToString("N")[..8], (Linie("linie1"), eingangPy));
        var app = lauf.Host;
        var port = lauf.Port;

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
            await lauf.DisposeAsync();
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
    /// P1 (docs/04 §4.7, docs/10 E18): der Dateiwächter des Katalogs als Quelle einer Instanz der Kamera-App gegen ein Temp-Verzeichnis — eine
    /// Datei führt in genau einem Lauf bis BildVerfuegbar. Nach einem Neustart des Rechnerdiensts meldet der Wächter sie erneut (er merkt sich nichts über
    /// den Lauf hinaus) — mit derselben Kennung aus dem Inhalt: dieselben Ids, das Backend verwirft die Wiederholung, sie wirkt nicht noch einmal.
    /// </summary>
    [Trait("Kategorie", "Garantie")]
    [Fact]
    public async Task Dateiwaechter_wirkt_eine_Datei_genau_einmal_auch_wenn_er_sie_nach_einem_Neustart_erneut_meldet()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "quelle-e2e-" + Guid.NewGuid().ToString("N"));
        var eingangsOrdner = Path.Combine(verzeichnis, "ein");
        Directory.CreateDirectory(eingangsOrdner);
        var linie = Linie("linie1");
        var t = Zufallszeit(12);
        var datei = Datei(Path.Combine(verzeichnis, "vorlage"), t, BildVersion.Dc0);
        var paar = Aufnahmename.Resolve(datei.Name)!.Paar;
        var lauf = await Lauf.StarteAsync(Path.Combine(verzeichnis, "vorschau"), "quelle-e2e-" + Guid.NewGuid().ToString("N")[..8], (linie, eingangsOrdner));
        try
        {
            var es = lauf.Host.Services.GetRequiredService<IEventStoreRepository>();
            File.Copy(datei.Pfad, Path.Combine(eingangsOrdner, datei.Name));
            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(60),
                "Die Datei führte nicht bis BildVerfuegbar — Dateiwächter → Leitung → Bildaufbereitung lief nicht durch.");

            // Der Rechnerdienst fällt aus und kommt wieder: der neue Wächter meldet die Datei erneut.
            await lauf.StoppeQuellenAsync();
            lauf.StarteQuellen((linie, eingangsOrdner));
            await Warte(() => Task.FromResult(lauf.Quellen!.Laeufer.TryGetValue(linie + "/" + Quelle, out var l) && l.Hineingegeben >= 1),
                TimeSpan.FromSeconds(30), "Nach dem Neustart hat der Wächter die Datei nicht erneut gemeldet.");
            await Task.Delay(TimeSpan.FromSeconds(5));   // was eine zweite Kette noch täte
            var stream = await es.ReadStreamAsync(paar, 0, default);
            stream.Count(e => e.Payload is ImagePairErstellt).Should().Be(1);
            stream.Count(e => e.Payload is BildVerfuegbar).Should().Be(1, "dieselbe Kennung ergibt dieselben Ids — das Backend verwirft die Wiederholung");
        }
        finally
        {
            await lauf.DisposeAsync();
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* egal */ }
        }
    }

    /// <summary>
    /// Mehrere Verzeichnisse sind mehrere Instanzen einer App (docs/04 §4.7): zwei Instanzen der Kamera-App (je eine Datei des Paars) geben beide in
    /// dieselbe Bildaufbereitung — der Graph läuft je Umgebung einmal —, beide führen bis zum Paar; jede hat ihre eigene Verbindung und ihren Läufer.
    /// </summary>
    [Fact]
    public async Task Zwei_Instanzen_einer_App_auf_zwei_Verzeichnissen()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "zwei-linien-" + Guid.NewGuid().ToString("N"));
        var linie1 = Path.Combine(verzeichnis, "linie1");
        var linie2 = Path.Combine(verzeichnis, "linie2");
        Directory.CreateDirectory(linie1);
        Directory.CreateDirectory(linie2);
        var (n1, n2) = (Linie("linie1"), Linie("linie2"));
        var lauf = await Lauf.StarteAsync(Path.Combine(verzeichnis, "vorschau"), "zwei-linien-" + Guid.NewGuid().ToString("N")[..8], (n1, linie1), (n2, linie2));
        try
        {
            lauf.Host.Services.GetRequiredService<GraphInstanzen>().Alle.Count(i => i.Graph.Name == nameof(Bildaufbereitung)).Should().Be(1);
            var t = Zufallszeit(14);
            var dc0 = Datei(linie1, t, BildVersion.Dc0);
            Datei(linie2, t, BildVersion.Dc2);
            var paar = Aufnahmename.Resolve(dc0.Name)!.Paar;
            var es = lauf.Host.Services.GetRequiredService<IEventStoreRepository>();
            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairKomplett), TimeSpan.FromSeconds(60),
                "Das Paar wurde nicht komplett — je Linie ein Wächter, je Datei ein Lauf.");
            lauf.Quellen!.Laeufer.Keys.Should().BeEquivalentTo([n1 + "/" + Quelle, n2 + "/" + Quelle], "je Verzeichnis eine Instanz derselben App");
            lauf.Quellen.Laeufer.Values.Should().OnlyContain(l => l.Hineingegeben == 1, "jede Instanz gab ihre Datei hinein");
        }
        finally
        {
            await lauf.DisposeAsync();
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
