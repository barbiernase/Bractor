using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 (Store-Semantik, echtes Marten) — Metadaten-Readback (Phase 1, 1a-ii).
///
/// Portiert vom früheren In-Memory-Test: CorrelationId/CausationId/AggregateType werden beim
/// Append gestempelt und kommen pro Event über <c>ReadStreamAsync</c> wieder heraus. Das ist echte
/// Store-Semantik — Marten führt CorrelationId/CausationId als Event-Metadaten und den Aggregat-Typ
/// als Header (MetadataConfig muss aktiv sein); ein In-Memory-Store könnte das Verhalten nur
/// nachbauen, nicht beweisen. Deckt auch den Null-Fall ab (kein Header → <c>string.Empty</c>).
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
public class MetadataPostgresTests : IClassFixture<MetadataPostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public MetadataPostgresTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task ReadStream_liefert_die_beim_Append_gestempelten_Metadaten_pro_Event()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        var id = Guid.NewGuid();

        await es.AppendEventsAsync(
            id, expectedVersion: 0,
            new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() },
            correlationId: "korr-1",
            causationId: "cmd-1",
            aggregateType: "ImagePair");

        var events = await es.ReadStreamAsync(id, fromVersion: 0, default);

        events.Should().HaveCount(2);
        events.Should().OnlyContain(e =>
            e.CorrelationId == "korr-1" &&
            e.CausationId == "cmd-1" &&
            e.AggregateType == "ImagePair");
        events.Select(e => e.AggregateVersion).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Ohne_Metadaten_bleiben_die_Felder_leer_aber_gueltig()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        var id = Guid.NewGuid();

        // Append ohne die optionalen Metadaten → Null-Header → string.Empty (nicht null).
        await es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() });

        var events = await es.ReadStreamAsync(id, 0, default);

        events.Should().ContainSingle();
        events[0].CorrelationId.Should().BeEmpty();
        events[0].CausationId.Should().BeEmpty();
        events[0].AggregateType.Should().BeEmpty();
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadStream.");
    }

    /// <summary>Eigenes Schema; MetadataConfig aktiv wie in der Prod-Config (CqrsServiceExtension).</summary>
    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; }

        public Fixture()
        {
            Store = DocumentStore.For(opts =>
            {
                opts.Connection(
                    "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
                opts.DatabaseSchemaName = "audit_metadata_it";
                opts.Events.DatabaseSchemaName = "audit_metadata_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;

                // Wie Prod: Metadaten müssen aktiv sein, sonst persistiert Marten sie nicht.
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
