using Abstractions;
using Core;
using Domain.Datensatz;
using Domain.Infrastructure;
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

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Dokumente, in die MEHRERE Streams schreiben, gegen echtes Postgres mit der echten Domäne und dem GENERIERTEN
/// Marten-Schema (<c>AddGeneratedProjectionServices</c>).
///
/// Die Konsum-Maschine garantiert einen Schreiber je (Projektion, Stream). Zwei Datensätze sind zwei Streams, ihre
/// Adapter laufen parallel. Früher pflegten beide dieselbe Liste „Datensätze dieses Bildpaares“ per Lesen → Ändern →
/// Schreiben — der spätere Commit überschrieb den früheren still (Lost Update). Jetzt: Zeilen je (Datensatz, Paar).
/// Ebenso der Singleton „aktives Modell“: jedes Modell ist ein eigener Stream; „die jüngste Aktivierung gewinnt“ muss
/// nach Event-Zeit gelten, nicht nach der zufälligen Verarbeitungsreihenfolge der Streams.
/// </summary>
[Collection(GeteiltCollection.Name)]
public class GeteilteDokumentePostgresTests
{
    private readonly GeteiltFixture _fx;
    public GeteilteDokumentePostgresTests(GeteiltFixture fx) => _fx = fx;

    [Fact]
    public async Task Zwei_Datensaetze_nehmen_dieselben_Paare_gleichzeitig_auf_beide_stehen_im_Rueckwaerts_Index()
    {
        var paare = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToList();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var es = _fx.EventStore();
        foreach (var ds in new[] { a, b })
            await es.AppendEventsAsync(ds, 0,
                new IEvent[] { new DatensatzErstellt("ds-" + ds.ToString("N")[..6]) }
                    .Concat(paare.Select(p => (IEvent)new PaarAufgenommen(p))).ToList());

        // Zwei Streams, zwei Adapter, zwei Store-Instanzen (je ein Fähigkeits-Bereich) — gleichzeitig.
        await Task.WhenAll(
            Task.Run(() => DatensatzAdapter(es).WakeAsync(a)),
            Task.Run(() => DatensatzAdapter(es).WakeAsync(b)));

        var leser = new DatensatzStore(_fx.Store);
        foreach (var p in paare)
            (await leser.HoleDatensaetzeFuerPaarAsync(p)).Select(d => d.Id)
                .Should().BeEquivalentTo(new[] { a, b }, $"Paar {p} liegt in beiden Datensätzen");
    }

    [Fact]
    public async Task Paar_entfernen_nimmt_nur_die_Zeile_dieses_Datensatzes_heraus()
    {
        var p = Guid.NewGuid();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var es = _fx.EventStore();
        await es.AppendEventsAsync(a, 0, new IEvent[] { new DatensatzErstellt("a"), new PaarAufgenommen(p), new PaarEntfernt(p) });
        await es.AppendEventsAsync(b, 0, new IEvent[] { new DatensatzErstellt("b"), new PaarAufgenommen(p) });

        await DatensatzAdapter(es).WakeAsync(a);
        await DatensatzAdapter(es).WakeAsync(b);

        (await new DatensatzStore(_fx.Store).HoleDatensaetzeFuerPaarAsync(p)).Select(d => d.Id)
            .Should().BeEquivalentTo(new[] { b });
    }

    [Fact]
    public async Task Aktives_Modell_juengere_Aktivierung_gewinnt_auch_wenn_ihr_Stream_zuerst_verarbeitet_wird()
    {
        var es = _fx.EventStore();
        var alt = await ModellMitAktivierung(es);
        var neu = await ModellMitAktivierung(es);   // später angehängt → jüngere Event-Zeit

        // Umgekehrte Verarbeitungsreihenfolge: der Stream des NEUEN Modells zuerst, der des alten danach.
        await ModellAdapter(es).WakeAsync(neu);
        await ModellAdapter(es).WakeAsync(alt);

        (await new ModellStore(_fx.Store).GetAktivesAsync())!.ModellId.Should().Be(neu);
    }

    [Fact]
    public async Task Aktives_Modell_gleichzeitig_aktiviert_ergibt_immer_die_juengste_Aktivierung()
    {
        var es = _fx.EventStore();
        for (var runde = 0; runde < 5; runde++)
        {
            var modelle = new List<Guid>();
            for (var i = 0; i < 4; i++) modelle.Add(await ModellMitAktivierung(es));

            // Vier Streams schreiben gleichzeitig in DASSELBE Dokument → Versionsprüfung + Wiederholung im Adapter.
            await Task.WhenAll(modelle.Select(m => Task.Run(() => ModellAdapter(es).WakeAsync(m))));

            (await new ModellStore(_fx.Store).GetAktivesAsync())!.ModellId
                .Should().Be(modelle[^1], $"Runde {runde}: das zuletzt aktivierte Modell");
        }
    }

    // ─── Helfer ───

    private ProjectionAdapter DatensatzAdapter(MartenEventStore es)
    {
        var store = new DatensatzStore(_fx.Store);
        var projektion = new DatensatzProjektion();
        var faehigkeiten = new FaehigkeitenAus(store);
        return new ProjectionAdapter(es, store, null, projektion.SubscriberId,
            (e, w) => projektion.DispatchAsync(e, w, _ => Task.CompletedTask, faehigkeiten));
    }

    private ProjectionAdapter ModellAdapter(MartenEventStore es)
    {
        var store = new ModellStore(_fx.Store);
        var projektion = new ModellProjektion();
        var faehigkeiten = new FaehigkeitenAus(store);
        return new ProjectionAdapter(es, store, null, projektion.SubscriberId,
            (e, w) => projektion.DispatchAsync(e, w, _ => Task.CompletedTask, faehigkeiten));
    }

    private static async Task<Guid> ModellMitAktivierung(MartenEventStore es)
    {
        var id = Guid.NewGuid();
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
            opts.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);
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
