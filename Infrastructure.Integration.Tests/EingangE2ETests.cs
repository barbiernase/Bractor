using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Eingang;
using Infrastructure.Einblick;
using Infrastructure.Quellen;
using Infrastructure.Umgebung;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — DURCHSPIELEN gegen echtes Postgres/Redis (docs/konzept-werkbank.md §6, Werkbank P5): der JSON-Eingang des Hosts.
/// <list type="bullet">
/// <item>Als Akteur handeln: ein Token → Akteur-Tor → Aggregat (Event im Log, Ablehnung als Ablehnungs-Event), Verweigerung ohne Befugnis
///   bzw. ohne gültiges Token, Query an den Reader;</item>
/// <item>Laufort Hand: der Bildeingang wartet an der Dateinamen-Deutung auf einen Menschen; seine Antwort über <c>/eingang/ergebnis</c>
///   lässt den Fluss weiterlaufen bis zum Paar.</item>
/// </list>
/// </summary>
public class EingangE2ETests
{
    private const string Verbindung = "Host=localhost;Port=5432;Username=postgres;Password=postgres";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Als_Akteur_handeln_und_eine_Hand_Funktion_beantworten()
    {
        var name = "sim-eingang-" + Guid.NewGuid().ToString("N")[..6];
        var u = new UmgebungKonfig
        {
            Umgebung = new UmgebungKopf { Name = name, Art = UmgebungsArt.Sim },
            EventStore = new EventStoreKonfig { Verbindung = Verbindung, Datenbank = "cqrs_it_eingang_" + Guid.NewGuid().ToString("N")[..8], Schema = "es" },
            VersionIndex = new VersionIndexKonfig { Praefix = name + ":" },
            Cluster = new ClusterKonfig { Art = ClusterArt.Einzelknoten, Name = "cqrs-" + name },
            Transport = new TransportKonfig { GrpcPort = FreierPort(), EinblickPort = FreierPort(), JsonEingang = true },
            Funktionen = new(StringComparer.Ordinal)
            {
                ["IDateinameDeutung"] = new() { Laufort = FunktionsLaufort.Hand },
                ["IBildVerkleinerung"] = new() { Slots = 1 }, ["IHistogrammAusgleich"] = new() { Slots = 1 },
                ["IRangeSuche"] = new() { Slots = 1 }, ["IMitgliederEinfrieren"] = new() { Slots = 1 },
            },
        };
        var verz = Directory.CreateTempSubdirectory("eingang-it-").FullName;
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Quellen:DateiErkannt:Pfad"] = Path.Combine(verz, "eingang"),
            ["Konfig:PreprocessingConfig:OutputPath"] = Path.Combine(verz, ".preprocessed"),
            // konzept-werkbank §12.3 E12: der Webhook der Quelle ist Konfig — kein MapQuellWebhook im Programm.
            ["Quellen:DateiErkannt:Webhook"] = "/webhook/datei",
            ["Identitaeten:Tokens:t-ki"] = "KIOperator",
            ["Identitaeten:Tokens:t-inspekteur"] = "Inspekteur",
        });
        builder.AddCqrs(u);
        await using var app = builder.Build();
        app.MapEinblick(u);
        app.MapEingang(u);
        app.MapQuellWebhooks(u);
        using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{u.Transport.EinblickPort}") };
        Task<HttpResponseMessage> Sende(string pfad, object inhalt, string? token)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, pfad) { Content = JsonContent.Create(inhalt) };
            if (token != null) req.Headers.Add("akteur-token", token);
            return http.SendAsync(req);
        }
        static async Task<JsonElement> Lies(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

        await app.StartAsync();
        try
        {
            // ── Als Akteur handeln ──
            var rechte = await Lies(await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/eingang/rechte") { Headers = { { "akteur-token", "t-ki" } } }));
            rechte.GetProperty("akteur").GetString().Should().Be("KIOperator");
            rechte.GetProperty("commands").EnumerateArray().Select(x => x.GetString()).Should().Contain("ErstelleDatensatz").And.NotContain("LabelBildPaar");
            rechte.GetProperty("queries").EnumerateArray().Select(x => x.GetString()).Should().Contain("HoleDatensatz");

            var ds = Guid.NewGuid();
            var erstellt = await Sende("/eingang/command", new { typ = "ErstelleDatensatz", daten = new { AggregateId = ds, Name = "Werkbank" } }, "t-ki");
            erstellt.StatusCode.Should().Be(HttpStatusCode.OK);
            var e1 = await Lies(erstellt);
            e1.GetProperty("ok").GetBoolean().Should().BeTrue(e1.ToString());
            e1.GetProperty("akteur").GetString().Should().Be("KIOperator");
            e1.GetProperty("events")[0].GetProperty("typ").GetString().Should().Be("DatensatzErstellt");

            var es = app.Services.GetRequiredService<IEventStoreRepository>();
            var log = await es.ReadStreamAsync(ds, 0, default);
            log.Should().Contain(e => e.Payload is Domain.Datensatz.DatensatzErstellt);
            log.First(e => e.Payload is Domain.Datensatz.DatensatzErstellt).UserId.Should().Be("KIOperator", "das Tor stempelt den Akteur");

            var nochmal = await Lies(await Sende("/eingang/command", new { typ = "ErstelleDatensatz", daten = new { AggregateId = ds, Name = "Werkbank" } }, "t-ki"));
            nochmal.GetProperty("ablehnung").GetProperty("typ").GetString().Should().Be("DatensatzExistiertBereits");
            // Einblick (Werkbank P6): der abgelehnte Command von außen ist ein gescheiterter Vorgang; seine Marke trägt den Akteur als Absender.
            var leser = new SpurLeser(u);
            var gescheitert = (await leser.VorgaengeAsync(50, default)).Single(v => v.Korrelation == nochmal.GetProperty("korrelation").GetString());
            (gescheitert.Zustand, gescheitert.Grund).Should().Be(("gescheitert", "DatensatzExistiertBereits"));
            (await leser.VorgangAsync(gescheitert.Korrelation, default)).Single().Ueber.Should().Be("akteur:KIOperator");

            (await Sende("/eingang/command", new { typ = "ErstelleDatensatz", daten = new { AggregateId = Guid.NewGuid(), Name = "x" } }, "t-inspekteur"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "der Inspekteur hat kein IDarf<ErstelleDatensatz>");
            (await Sende("/eingang/command", new { typ = "ErstelleDatensatz", daten = new { AggregateId = Guid.NewGuid(), Name = "x" } }, "falsch"))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await Sende("/eingang/command", new { typ = "GibtEsNicht", daten = new { } }, "t-ki")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            JsonElement antwort = default;
            await Warte(async () =>
            {
                antwort = await Lies(await Sende("/eingang/query", new { typ = "HoleDatensatz", daten = new { DatensatzId = ds } }, "t-ki"));
                return antwort.GetProperty("ok").GetBoolean() && antwort.GetProperty("antwort").GetProperty("typ").GetString() == "DatensatzAntwort";
            }, TimeSpan.FromSeconds(30), "Die Projektion hat den Datensatz nicht geliefert.");
            antwort.GetProperty("antwort").GetProperty("daten").GetProperty("Name").GetString().Should().Be("Werkbank");

            // ── Laufort Hand: die Dateinamen-Deutung beantwortet ein Mensch ──
            var t = new DateTimeOffset(2026, 10, 12, 9, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
            var dateiname = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_DC0.tiff";
            var vorgang = await QuellEingang.Live(app.Services).NimmAufAsync(new DateiErkannt
            {
                Name = dateiname, Pfad = Path.Combine(verz, dateiname), RelativerPfad = dateiname, Groesse = 1, GeaendertAm = t, ErkanntAm = t,
                Kennung = dateiname + "|1",
            }, default);

            JsonElement auftrag = default;
            await Warte(async () =>
            {
                var a = await Lies(await http.GetAsync("/eingang/auftraege"));
                a.GetProperty("funktionen").EnumerateArray().Select(x => x.GetString()).Should().Equal("IDateinameDeutung");
                var liste = a.GetProperty("auftraege").EnumerateArray().ToList();
                if (liste.Count == 0) return false;
                auftrag = liste.Single();
                return true;
            }, TimeSpan.FromSeconds(30), "Der Auftrag an die Hand-Funktion kam nicht an.");
            auftrag.GetProperty("funktion").GetString().Should().Be("IDateinameDeutung");
            auftrag.GetProperty("auftrag").GetProperty("typ").GetString().Should().Be("DeuteDateiname");
            auftrag.GetProperty("auftrag").GetProperty("daten").GetProperty("Dateiname").GetString().Should().Be(dateiname);
            auftrag.GetProperty("ergebnisse").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo("ImagePairDateiGedeutet", "DateinameUnbekannt");
            var vg = auftrag.GetProperty("vorgang").GetString();

            (await Sende("/eingang/ergebnis", new { vorgang = vg, typ = "BildVerkleinert", daten = new { } }, null))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, "nur ein Ergebnis-Fall der Funktion");

            var paar = Guid.NewGuid();
            var antwortHand = await Lies(await Sende("/eingang/ergebnis", new
            {
                vorgang = vg, typ = "ImagePairDateiGedeutet",
                daten = new { PaarId = paar, PairKey = "HAND-" + paar.ToString("N")[..6], Version = "Dc0", ProduziertAm = t, Pfad = Path.Combine(verz, dateiname) },
            }, null));
            antwortHand.GetProperty("ausgang").GetString().Should().Be("Geschrieben", antwortHand.ToString());

            await Warte(async () => (await es.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairErstellt), TimeSpan.FromSeconds(30),
                "Nach der Antwort von Hand lief der Fluss nicht weiter (kein ImagePairErstellt).");
            var ergebnis = (await es.ReadStreamAsync(Guid.Parse(vg!), 0, default)).Single();
            ergebnis.Wirker.Should().Be("funktion:IDateinameDeutung", "das Ergebnis schreibt die Funktion — wo gerechnet wurde, ist eine Bindung");
            (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("auftraege").GetArrayLength().Should().Be(0);
            vorgang.Should().NotBe(Guid.Empty);

            // ── Webhook aus der Konfig (konzept-werkbank §12.3 E12): POST auf die Route startet genau einen Vorgang — zweimal gesendet, derselbe Vorgang ──
            var t2 = t.AddMinutes(7);
            var datei2 = $"{t2:yyyy_MM_dd_HH_mm_ss_fff}_DC0.tiff";
            var body = new { Name = datei2, Pfad = Path.Combine(verz, datei2), RelativerPfad = datei2, Groesse = 1, GeaendertAm = t2, ErkanntAm = t2, Kennung = datei2 + "|1" };
            var w1 = await http.PostAsJsonAsync("/webhook/datei", body);
            var w2 = await http.PostAsJsonAsync("/webhook/datei", body);
            w1.StatusCode.Should().Be(HttpStatusCode.Accepted, await w1.Content.ReadAsStringAsync());
            w2.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var v1 = (await Lies(w1)).GetProperty("vorgang").GetGuid();
            (await Lies(w2)).GetProperty("vorgang").GetGuid().Should().Be(v1, "dieselbe Nachricht ist derselbe Vorgang (Kennung)");
            var wurzel = await es.ReadStreamAsync(v1, 0, default);
            wurzel.Should().ContainSingle().Which.Payload.Should().BeOfType<DateiErkannt>().Which.Name.Should().Be(datei2);
            wurzel.Single().Wirker.Should().Be("quelle:DateiErkannt");
            await Warte(async () => (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("auftraege").GetArrayLength() > 0,
                TimeSpan.FromSeconds(30), "Der Webhook hat den Bildeingang nicht gestartet (kein Auftrag an die Deutung).");
            await Task.Delay(1500);
            (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("auftraege").EnumerateArray()
                .Should().ContainSingle("genau ein Vorgang, also genau ein Auftrag").Which.GetProperty("auftrag").GetProperty("daten").GetProperty("Dateiname").GetString()
                .Should().Be(datei2);
            (await http.PostAsJsonAsync("/webhook/datei", new { Unsinn = 1 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await app.StopAsync();
            await using (var conn = new NpgsqlConnection(Verbindung + ";Database=postgres"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"drop database if exists \"{u.EventStore.Datenbank}\" with (force)";
                await cmd.ExecuteNonQueryAsync();
            }
            try { Directory.Delete(verz, true); } catch { /* egal */ }
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

    private static async Task Warte(Func<Task<bool>> bedingung, TimeSpan timeout, string meldung)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await bedingung()) return;
            await Task.Delay(200);
        }
        throw new TimeoutException(meldung);
    }
}
