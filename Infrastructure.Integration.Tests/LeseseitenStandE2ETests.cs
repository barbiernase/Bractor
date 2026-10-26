using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Abstractions;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Umgebung;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Domain.Projections.ImagePair;
using Domain.Laufzeit;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — „Passt die Leseseite zum Code?" gegen echtes Postgres (docs/konzept-werkbank.md §4.6 und §12.3 E9, Werkbank P3.4):
/// ein Sim-Host, dessen gespeicherter Projektions-Stand nicht zum Code-Hash passt, leert beim Start die Leseseite und baut sie aus dem
/// Log neu auf; passt der Stand, bleibt sie unangetastet. Bewiesen über ein fremdes Read-Model-Dokument, das kein Stream erzeugt hat:
/// es überlebt nur den Start ohne Code-Änderung.
/// </summary>
public class LeseseitenStandE2ETests
{
    private const string Verbindung = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private static UmgebungKonfig Umgebung(string name, string db) => new()
    {
        Umgebung = new UmgebungKopf { Name = name, Art = UmgebungsArt.Sim },
        EventStore = new EventStoreKonfig { Verbindung = Verbindung, Datenbank = db, Schema = "es" },
        VersionIndex = new VersionIndexKonfig { Praefix = name + ":" },
        Cluster = new ClusterKonfig { Art = ClusterArt.Einzelknoten, Name = "cqrs-" + name },
        Transport = new TransportKonfig { GrpcPort = FreierPort(), EinblickPort = 0 },
        Funktionen = new(StringComparer.Ordinal)
        {
            ["IBildaufbereitung"] = new() { Slots = 1 },
        },
    };

    [Fact]
    public async Task Geaenderter_Projektions_Code_baut_die_Leseseite_in_Sim_neu_auf()
    {
        var u = Umgebung("it-stand-" + Guid.NewGuid().ToString("N")[..6], "cqrs_it_stand_" + Guid.NewGuid().ToString("N")[..8]);
        var verz = Directory.CreateTempSubdirectory("stand-it-").FullName;
        WebApplication Baue()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Quellen:DateiErkannt:Pfad"] = verz, ["Konfig:BildaufbereitungKonfig:AusgabePfad"] = Path.Combine(verz, ".preprocessed") });
            builder.AddCqrs(u);
            return builder.Build();
        }
        var paar = Guid.NewGuid();
        var fremd = Guid.NewGuid();
        try
        {
            // 1. Erster Start: ein Paar entsteht, die Projektion schreibt sein Read-Model; der Stand wird gespeichert.
            await using (var host = Baue())
            {
                await host.StartAsync();
                host.Services.GetRequiredService<IAggregateDispatcher>().Dispatch(new CommandEnvelope
                {
                    AggregateType = "ImagePair", Modus = new CommandModus.Client(0),
                    Payload = new NimmBildAuf(new(paar), "PAIR-STAND", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "/stand", BildVersion.Dc0, new BildMeta("dc0.tiff", 1, 1, 1, DateTimeOffset.UtcNow), "/stand" + ".png"),
                });
                var marten = host.Services.GetRequiredService<IDocumentStore>();
                await Warte(async () => { await using var s = marten.QuerySession(); return await s.LoadAsync<ImagePairReadModel>(paar) != null; },
                    TimeSpan.FromSeconds(30), "Die Projektion hat das Paar nicht geschrieben.");
                // Ein Dokument, das kein Stream erzeugt hat — es verschwindet nur, wenn die Leseseite geleert wird.
                await using (var s = marten.LightweightSession()) { s.Store(new ImagePairReadModel { Id = fremd, PairKey = "FREMD" }); await s.SaveChangesAsync(); }
                await host.StopAsync();
            }
            (await StandAsync(u)).Should().ContainKey("projektion:imagepair-projektion");

            // 2. Neustart ohne Code-Änderung: nichts wird geleert.
            await using (var host = Baue())
            {
                await host.StartAsync();
                await using (var s = host.Services.GetRequiredService<IDocumentStore>().QuerySession())
                    (await s.LoadAsync<ImagePairReadModel>(fremd)).Should().NotBeNull("der Code ist unverändert");
                await host.StopAsync();
            }

            // 3. Der gespeicherte Stand passt nicht mehr (= Projektions-Code geändert): die Leseseite wird geleert und neu aufgebaut.
            await SetzeStandAsync(u, "projektion:imagepair-projektion", "alt");
            await using (var host = Baue())
            {
                await host.StartAsync();
                var marten = host.Services.GetRequiredService<IDocumentStore>();
                await using (var s = marten.QuerySession())
                    (await s.LoadAsync<ImagePairReadModel>(fremd)).Should().BeNull("die Leseseite wurde geleert");
                await Warte(async () => { await using var s = marten.QuerySession(); return (await s.LoadAsync<ImagePairReadModel>(paar))?.PairKey == "PAIR-STAND"; },
                    TimeSpan.FromSeconds(30), "Die Projektion hat das Paar nach dem Leeren nicht aus dem Log neu aufgebaut.");
                await host.StopAsync();
            }
            (await StandAsync(u))["projektion:imagepair-projektion"].Should().NotBe("alt", "der neue Stand ist gespeichert");
        }
        finally
        {
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

    private static async Task<Dictionary<string, string>> StandAsync(UmgebungKonfig u)
    {
        await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(u.EventStore));
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"select schluessel, wert from \"{u.EventStore.Schema}\".stand";
        var aus = new Dictionary<string, string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) aus[r.GetString(0)] = r.GetString(1);
        return aus;
    }

    private static async Task SetzeStandAsync(UmgebungKonfig u, string schluessel, string wert)
    {
        await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(u.EventStore));
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"update \"{u.EventStore.Schema}\".stand set wert = @w where schluessel = @k";
        cmd.Parameters.AddWithValue("k", schluessel);
        cmd.Parameters.AddWithValue("w", wert);
        (await cmd.ExecuteNonQueryAsync()).Should().Be(1);
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
