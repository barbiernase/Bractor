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
using Domain.Laufzeit;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — DURCHSPIELEN gegen echtes Postgres (docs/konzept-werkbank.md §6, Werkbank P5): der JSON-Eingang des Hosts.
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
            Cluster = new ClusterKonfig { Art = ClusterArt.Einzelknoten, Name = "cqrs-" + name },
            Transport = new TransportKonfig { GrpcPort = FreierPort(), EinblickPort = FreierPort(), JsonEingang = true },
            Funktionen = new(StringComparer.Ordinal)
            {
                ["IBildaufbereitung"] = new() { Laufort = FunktionsLaufort.Hand },
            },
        };
        var verz = Directory.CreateTempSubdirectory("eingang-it-").FullName;
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Quellen:DateiErkannt:Pfad"] = Path.Combine(verz, "eingang"),
            ["Konfig:BildaufbereitungKonfig:AusgabePfad"] = Path.Combine(verz, ".preprocessed"),
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
            // Die geschriebenen Events zeigt der Eingang auf Wunsch — gelesen aus dem Log (die Antwort des Aggregats nennt nur den Bereich).
            var erstellt = await Sende("/eingang/command", new { typ = "ErstelleDatensatz", daten = new { AggregateId = ds, Name = "Werkbank" }, zeigeEvents = true }, "t-ki");
            erstellt.StatusCode.Should().Be(HttpStatusCode.OK);
            var e1 = await Lies(erstellt);
            e1.GetProperty("ok").GetBoolean().Should().BeTrue(e1.ToString());
            e1.GetProperty("akteur").GetString().Should().Be("KIOperator");
            e1.GetProperty("events")[0].GetProperty("typ").GetString().Should().Be("DatensatzErstellt");
            e1.GetProperty("ersteVersion").GetInt32().Should().Be(1);

            var es = app.Services.GetRequiredService<IEventStoreRepository>();
            var log = await es.ReadStreamAsync(ds, 0, default);
            log.Should().Contain(e => e.Payload is Domain.Datensatz.DatensatzErstellt);
            log.First(e => e.Payload is Domain.Datensatz.DatensatzErstellt).UserId.Should().Be("KIOperator", "das Tor stempelt den Akteur");

            var nochmal = await Lies(await Sende("/eingang/command", new { typ = "ErstelleDatensatz", daten = new { AggregateId = ds, Name = "Werkbank" } }, "t-ki"));
            nochmal.GetProperty("ablehnung").GetProperty("typ").GetString().Should().Be("DatensatzExistiertBereits");
            nochmal.GetProperty("events").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, "ohne Wunsch liest der Eingang das Log nicht");
            // Eine Ablehnung steht nicht im Log (Plan Pub/Sub-Neustrukturierung Phase 5): der Stream bleibt, wie er war, der Einblick kennt keinen
            //   Vorgang dazu — „abgelehnt“ hält nur die Inbox-Tabelle.
            (await es.ReadStreamAsync(ds, 0, default)).Should().HaveCount(log.Count, "eine Ablehnung schreibt kein Event");
            var leser = new SpurLeser(u);
            (await leser.VorgaengeAsync(50, default)).Should().NotContain(v => v.Korrelation == nochmal.GetProperty("korrelation").GetString());
            (await leser.VorgangAsync(nochmal.GetProperty("korrelation").GetString()!, default)).Should().BeEmpty();

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

            // ── Laufort Hand: die Bildaufbereitung (Funktion als Schnittstelle) beantwortet ein Mensch ──
            var t = new DateTimeOffset(2026, 10, 12, 9, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
            var dateiname = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_DC0.tiff";
            var vorgang = await QuellEingang.Live(app.Services).NimmAufAsync(new DateiErkannt
            {
                Name = dateiname, Pfad = Path.Combine(verz, dateiname), RelativerPfad = dateiname, Groesse = 1, GeaendertAm = t, ErkanntAm = t,
                Kennung = dateiname + "|1",
            }, default);

            JsonElement eingang = default;
            await Warte(async () =>
            {
                var liste = (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("eingaenge").EnumerateArray().ToList();
                if (liste.Count == 0) return false;
                eingang = liste.Single();
                return true;
            }, TimeSpan.FromSeconds(30), "Der Eingang an die Hand-Funktion kam nicht an.");
            eingang.GetProperty("funktion").GetString().Should().Be("IBildaufbereitung");
            eingang.GetProperty("eingang").GetProperty("typ").GetString().Should().Be("DateiErkannt");
            eingang.GetProperty("eingang").GetProperty("daten").GetProperty("Name").GetString().Should().Be(dateiname);
            eingang.GetProperty("ausgaben").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo("BildAufbereitet", "BildNichtLesbar");
            var vg = eingang.GetProperty("vorgang").GetString();

            (await Sende("/eingang/ergebnis", new { vorgang = vg, ausgaben = new[] { new { typ = "NimmBildAuf", daten = new { } } } }, null))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, "nur eine Ausgabe aus der Signatur der Funktion");

            var antwortHand = await Lies(await Sende("/eingang/ergebnis", new
            {
                vorgang = vg,
                ausgaben = new[]
                {
                    new
                    {
                        typ = "BildAufbereitet",
                        daten = (object)new
                        {
                            Dateiname = dateiname, Pfad = Path.Combine(verz, dateiname), Groesse = 1L, ErkanntAm = t,
                            Vorschau = Path.Combine(verz, "hand.png"), BreitePixel = 4, HoehePixel = 2, ErstelltAm = t,
                        },
                    },
                },
            }, null));
            antwortHand.GetProperty("ausgang").GetString().Should().Be("Geschrieben", antwortHand.ToString());

            var paar = ImagePairFileName.Resolve(dateiname)!.AggregateId;
            await Warte(async () => (await es.ReadStreamAsync(paar.Wert, 0, default)).Any(e => e.Payload is ImagePairErstellt), TimeSpan.FromSeconds(30),
                "Nach der Antwort von Hand lief es nicht weiter (kein ImagePairErstellt über den Bildeingang).");
            var festgehalten = await es.ReadStreamAsync(Infrastructure.Funktionen.FunktionsIds.Festhalten("IBildaufbereitung", vorgang, 1), 0, default);
            festgehalten.Should().ContainSingle(e => e.Payload is Domain.Pipeline.Bildaufbereitung.BildAufbereitet);
            festgehalten.Should().OnlyContain(e => e.Wirker == "funktion:IBildaufbereitung", "die Ausgabe schreibt die Funktion — wo gerechnet wurde, ist eine Bindung");
            (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("eingaenge").GetArrayLength().Should().Be(0);
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
            await Warte(async () => (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("eingaenge").GetArrayLength() > 0,
                TimeSpan.FromSeconds(30), "Der Webhook hat keinen Eingang an die Bildaufbereitung gegeben.");
            await Task.Delay(1500);
            (await Lies(await http.GetAsync("/eingang/auftraege"))).GetProperty("eingaenge").EnumerateArray()
                .Should().ContainSingle("genau eine Nachricht im Log, also genau ein Eingang").Which.GetProperty("eingang").GetProperty("daten").GetProperty("Name").GetString()
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

    /// <summary>
    /// Garantie (docs/11 §11.3): ein Client, der nach verlorener Antwort mit DERSELBEN CommandId wiederholt, wirkt genau einmal — auch bei einem
    /// Command, der fachlich nicht idempotent ist (jede Range ist ein neues Event).
    /// </summary>
    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Client_wiederholt_mit_derselben_CommandId_und_wirkt_genau_einmal()
    {
        var name = "sim-wiederholung-" + Guid.NewGuid().ToString("N")[..6];
        var u = new UmgebungKonfig
        {
            Umgebung = new UmgebungKopf { Name = name, Art = UmgebungsArt.Sim },
            EventStore = new EventStoreKonfig { Verbindung = Verbindung, Datenbank = "cqrs_it_wiederholung_" + Guid.NewGuid().ToString("N")[..8], Schema = "es" },
            Cluster = new ClusterKonfig { Art = ClusterArt.Einzelknoten, Name = "cqrs-" + name },
            Transport = new TransportKonfig { GrpcPort = FreierPort(), EinblickPort = FreierPort(), JsonEingang = true },
            Funktionen = new(StringComparer.Ordinal)
            {
                ["IBildaufbereitung"] = new() { Slots = 1 },
            },
        };
        var verz = Directory.CreateTempSubdirectory("wiederholung-it-").FullName;
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Quellen:DateiErkannt:Pfad"] = Path.Combine(verz, "eingang"),
            ["Konfig:BildaufbereitungKonfig:AusgabePfad"] = Path.Combine(verz, ".preprocessed"),
            ["Identitaeten:Tokens:t-ki"] = "KIOperator",
        });
        builder.AddCqrs(u);
        await using var app = builder.Build();
        app.MapEingang(u);
        using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{u.Transport.EinblickPort}") };
        async Task<JsonElement> Sende(object inhalt)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/eingang/command") { Content = JsonContent.Create(inhalt) };
            req.Headers.Add("akteur-token", "t-ki");
            return JsonDocument.Parse(await (await http.SendAsync(req)).Content.ReadAsStringAsync()).RootElement;
        }

        await app.StartAsync();
        try
        {
            var ds = Guid.NewGuid();
            (await Sende(new { typ = "ErstelleDatensatz", daten = new { AggregateId = ds, Name = "Wiederholung" } })).GetProperty("ok").GetBoolean().Should().BeTrue();
            var commandId = Guid.NewGuid();
            var range = new { typ = "FuegeRangeHinzu", commandId, daten = new { AggregateId = ds, Kriterien = new { } } };

            var erste = await Sende(range);
            var wiederholt = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Sende(range)));

            erste.GetProperty("ok").GetBoolean().Should().BeTrue(erste.ToString());
            wiederholt.Should().OnlyContain(w => w.GetProperty("ok").GetBoolean(), "die Wiederholung bekommt idempotent dasselbe Ergebnis");
            var log = await app.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(ds, 0, default);
            log.Count(e => e.Payload is Domain.Datensatz.RangeAngefordert).Should().Be(1, "dieselbe CommandId wirkt genau einmal");
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
