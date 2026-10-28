using Abstractions;
using Core;
using Domain.Datensatz;
using Domain.Infrastructure.Generated;
using Domain.Modell;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Domain.Infrastructure.Datensatz;
using Domain.Infrastructure.Modell;
using Domain.Projections.Datensatz;
using Domain.Projections.Modell;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Dokumente, in die MEHRERE Streams schreiben, gegen echtes Postgres mit der echten Domäne und dem GENERIERTEN
/// Marten-Schema (<c>AddGeneratedProjectionServices</c>).
///
/// Die Konsum-Maschine garantiert einen Schreiber je (Projektion, Partition) und damit je Stream. Zwei Datensätze in zwei Partitionen
/// laufen in zwei Batch-Lesern parallel. Früher pflegten beide dieselbe Liste „Datensätze dieses Bildpaares“ per Lesen → Ändern →
/// Schreiben — der spätere Commit überschrieb den früheren still (Lost Update). Jetzt: Zeilen je (Datensatz, Paar).
/// Ebenso der Singleton „aktives Modell“: jedes Modell ist ein eigener Stream; „die jüngste Aktivierung gewinnt“ muss
/// nach Event-Zeit gelten, nicht nach der zufälligen Verarbeitungsreihenfolge der Partitionen.
/// </summary>
[Collection(GeteiltCollection.Name)]
[Trait("Kategorie", "Garantie")]
public class GeteilteDokumentePostgresTests
{
    private readonly GeteiltFixture _fx;
    public GeteilteDokumentePostgresTests(GeteiltFixture fx) => _fx = fx;

    private static readonly Konsum VierPartitionen = new(4, 500);

    [Fact]
    public async Task Zwei_Datensaetze_nehmen_dieselben_Paare_gleichzeitig_auf_beide_stehen_im_Rueckwaerts_Index()
    {
        var paare = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToList();
        var (a, b) = (BatchHilfe.StreamIn(0, 4), BatchHilfe.StreamIn(1, 4));
        var proj = await Frisch("datensatz");
        var es = _fx.EventStore();
        foreach (var ds in new[] { a, b })
            await es.AppendEventsAsync(ds, 0,
                new IEvent[] { new DatensatzErstellt("ds-" + ds.ToString("N")[..6]) }
                    .Concat(paare.Select(p => (IEvent)new PaarAufgenommen(new(p)))).ToList());

        // Zwei Partitionen, zwei Batch-Leser, zwei Store-Instanzen (je ein Fähigkeits-Bereich) — gleichzeitig.
        await Task.WhenAll(
            Task.Run(() => DatensatzLeser(es, proj, 0).LaufAsync()),
            Task.Run(() => DatensatzLeser(es, proj, 1).LaufAsync()));

        var leser = new DatensatzStore(_fx.Store);
        foreach (var p in paare)
            (await leser.HoleDatensaetzeFuerPaarAsync(new(p))).Select(d => d.Id.Wert)
                .Should().BeEquivalentTo(new[] { a, b }, $"Paar {p} liegt in beiden Datensätzen");
    }

    [Fact]
    public async Task Paar_entfernen_nimmt_nur_die_Zeile_dieses_Datensatzes_heraus()
    {
        var p = Guid.NewGuid();
        var (a, b) = (BatchHilfe.StreamIn(0, 4), BatchHilfe.StreamIn(1, 4));
        var proj = await Frisch("datensatz");
        var es = _fx.EventStore();
        await es.AppendEventsAsync(a, 0, new IEvent[] { new DatensatzErstellt("a"), new PaarAufgenommen(new(p)), new PaarEntfernt(new(p)) });
        await es.AppendEventsAsync(b, 0, new IEvent[] { new DatensatzErstellt("b"), new PaarAufgenommen(new(p)) });

        await DatensatzLeser(es, proj, 0).LaufAsync();
        await DatensatzLeser(es, proj, 1).LaufAsync();

        (await new DatensatzStore(_fx.Store).HoleDatensaetzeFuerPaarAsync(new(p))).Select(d => d.Id.Wert)
            .Should().BeEquivalentTo(new[] { b });
    }

    [Fact]
    public async Task Aktives_Modell_juengere_Aktivierung_gewinnt_auch_wenn_ihr_Stream_zuerst_verarbeitet_wird()
    {
        var es = _fx.EventStore();
        var proj = await Frisch("modell");
        var alt = await ModellMitAktivierung(es, BatchHilfe.StreamIn(0, 4));
        var neu = await ModellMitAktivierung(es, BatchHilfe.StreamIn(1, 4));   // später angehängt → jüngere Event-Zeit

        // Umgekehrte Verarbeitungsreihenfolge: die Partition des NEUEN Modells zuerst, die des alten danach.
        await ModellLeser(es, proj, 1).LaufAsync();
        await ModellLeser(es, proj, 0).LaufAsync();

        (await new ModellStore(_fx.Store).GetAktivesAsync())!.ModellId.Wert.Should().Be(neu);
    }

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

    private BatchLeser DatensatzLeser(MartenEventStore es, string proj, int partition)
    {
        var store = new DatensatzStore(_fx.Store);
        var projektion = new DatensatzProjektion();
        var faehigkeiten = new FaehigkeitenAus(store);
        return new BatchLeser(es, new Projektionsnaht(store, null), proj, partition, VierPartitionen, BatchLeser.TypenAus(GeneratedProjektionen.DatensatzProjektion_Typen),
            (e, ct) => GeneratedProjektionen.DatensatzProjektion_Dispatch(projektion, e, faehigkeiten, ct));
    }

    private BatchLeser ModellLeser(MartenEventStore es, string proj, int partition)
    {
        var store = new ModellStore(_fx.Store);
        var projektion = new ModellProjektion();
        var faehigkeiten = new FaehigkeitenAus(store);
        return new BatchLeser(es, new Projektionsnaht(store, null), proj, partition, VierPartitionen, BatchLeser.TypenAus(GeneratedProjektionen.ModellProjektion_Typen),
            (e, ct) => GeneratedProjektionen.ModellProjektion_Dispatch(projektion, e, faehigkeiten, ct));
    }

    private static async Task<Guid> ModellMitAktivierung(MartenEventStore es, Guid id)
    {
        await es.AppendEventsAsync(id, 0, new IEvent[]
        {
            new ModellRegistriert(new(Guid.NewGuid()), new(Guid.NewGuid()), 1, "m", "/m.pt", new ModellMetriken(0.1, 0.9)),
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
            opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;   // wie der Host; der Horizont setzt ihn voraus
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
