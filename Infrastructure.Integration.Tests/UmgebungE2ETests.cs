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
using Domain.Laufzeit;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — UMGEBUNGEN gegen echtes Postgres (docs/06 §6.5, Werkbank P2):
/// <list type="bullet">
/// <item>das Siegel: ein Sim-Host startet nicht gegen eine Datenbank mit Siegel <c>Prod</c> (und umgekehrt);</item>
/// <item>zwei Umgebungen parallel auf einer Maschine stören sich nicht — eigene DB, eigener Cluster (Einzelknoten);</item>
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
        Cluster = new ClusterKonfig { Art = ClusterArt.Einzelknoten, Name = "cqrs-" + name },
        Transport = new TransportKonfig { GrpcPort = FreierPort(), EinblickPort = 0 },
        Funktionen = new(StringComparer.Ordinal)
        {
            ["IBildaufbereitung"] = new() { Slots = 1 },
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

    [Trait("Kategorie", "Garantie")]
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

    /// <summary>
    /// PF-18: ein Standalone-Host legt das ganze Schema beim Start an — nach dem Start stehen die Tabellen aller Read-Models (Marten kennt sie
    /// sonst erst beim ersten Zugriff) und die Regel für den Event-Zeitstempel, bevor irgendein Command oder eine Abfrage kam. In Prod (kein
    /// Abgleich der Leseseite, der sie nebenbei anlegt) ist das der einzige Weg.
    /// </summary>
    [Fact]
    public async Task Ein_Prod_Host_legt_das_ganze_Schema_beim_Start_an()
    {
        var db = "cqrs_it_schema_" + Guid.NewGuid().ToString("N")[..8];
        var u = Umgebung("it-schema-" + Guid.NewGuid().ToString("N")[..6], UmgebungsArt.Prod, db);
        var verz = Directory.CreateTempSubdirectory("umgebung-it-").FullName;
        await using (var conn = new NpgsqlConnection(Verbindung + ";Database=postgres"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"create database \"{db}\"";   // ein Prod-Host legt keine Datenbank an
            await cmd.ExecuteNonQueryAsync();
        }
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Quellen:DateiErkannt:Pfad"] = Path.Combine(verz, "quelle"),
            ["Konfig:BildaufbereitungKonfig:AusgabePfad"] = Path.Combine(verz, ".preprocessed"),
        });
        builder.AddCqrs(u);
        await using var host = builder.Build();
        try
        {
            await host.StartAsync();
            var store = host.Services.GetRequiredService<IDocumentStore>();
            var readModels = host.Services.GetServices<ReadModelTyp>().ToList();
            readModels.Should().NotBeEmpty();

            await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(u.EventStore));
            await conn.OpenAsync();
            foreach (var rm in readModels)
            {
                var tabelle = store.Options.FindOrResolveDocumentType(rm.Typ).TableName.QualifiedName;
                await using var cmd = new NpgsqlCommand("select to_regclass($1) is not null", conn);
                cmd.Parameters.Add(new NpgsqlParameter { Value = tabelle });
                ((bool)(await cmd.ExecuteScalarAsync())!).Should().BeTrue($"die Tabelle {tabelle} des Read-Models {rm.Typ.Name} steht nach dem Start");
            }
            await using (var cmd = new NpgsqlCommand(
                $"select exists(select 1 from pg_trigger where tgname = '{EventZeit.Regel}' and tgrelid = 'es.mt_events'::regclass)", conn))
                ((bool)(await cmd.ExecuteScalarAsync())!).Should().BeTrue("die Regel für den Event-Zeitstempel steht nach dem Start");
        }
        finally
        {
            await host.StopAsync();
            await LoescheDatenbankAsync(db);
            try { Directory.Delete(verz, true); } catch { /* egal */ }
        }
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
                ["Konfig:BildaufbereitungKonfig:AusgabePfad"] = Path.Combine(verz, u.Umgebung.Name, ".preprocessed"),
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
                AggregateType = "ImagePair", Modus = new CommandModus.Client(0),
                Payload = new NimmBildAuf(new(paar), "PAIR-UMGEBUNG", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/umgebung/a", BildVersion.Dc0, new BildMeta("dc0.tiff", 1, 1, 1, DateTimeOffset.UtcNow), "/umgebung/a" + ".png"),
            });
            var esA = hostA.Services.GetRequiredService<IEventStoreRepository>();
            var esB = hostB.Services.GetRequiredService<IEventStoreRepository>();
            await Warte(async () => (await esA.ReadStreamAsync(paar, 0, default)).Any(e => e.Payload is ImagePairErstellt), TimeSpan.FromSeconds(30),
                "Umgebung A hat den Command nicht verarbeitet.");
            (await esB.ReadStreamAsync(paar, 0, default)).Should().BeEmpty("Umgebung B hat eine eigene Datenbank");

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
            DocumentStore Store(UmgebungKonfig u) => DocumentStore.For(o =>
            {
                o.Connection(UmgebungLader.Verbindung(u.EventStore));
                o.DatabaseSchemaName = "es";
                o.Events.DatabaseSchemaName = "es";
                Infrastructure.Uhr.FristVerzeichnis.Registriere(o);
                EventZeit.Registriere(o, UmgebungsDbClock.Ausdruck(u));   // dieselbe Uhr wie der Host (G-10)
            });
            using var store = Store(sim);
            await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            var uhr = new UmgebungsDbClock(sim);
            var vorher = await uhr.JetztAsync();

            // Eine Frist, fällig in einer Stunde — die Zeile, die die Uhr sonst mit dem FristGesetzt schreibt.
            var stream = Guid.NewGuid();
            await using (var conn = new NpgsqlConnection(UmgebungLader.Verbindung(sim.EventStore)))
            {
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    $"insert into es.{Infrastructure.Uhr.FristVerzeichnis.Tabelle} (stream_id, version, faellig, befehl, daten) values ($1, 1, $2, 'X', '{{}}')", conn);
                cmd.Parameters.Add(new NpgsqlParameter { Value = stream });
                cmd.Parameters.Add(new NpgsqlParameter { Value = vorher.AddHours(1) });
                await cmd.ExecuteNonQueryAsync();
            }
            async Task<bool> Faellig(DocumentStore s) =>
                (await new Infrastructure.Uhr.FristVerzeichnis(s).FaelligeAsync(100, default)).Any(f => f.Stream == stream);
            (await Faellig(store)).Should().BeFalse();

            await SetzeVersatzAsync(sim, "2 hours");
            (await uhr.JetztAsync()).Should().BeCloseTo(vorher.AddHours(2), TimeSpan.FromMinutes(1));
            (await Faellig(store)).Should().BeTrue("der Versatz verschiebt die Uhr, gegen die die Fälligkeit verglichen wird");

            // Dieselbe Datenbank als Prod: die Uhr ignoriert den Versatz, die Frist ist nicht fällig.
            var alsProd = Umgebung("sim-uhr", UmgebungsArt.Prod, db);
            (await new UmgebungsDbClock(alsProd).JetztAsync()).Should().BeCloseTo(vorher, TimeSpan.FromMinutes(1));
            using var prod = Store(alsProd);
            (await Faellig(prod)).Should().BeFalse("in Prod gilt die Datenbankzeit");
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
