using Abstractions;
using Core;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Phase 2 (2a) — Co-Commit gegen ECHTES Postgres: das größte Risiko des Projekts.
///
/// Beweist die Store-seitige exactly-once-Wirksamkeit im realen Marten-Store:
/// Effekt (Historien-Append) und Fortschrittsmarke (Checkpoint) werden über EINE
/// Session mit EINEM SaveChanges gemeinsam gültig. Ein Absturz zwischen Effekt und
/// Marke verwirft beides → der nächste Read wiederholt → GENAU EIN Eintrag.
///
/// Braucht laufendes Postgres auf localhost:5432 (cqrs_events / postgres / postgres) —
/// im Dev-Setup über docker-compose.infrastructure.yml.
/// </summary>
public class CoCommitPostgresTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public CoCommitPostgresTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Happy_Path_schreibt_Effekt_und_Marke_gemeinsam()
    {
        var (streamId, projId) = FreshIds();
        var es = EventStoreMit(streamId, count: 1);
        var store = new Domain.Infrastructure.ImagePairHistorieStore(_fx.Store);
        var adapter = new ProjectionAdapter(es, store, projId, Dispatch(store));

        await adapter.WakeAsync(streamId);

        (await store.GetByPairIdAsync(streamId))!.Eintraege.Should().HaveCount(1);
        (await store.LastProcessedVersionAsync(projId, streamId, default)).Should().Be(1);
    }

    [Fact]
    public async Task Mehrere_Events_pro_Batch_akkumulieren_korrekt()
    {
        // Prüft read-your-writes der IdentitySession: drei Appends an denselben Stream
        // in EINEM Batch dürfen sich nicht gegenseitig überschreiben.
        var (streamId, projId) = FreshIds();
        var es = EventStoreMit(streamId, count: 3);
        var store = new Domain.Infrastructure.ImagePairHistorieStore(_fx.Store);
        var adapter = new ProjectionAdapter(es, store, projId, Dispatch(store));

        await adapter.WakeAsync(streamId);

        (await store.GetByPairIdAsync(streamId))!.Eintraege.Should().HaveCount(3);
        (await store.LastProcessedVersionAsync(projId, streamId, default)).Should().Be(3);
    }

    [Fact]
    public async Task Absturz_zwischen_Effekt_und_Marke_bleibt_exactly_once()
    {
        var (streamId, projId) = FreshIds();
        var es = EventStoreMit(streamId, count: 1);
        var store = new Domain.Infrastructure.ImagePairHistorieStore(_fx.Store);

        // Absturz nach dem Effekt, vor der Marke — genau einmal.
        var armed = true;
        Func<Task> crash = () =>
        {
            if (!armed) return Task.CompletedTask;
            armed = false;
            throw new InvalidOperationException("Absturz zwischen Effekt und Marke");
        };

        var adapter = new ProjectionAdapter(es, store, projId, Dispatch(store), crashAfterEffectBeforeMark: crash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.WakeAsync(streamId));

        // Nichts wurde durabel: weder Effekt noch Marke (die ganze Session wurde verworfen).
        (await store.GetByPairIdAsync(streamId)).Should().BeNull();
        (await store.LastProcessedVersionAsync(projId, streamId, default)).Should().Be(-1);

        // Neustart: gleicher Store, gleiche Events → Wiederholung, dann Commit.
        var adapter2 = new ProjectionAdapter(es, store, projId, Dispatch(store));
        await adapter2.WakeAsync(streamId);

        // exactly-once wirksam: GENAU EIN Eintrag trotz Absturz.
        (await store.GetByPairIdAsync(streamId))!.Eintraege.Should().HaveCount(1);
        (await store.LastProcessedVersionAsync(projId, streamId, default)).Should().Be(1);
    }

    // ─── Helfer ───

    private static (Guid streamId, string projId) FreshIds()
        => (Guid.NewGuid(), "it-historie-" + Guid.NewGuid().ToString("N"));

    // Seedet die Quell-Events über ECHTES Marten (kein In-Memory-Store mehr) — der Adapter liest
    // sie danach über dasselbe Postgres. Eigenes Event-Schema je Stream-Guid → keine Kollision.
    private MartenEventStore EventStoreMit(Guid streamId, int count)
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        es.AppendEventsAsync(
            streamId, 0,
            Enumerable.Range(0, count).Select(_ => (IEvent)new ImagePairInspiziert()).ToList())
          .GetAwaiter().GetResult();
        return es;
    }

    private static Func<EventEnvelope, ProjectionWriter, Task> Dispatch(IImagePairHistorieWriteStore effektStore)
    {
        var projection = new ImagePairHistorieProjection(effektStore);
        return (e, writer) => projection.DispatchAsync(e, writer, _ => Task.CompletedTask);
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadStream.");
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
            opts.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);
        });
    }

    public void Dispose() => Store.Dispose();
}
