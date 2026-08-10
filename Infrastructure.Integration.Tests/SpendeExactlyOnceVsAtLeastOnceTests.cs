using Abstractions;
using Core;
using Domain.Infrastructure;
using Domain.Projections;
using Domain.Spende;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using JasperFx;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Der KANONISCHE exactly-once-vs-at-least-once-Beweis gegen ECHTES Postgres — das Gegenstück zum
/// LoadHarness-Durchsatz (Happy Path). Dieselbe (nicht-idempotente) Summen-Projektion, derselbe Absturz
/// zwischen Effekt und Marke, EINZIGER Unterschied: die Store-Implementierung.
///
/// - Co-Commit (exactly-once): Effekt + Marke in EINER Transaktion → Absturz verwirft beides → Re-Delivery
///   addiert GENAU EINMAL → Summe korrekt (10).
/// - Getrennt (at-least-once): Effekt sofort committet, Marke separat → Absturz lässt den Effekt durabel,
///   die Marke fehlt → Re-Delivery addiert NOCHMAL → Summe zu hoch (20).
///
/// Der Fachcode (SpendenTopfProjection) ist in beiden Fällen byte-identisch — die Garantie entscheidet allein
/// die Store-Naht (Invariante: „Idempotenz ist NIE Default; Co-Commit ist der allgemeine Mechanismus").
/// Braucht laufendes Postgres auf localhost:5432.
/// </summary>
public class SpendeExactlyOnceVsAtLeastOnceTests : IClassFixture<SpendePostgresFixture>
{
    private readonly SpendePostgresFixture _fx;
    public SpendeExactlyOnceVsAtLeastOnceTests(SpendePostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task ExactlyOnce_Absturz_zwischen_Effekt_und_Marke_bleibt_genau_einmal()
    {
        var (streamId, projId) = FreshIds();
        var es = EventStoreMit(streamId, betrag: 10m);
        var store = new SpendenTopfCoCommitStore(_fx.Store);

        var adapter = new ProjectionAdapter(es, store, null, projId, Dispatch(store),
            crashAfterEffectBeforeMark: EinmalAbsturz());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.WakeAsync(streamId));

        // Effekt UND Marke wurden gemeinsam verworfen — nichts durabel.
        (await Summe(streamId)).Should().BeNull("Co-Commit: der Absturz verwirft die ganze Transaktion");

        // Neustart, gleiche Events → Wiederholung, dann sauberer Commit.
        await new ProjectionAdapter(es, store, null, projId, Dispatch(store)).WakeAsync(streamId);

        (await Summe(streamId)).Should().Be(10m, "exactly-once: die Spende ist GENAU EINMAL wirksam");
    }

    [Fact]
    public async Task AtLeastOnce_Absturz_zwischen_Effekt_und_Marke_verdoppelt_die_Summe()
    {
        var (streamId, projId) = FreshIds();
        var es = EventStoreMit(streamId, betrag: 10m);
        var store = new SpendenTopfAtLeastOnceStore(_fx.Store);

        var adapter = new ProjectionAdapter(es, store, null, projId, Dispatch(store),
            crashAfterEffectBeforeMark: EinmalAbsturz());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.WakeAsync(streamId));

        // Der Effekt wurde SOFORT committet (getrennte Transaktion), nur die Marke fehlt.
        (await Summe(streamId)).Should().Be(10m, "at-least-once: der Effekt ist schon durabel, die Marke fehlt");

        // Neustart: keine Marke → dasselbe Event wird NOCHMAL verarbeitet.
        await new ProjectionAdapter(es, store, null, projId, Dispatch(store)).WakeAsync(streamId);

        (await Summe(streamId)).Should().Be(20m,
            "at-least-once: die Re-Delivery addiert die (nicht-idempotente) Spende ein zweites Mal → Summe zu hoch");
    }

    // ─── Helfer ───

    private static (Guid streamId, string projId) FreshIds()
        => (Guid.NewGuid(), "it-spende-" + Guid.NewGuid().ToString("N"));

    /// <summary>Ein einziger Absturz beim ersten Aufruf (nach dem Effekt, vor der Marke), danach normal.</summary>
    private static Func<Task> EinmalAbsturz()
    {
        var armed = true;
        return () =>
        {
            if (!armed) return Task.CompletedTask;
            armed = false;
            throw new InvalidOperationException("Absturz zwischen Effekt und Marke");
        };
    }

    private async Task<decimal?> Summe(Guid kampagne)
    {
        await using var s = _fx.Store.QuerySession();
        return (await s.LoadAsync<SpendenTopfReadModel>(kampagne))?.Summe;
    }

    private MartenEventStore EventStoreMit(Guid streamId, decimal betrag)
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        es.AppendEventsAsync(streamId, 0, new IEvent[] { new SpendeEingegangen(betrag) }).GetAwaiter().GetResult();
        return es;
    }

    private static Func<EventEnvelope, ProjectionWriter, Task> Dispatch(ISpendenTopfWriteStore store)
    {
        var projection = new SpendenTopfProjection(store);
        return (e, writer) => projection.DispatchAsync(e, writer, _ => Task.CompletedTask);
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadStream.");
    }
}

public sealed class SpendePostgresFixture : IDisposable
{
    public IDocumentStore Store { get; }

    public SpendePostgresFixture()
    {
        Store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "spende_eo_it";
            opts.AutoCreateSchemaObjects = AutoCreate.All;
            opts.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);
            opts.Schema.For<SpendenTopfReadModel>().Identity(x => x.Id);
        });
    }

    public void Dispose() => Store.Dispose();
}
