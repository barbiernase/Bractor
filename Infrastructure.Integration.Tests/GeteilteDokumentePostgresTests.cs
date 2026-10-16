using Abstractions;
using Core;
using Domain.Infrastructure.Generated;
using Domain.Modell;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Domain.Infrastructure.Modell;
using Domain.Projections.Modell;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ein Dokument, in das MEHRERE Streams schreiben, gegen echtes Postgres mit dem GENERIERTEN Marten-Schema
/// (<c>AddGeneratedProjectionServices</c>): der Singleton „aktives Modell“ der Beispieldomäne, vier Partitions-Leser gleichzeitig. Bewiesen
/// wird die generierte Optimistic-Concurrency-Registrierung — die Wiederholung im Leser selbst prüft <see cref="SchreibRegelPostgresTests"/>
/// domänenneutral.
/// </summary>
[Collection(GeteiltCollection.Name)]
public class GeteilteDokumentePostgresTests
{
    private readonly GeteiltFixture _fx;
    public GeteilteDokumentePostgresTests(GeteiltFixture fx) => _fx = fx;

    private static readonly Konsum VierPartitionen = new(4, 500);

    [Fact]
    public async Task Aktives_Modell_gleichzeitig_aktiviert_ergibt_immer_die_juengste_Aktivierung()
    {
        var es = _fx.EventStore();
        var proj = await Frisch("modell");
        for (var runde = 0; runde < 5; runde++)
        {
            var modelle = new List<Guid>();
            for (var i = 0; i < 4; i++) modelle.Add(await ModellMitAktivierung(es, BatchHilfe.StreamIn(i, 4)));

            // Vier Partitions-Leser schreiben gleichzeitig in DASSELBE Dokument → Versionsprüfung, Trennen und Wiederholung im Leser.
            await Task.WhenAll(Enumerable.Range(0, 4).Select(p => Task.Run(() => ModellLeser(es, proj, p).LaufAsync())));

            (await new ModellStore(_fx.Store).GetAktivesAsync())!.ModellId
                .Should().Be(modelle[^1], $"Runde {runde}: das zuletzt aktivierte Modell");
        }
    }

    // ─── Helfer ───

    /// <summary>Ein frischer Projektions-Schlüssel am Kopf des Logs (die Datenbank lebt über viele Läufe).</summary>
    private async Task<string> Frisch(string projektion)
    {
        var proj = $"{projektion}-it-{Guid.NewGuid():N}";
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, VierPartitionen);
        return proj;
    }

    private BatchLeser ModellLeser(MartenEventStore es, string proj, int partition)
    {
        var store = new ModellStore(_fx.Store);
        var projektion = new ModellProjektion();
        var faehigkeiten = new FaehigkeitenAus(store);
        return new BatchLeser(es, store, proj, partition, VierPartitionen, BatchLeser.TypenAus(ModellProjektion.SubscribedTypes),
            (e, w) => projektion.DispatchAsync(e, w, BatchLeser.KeineAusgabe, faehigkeiten));
    }

    private static async Task<Guid> ModellMitAktivierung(MartenEventStore es, Guid id)
    {
        await es.AppendEventsAsync(id, 0, new IEvent[]
        {
            new ModellRegistriert(Guid.NewGuid(), Guid.NewGuid(), 1, "m", "/m.pt", new ModellMetriken(0.1, 0.9)),
            new ModellAktiviert("/m.pt", "m")
        });
        return id;
    }
}

[CollectionDefinition(Name)]
public sealed class GeteiltCollection : ICollectionFixture<GeteiltFixture>
{
    public const string Name = "Geteilte Dokumente (eigene Datenbank)";
}

/// <summary>
/// Eigene Datenbank <c>cqrs_it_geteilt</c>: das generierte Schema legt ReadModels fest ins Schema <c>rm</c> — in der
/// Dev-Datenbank träfe der Test den echten Singleton „aktives Modell“. Marten wie im Host über DI, mit
/// <c>AddGeneratedProjectionServices</c> (so prüft der Test auch die generierte Optimistic-Concurrency-Registrierung).
/// </summary>
public sealed class GeteiltFixture : IAsyncLifetime
{
    private const string Datenbank = "cqrs_it_geteilt";
    private const string Server = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private ServiceProvider _sp = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await using (var c = new NpgsqlConnection(Server + ";Database=postgres"))
        {
            await c.OpenAsync();
            await using var pruef = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{Datenbank}'", c);
            if (await pruef.ExecuteScalarAsync() is null)
                await new NpgsqlCommand($"CREATE DATABASE {Datenbank}", c).ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarten(opts =>
        {
            opts.Connection(Server + ";Database=" + Datenbank);
            opts.DatabaseSchemaName = "it_geteilt";
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            KonsumSchema.Registriere(opts);
            opts.Schema.For<SondenZaehler>().Identity(x => x.Id).UseOptimisticConcurrency(true);
            opts.Schema.For<SondenEigen>().Identity(x => x.Id);
        });
        services.AddGeneratedProjectionServices();
        _sp = services.BuildServiceProvider();
        Store = _sp.GetRequiredService<IDocumentStore>();
    }

    public MartenEventStore EventStore()
        => new(Store, new KeineAggregate(), NullLogger<MartenEventStore>.Instance);

    public async Task DisposeAsync() => await _sp.DisposeAsync();

    private sealed class KeineAggregate : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadStream.");
    }
}
