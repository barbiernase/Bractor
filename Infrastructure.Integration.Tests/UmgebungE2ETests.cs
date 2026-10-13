using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Umgebung;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — UMGEBUNGEN gegen echtes Postgres/Redis (docs/konzept-werkbank.md §4, Werkbank P2):
/// <list type="bullet">
/// <item>das Siegel: ein Sim-Host startet nicht gegen eine Datenbank mit Siegel <c>Prod</c> (und umgekehrt);</item>
/// <item>zwei Umgebungen parallel auf einer Maschine stören sich nicht — eigene DB, eigenes Redis-Präfix, eigener Cluster (Einzelknoten);</item>
/// <item>der Uhr-Versatz einer Sim-Umgebung macht eine Frist früher fällig; in Prod wirkt er nie.</item>
/// </list>
/// </summary>
public class UmgebungE2ETests
{
    private const string Verbindung = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private static UmgebungKonfig Umgebung(string name, UmgebungsArt art, string db) => new()
    {
        Umgebung = new UmgebungKopf { Name = name, Art = art },
        EventStore = new EventStoreKonfig { Verbindung = Verbindung, Datenbank = db, Schema = "es" },
        VersionIndex = new VersionIndexKonfig { Praefix = name + ":" },
        Cluster = new ClusterKonfig { Art = ClusterArt.Einzelknoten, Name = "cqrs-" + name },
        Transport = new TransportKonfig { GrpcPort = FreierPort(), EinblickPort = 0 },
        Funktionen = new(StringComparer.Ordinal)
        {
            ["IBildVerkleinerung"] = new() { Slots = 1 }, ["IHistogrammAusgleich"] = new() { Slots = 1 }, ["IDateinameDeutung"] = new() { Slots = 1 },
            ["IRangeSuche"] = new() { Slots = 1 }, ["IMitgliederEinfrieren"] = new() { Slots = 1 },
        },
    };

