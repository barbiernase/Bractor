using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Abstractions;
using Domain.Pipeline.Bildaufbereitung;
using Domain.ImagePair;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Domain.Pipeline.ImagePair;
using Domain.Pipeline.Trainingslauf;

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
    /// <summary>Eigenes Event-Schema — nie das Standard-Schema <c>es</c>, in das auch andere Läufe schreiben.</summary>
    private const string Schema = "it_fluss";

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
        s.AddCqrsFramework(opts => { opts.EnableGrpc = grpc; opts.ClusterName = cluster; opts.EventStoreSchema = Schema; });
        GeneratedPipelines.RegisterAllPipelines(s);
        s.AddGeneratedProzesse();
        if (verkleinerungExtern) s.AddExterneFunktion<IBildVerkleinerung>();
        else s.AddFunktion<IBildVerkleinerung, Verkleinerung>(slots: 2);
        s.AddFunktion<IHistogrammAusgleich, Ausgleich>(slots: 2);
        s.AddFunktion<IDateinameDeutung, ImagePairDateinameDeutung>(slots: 4);
        s.AddFunktion<Domain.Pipeline.Datensatz.IRangeSuche, Domain.Pipeline.Datensatz.RangeSuche>();
        s.AddFunktion<Domain.Pipeline.Datensatz.IMitgliederEinfrieren, Domain.Pipeline.Datensatz.MitgliederEinfrieren>();
    }

    private static DateiErkannt Datei(string watchDir, DateTimeOffset t, BildVersion v)
    {
        var name = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_{v.ToString().ToUpperInvariant()}.tiff";
        return new DateiErkannt
        {
            Name = name, Pfad = Path.Combine(watchDir, name), RelativerPfad = name, Groesse = 4711, GeaendertAm = t, ErkanntAm = t,
            Kennung = $"{name}|4711",
        };
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
            var paar = ImagePairFileName.Resolve(datei.Name)!.AggregateId;

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

            // Spur (Werkbank P4): von BildVerfuegbar läuft eine lückenlose Ursache-Kette über Funktionen und Commands bis zur Quelle;
            //   jeder Eintrag trägt seinen Wirker (die Karte im Editor) — gelesen aus den Headern des echten Logs.
            var bildEnv = stream.Single(e => e.Payload is BildVerfuegbar);
            bildEnv.Wirker.Should().Be("decide:ImagePair/MeldeBildVerfuegbar");
            var kette = new List<EventEnvelope> { bildEnv };
            var schritt = bildEnv;
            while (schritt.Ursache is { } ursache && kette.Count < 20)
            {
                Spur.ZerlegeUrsache(ursache, out var s, out var v).Should().BeTrue($"'{ursache}' ist eine Ursache");
                schritt = (await es.ReadStreamAsync(s, (int)v, default)).First(e => e.AggregateVersion == v);
                kette.Add(schritt);
            }
            schritt.Payload.Should().BeOfType<DateiErkannt>("die Kette endet an der Quelle: " + string.Join(" ← ", kette.Select(e => e.Wirker)));
            schritt.AggregateId.Should().Be(vorgang);
            schritt.Wirker.Should().Be("quelle:DateiErkannt");
            kette.Should().OnlyContain(e => e.Wirker != null);
            kette.Select(e => e.Wirker).Should().Contain(w => w!.StartsWith("funktion:"), "die Kette führt über mindestens eine Funktion");
            // Absender (ueber, Werkbank P6): der Fluss-Knoten, der den Command bzw. Auftrag abschickte — der Weg über die Karten dazwischen.
            bildEnv.Ueber.Should().Be("fluss:Bildeingang#melden");
            kette.Where(e => e.Wirker!.StartsWith("funktion:")).Should().OnlyContain(e => e.Ueber != null && e.Ueber.StartsWith("fluss:Bildeingang#"));

            // Einblick (Werkbank P4.6/P4.7): der Vorgang steht mit Wurzel und Zustand in der Liste, sein Graph enthält die Kette.
            var leser = new Infrastructure.Einblick.SpurLeser("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres", Schema);
            var vg = (await leser.VorgaengeAsync(200, default)).Single(x => x.Korrelation == korrelation.ToString());
            (vg.Zustand, vg.Prozess, vg.WurzelWirker).Should().Be(("fertig", nameof(Bildeingang), "quelle:DateiErkannt"));
            (await leser.VorgaengeAsync(200, default)).Should().NotContain(x => x.Korrelation == vorgang.ToString(),
                "die Quell-Nachricht ist die Wurzel des Prozess-Vorgangs, kein eigener Vorgang");
            var graph = await leser.VorgangAsync(korrelation.ToString(), default);
            graph.Select(e => e.Seq).Should().BeInAscendingOrder();
            graph.First().Wirker.Should().Be("quelle:DateiErkannt", "der Graph beginnt an der Wurzel");
            graph.Should().Contain(e => e.Wirker == "decide:ImagePair/MeldeBildVerfuegbar" && !e.Intern);
            (await leser.LeseAbSeqAsync(graph.First().Seq - 1, 1, default)).Single().Seq.Should().Be(graph.First().Seq);

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
        // Der Interpreter der Sim-Umgebung (Umgebung:Python, sonst CQRS_PYTHON/python3) — dieselbe Regel wie die Werkbank.
        var python = Infrastructure.Umgebung.UmgebungLader.Lade(new ConfigurationManager(), "sim-main", []).Python();
        (python == "python3" || File.Exists(python)).Should().BeTrue($"Python für den Worker fehlt ({python}) — Umgebung:Python oder CQRS_PYTHON setzen");
        // Der GENERISCHE Worker (konzept-werkbank §12.3 E13) mit der Tabelle aus dem Codegen; die Test-Implementierung (funktionen/bild_verkleinerung.py,
        //   hängt „_py" an) liegt per --pfad vor der des Projekts.
        var skript = Path.Combine(RepoWurzel(), Projektstruktur.PythonWorker(Projektstruktur.StandardWurzel), "funktions_worker.py");
        var testImpls = Path.Combine(RepoWurzel(), "Infrastructure.Integration.Tests", "Python");

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
            StartInfo = new ProcessStartInfo(python, $"\"{skript}\" --funktionen IBildVerkleinerung --slots 2 --pfad \"{testImpls}\"")
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
            var t = new DateTimeOffset(2026, 10, 9, 8, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
            var datei = Datei(watchDir, t, BildVersion.Dc2);
            var paar = ImagePairFileName.Resolve(datei.Name)!.AggregateId;
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
                var finde = bereich.ServiceProvider.GetRequiredService<Domain.Projections.ImagePair.IFindImagePair>();
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

    /// <summary>
    /// P1 (docs/konzept-werkbank.md §3.5): der echte Datei-Treiber gegen ein Temp-Verzeichnis — eine Datei startet genau einen Vorgang
    /// (bis BildVerfuegbar); nach einem Neustart (neuer Host, derselbe Log) gleicht der Treiber gebündelt mit dem Log ab und meldet
    /// nichts erneut. Läuft im Default-Laufmodus EinmalImCluster (Lease über die Cluster-Identität quelle/DateiErkannt).
    /// </summary>
    [Fact]
    public async Task Datei_Treiber_meldet_eine_Datei_genau_einmal_auch_nach_Neustart()
    {
        var watchDir = Path.Combine(Path.GetTempPath(), "quelle-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(watchDir);
        IHost Baue()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Quellen:DateiErkannt:Pfad"] = watchDir,
                ["Quellen:DateiErkannt:Muster:0"] = "*.tiff",
                ["Quellen:DateiErkannt:Intervall"] = "00:00:00.200",
                ["Quellen:DateiErkannt:Fertig:Fenster"] = "00:00:00.300",
            });
            Verdrahte(builder.Services, watchDir, "quelle-e2e-" + Guid.NewGuid().ToString("N")[..8], grpc: false, verkleinerungExtern: false);
            builder.Services.AddQuellen(builder.Configuration);
            return builder.Build();
        }

        var t = new DateTimeOffset(2026, 10, 12, 9, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
        var name = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_DC0.tiff";
        var paar = ImagePairFileName.Resolve(name)!.AggregateId;
        try
        {
            using (var host = Baue())
            {
                await host.StartAsync();
                try
                {
                    var es = host.Services.GetRequiredService<IEventStoreRepository>();
                    await File.WriteAllTextAsync(Path.Combine(watchDir, name), "bild");
                    await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is BildVerfuegbar), TimeSpan.FromSeconds(60),
                        "Die Datei hat keinen Vorgang bis BildVerfuegbar gestartet — Treiber → QuellEingang → Dirigent lief nicht durch.");
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
