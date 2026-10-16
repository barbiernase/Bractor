using Abstractions;
using Core;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Domain.Projections.ImagePair;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Co-Commit des Batch-Konsums gegen ECHTES Postgres — das größte Risiko des Projekts (docs/04 §4.3).
///
/// Beweist die Store-seitige exactly-once-Wirksamkeit im realen Marten-Store: die Effekte eines Batches über VIELE Streams
/// (Historien-Appends), die Guards dieser Streams und der Partitions-Checkpoint werden über EINE Session mit EINEM SaveChanges gemeinsam
/// gültig. Ein Absturz mitten im Batch (nach den Effekten, vor dem Commit) verwirft alles → der nächste Durchlauf wiederholt → GENAU EIN
/// Eintrag je Event.
///
/// Braucht laufendes Postgres auf localhost:5432 (cqrs_events / postgres / postgres) — im Dev-Setup über docker-compose.infrastructure.yml.
/// </summary>
public class CoCommitPostgresTests : IClassFixture<PostgresFixture>
{
    private static readonly Konsum EinePartition = new(1, 500);
    private readonly PostgresFixture _fx;
    public CoCommitPostgresTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Viele_Streams_eines_Batches_werden_in_einem_Commit_gueltig()
    {
        // Read-your-writes der IdentitySession über Streams hinweg: je Stream drei Appends an dasselbe Dokument in EINEM Batch dürfen sich
        // nicht überschreiben; und der ganze Batch ist EIN Commit (die Version der Partition steigt um genau eins).
        var proj = await FrischeProjektion();
        var streams = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();
        var es = EventStore();
        foreach (var s in streams)
            await es.AppendEventsAsync(s, 0, Enumerable.Range(0, 3).Select(_ => (IEvent)new ImagePairInspiziert()).ToList());
        var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var vorher = (await BatchHilfe.PartitionAsync(_fx.Store, proj, 0))!.Version;

        (await BatchHilfe.HistorieLeser(es, store, proj, 0, EinePartition).LaufAsync()).Should().Be(60);

        foreach (var s in streams)
        {
            (await store.GetByPairIdAsync(s))!.Eintraege.Should().HaveCount(3);
            (await BatchHilfe.GuardAsync(_fx.Store, proj, s)).Should().Be(3);
        }
        (await BatchHilfe.PartitionAsync(_fx.Store, proj, 0))!.Version.Should().Be(vorher + 1, "Effekte, Guards und Partition: ein Commit");
    }

    [Fact]
    public async Task Absturz_mitten_im_Batch_bleibt_exactly_once()
    {
        var proj = await FrischeProjektion();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var es = EventStore();
        await es.AppendEventsAsync(a, 0, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() });
        await es.AppendEventsAsync(b, 0, new IEvent[] { new ImagePairInspiziert() });
        var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var vorher = await BatchHilfe.PartitionAsync(_fx.Store, proj, 0);

        // Absturz nach den Effekten aller Streams, vor dem Commit.
        var abstuerzend = BatchHilfe.HistorieLeser(es, store, proj, 0, EinePartition,
            absturz: () => throw new InvalidOperationException("Absturz mitten im Batch"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => abstuerzend.LaufAsync());

        // Nichts wurde durabel: weder Effekte noch Guards noch die Partition (die ganze Session wurde verworfen).
        (await store.GetByPairIdAsync(a)).Should().BeNull();
        (await store.GetByPairIdAsync(b)).Should().BeNull();
        (await BatchHilfe.GuardAsync(_fx.Store, proj, a)).Should().Be(-1);
        (await BatchHilfe.PartitionAsync(_fx.Store, proj, 0))!.Version.Should().Be(vorher!.Version);

        // Neustart: frische Store-Instanz, dieselben Events → Wiederholung, dann Commit.
        var neu = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        await BatchHilfe.HistorieLeser(es, neu, proj, 0, EinePartition).LaufAsync();

        (await neu.GetByPairIdAsync(a))!.Eintraege.Should().HaveCount(2, "exactly-once wirksam trotz Absturz");
        (await neu.GetByPairIdAsync(b))!.Eintraege.Should().HaveCount(1);
        (await BatchHilfe.GuardAsync(_fx.Store, proj, a)).Should().Be(2);
    }

    // ─── Helfer ───

    private async Task<string> FrischeProjektion()
    {
        var proj = "it-historie-" + Guid.NewGuid().ToString("N");
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, EinePartition);
        return proj;
    }

    // Karenz 0: frisch angehängte Events sind sofort „gesetzt“, die Position darf über sie hinaus.
    private MartenEventStore EventStore()
        => new(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/Read.");
    }
}

/// <summary>
/// Baut EINMAL einen Marten-Store gegen das lokale Dev-Postgres (eigenes Schema,
/// damit Produktionsdaten unberührt bleiben). Tests isolieren sich über frische Guids.
/// </summary>
public sealed class PostgresFixture : IDisposable
{
    public IDocumentStore Store { get; }

    public PostgresFixture()
    {
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(
                "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "phase2_cocommit_it";
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            KonsumSchema.Registriere(opts);
        });
    }

    public void Dispose() => Store.Dispose();
}