    private static async Task LoescheDatenbankAsync(string db)
    {
        await using var conn = new NpgsqlConnection(Verbindung + ";Database=postgres");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"drop database if exists \"{db}\" with (force)";
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Sim_Host_startet_nicht_gegen_eine_Datenbank_mit_Siegel_Prod()
    {
        var db = "cqrs_it_siegel_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            // Erst siegelt ein Prod-Start die (vorhandene) DB …
            await using (var conn = new NpgsqlConnection(Verbindung + ";Database=postgres"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"create database \"{db}\"";
                await cmd.ExecuteNonQueryAsync();
            }
            var prod = Umgebung("prod", UmgebungsArt.Prod, db);
            await new UmgebungsSiegel(prod).StartAsync(default);

            // … dann darf weder ein Sim-Host noch ein Prod-Host einer anderen Umgebung (anderer Cluster) dort starten.
            var sim = Umgebung("sim-x", UmgebungsArt.Sim, db);
            await FluentActions.Awaiting(() => new UmgebungsSiegel(sim).StartAsync(default))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Prod-Umgebung*");
            var andererCluster = Umgebung("prod", UmgebungsArt.Prod, db);
            andererCluster.Cluster.Name = "cqrs-anders";
            await FluentActions.Awaiting(() => new UmgebungsSiegel(andererCluster).StartAsync(default))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cluster*");
            await new UmgebungsSiegel(prod).StartAsync(default);   // die eigene Umgebung startet weiter
        }
        finally { await LoescheDatenbankAsync(db); }
    }

    [Fact]
    public async Task Zwei_Umgebungen_parallel_stoeren_sich_nicht()
    {
        var a = Umgebung("it-a-" + Guid.NewGuid().ToString("N")[..6], UmgebungsArt.Sim, "cqrs_it_a_" + Guid.NewGuid().ToString("N")[..8]);
        var b = Umgebung("it-b-" + Guid.NewGuid().ToString("N")[..6], UmgebungsArt.Sim, "cqrs_it_b_" + Guid.NewGuid().ToString("N")[..8]);
        var verz = Directory.CreateTempSubdirectory("umgebung-it-").FullName;
        WebApplication Baue(UmgebungKonfig u)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Quellen:DateiErkannt:Pfad"] = Path.Combine(verz, u.Umgebung.Name),
                ["Konfig:PreprocessingConfig:OutputPath"] = Path.Combine(verz, u.Umgebung.Name, ".preprocessed"),
            });
            builder.AddCqrs(u);
            return builder.Build();
        }
        await using var hostA = Baue(a);
        await using var hostB = Baue(b);
        try
        {
            await hostA.StartAsync();
            await hostB.StartAsync();

            var paar = Guid.NewGuid();
            hostA.Services.GetRequiredService<IAggregateDispatcher>().Dispatch(new CommandEnvelope
            {
                AggregateId = paar, AggregateType = "ImagePair", Modus = new CommandModus.Client(0),
                Payload = new ErstelleImagePair(paar, "PAIR-UMGEBUNG", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/umgebung/a"),
            });
            var esA = hostA.Services.GetRequiredService<IEventStoreRepository>();
            var esB = hostB.Services.GetRequiredService<IEventStoreRepository>();
            await Warte(async () => (await esA.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairErstellt), TimeSpan.FromSeconds(30),
                "Umgebung A hat den Command nicht verarbeitet.");
            (await esB.ReadStreamAsync(paar, 0, default)).Should().BeEmpty("Umgebung B hat eine eigene Datenbank");

            var redis = hostA.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>().GetDatabase(1);
            await Warte(async () => await redis.KeyExistsAsync($"{a.Umgebung.Name}:agg:{paar}"), TimeSpan.FromSeconds(10),
                "Der Version-Index von A steht nicht unter A's Präfix.");
            (await redis.KeyExistsAsync($"{b.Umgebung.Name}:agg:{paar}")).Should().BeFalse("B's Präfix bleibt leer");
            (await redis.KeyExistsAsync($"agg:{paar}")).Should().BeFalse("ohne Präfix schreibt keine Umgebung");
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
            await LoescheDatenbankAsync(a.EventStore.Datenbank);
            await LoescheDatenbankAsync(b.EventStore.Datenbank);
            try { Directory.Delete(verz, true); } catch { /* egal */ }
        }
    }

    [Fact]
    public async Task Uhr_Versatz_macht_eine_Frist_in_Sim_frueher_faellig_in_Prod_nie()
    {
        var db = "cqrs_it_uhr_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var sim = Umgebung("sim-uhr", UmgebungsArt.Sim, db);
            await new UmgebungsSiegel(sim).StartAsync(default);   // legt DB, Siegel und Uhr-Tabelle an
            using var store = DocumentStore.For(o =>
            {
                o.Connection(UmgebungLader.Verbindung(sim.EventStore));
                o.DatabaseSchemaName = "es";
                o.Schema.For<Frist>().Identity(x => x.Id);
            });
            var plan = new MartenFristplan(store);
            var uhr = new UmgebungsDbClock(sim);
            var vorher = await uhr.JetztAsync();
            var frist = new Frist(Guid.NewGuid(), vorher.AddHours(1), Guid.NewGuid(), "in-einer-stunde");
            await plan.PlaneAsync(frist);
            (await plan.FälligeAsync(await uhr.JetztAsync())).Select(f => f.Id).Should().NotContain(frist.Id);

            await SetzeVersatzAsync(sim, "2 hours");
            var nachher = await uhr.JetztAsync();
            nachher.Should().BeCloseTo(vorher.AddHours(2), TimeSpan.FromMinutes(1));
            (await plan.FälligeAsync(nachher)).Select(f => f.Id).Should().Contain(frist.Id, "der Versatz verschiebt die DB-Uhr");

            // Dieselbe Uhr in einer Prod-Umgebung ignoriert den Versatz.
            var alsProd = Umgebung("sim-uhr", UmgebungsArt.Prod, db);
            (await new UmgebungsDbClock(alsProd).JetztAsync()).Should().BeCloseTo(vorher, TimeSpan.FromMinutes(1));
        }
        finally { await LoescheDatenbankAsync(db); }
    }

    private static async Task SetzeVersatzAsync(UmgebungKonfig u, string intervall)
    {
        await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(u.EventStore));
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"update \"{u.EventStore.Schema}\".uhr set versatz = interval '{intervall}' where id = 1";
        await cmd.ExecuteNonQueryAsync();
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
            await Task.Delay(100);
        }
        throw new TimeoutException(meldung);
    }
}
