using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 (Store-Semantik, echtes Marten, KEIN Cluster) — Befund 1 des Backend-Audits für den Batch-Konsum: der Poll ist die letzte
/// Rettung für ein verlorenes Signal vor Stille; die Position darf kein Event überspringen, das nach dem Lesen der Seite committet wurde.
/// Das ist nur gegen echtes Marten sichtbar (globale Sequenz, Horizont aus dem realen Event-Log) — deshalb Ebene 2.
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class BatchNachholPostgresTests : IClassFixture<BatchNachholPostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public BatchNachholPostgresTests(Fixture fx) => _fx = fx;

    /// <summary>
    /// Batch-Konsum: ein Event, dessen Signal verloren geht, holt die nächste Weckung der Partition nach (der Poll weckt jede Partition alle
    /// 30 s) — der Leser liest ab seiner durablen Position, ohne Hinweis auf den Stream. Auch ein Event, das während eines Durchlaufs nach dem
    /// Lesen der Seite committet wird, überspringt die Position nicht: die gesetzte Sequenz wird VOR der Seite bestimmt.
    /// </summary>
    [Fact]
    public async Task Batch_Leser_holt_ein_Event_mit_verlorenem_Signal_beim_naechsten_Poll_nach()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        var konsum = new Konsum(1, 500);
        var proj = "it-poll-" + Guid.NewGuid().ToString("N");
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, konsum);
        var (erster, waehrend) = (Guid.NewGuid(), Guid.NewGuid());
        await es.AppendEventsAsync(erster, 0, new IEvent[] { new ImagePairInspiziert() });

        // Während des ersten Durchlaufs (Seite gelesen, Commit ausstehend) kommt ein Event dazu; sein Signal geht verloren.
        var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var einmal = true;
        var leser = BatchHilfe.HistorieLeser(es, store, proj, 0, konsum, absturz: async () =>
        {
            if (!einmal) return;
            einmal = false;
            await es.AppendEventsAsync(waehrend, 0, new IEvent[] { new ImagePairInspiziert() });
        });
        await leser.LaufAsync();
        (await store.GetByPairIdAsync(new(waehrend))).Should().BeNull("die Seite war schon gelesen");

        await leser.LaufAsync();                                     // der Poll weckt die Partition
        (await store.GetByPairIdAsync(new(erster)))!.Eintraege.Should().HaveCount(1);
        (await store.GetByPairIdAsync(new(waehrend)))!.Eintraege.Should().HaveCount(1, "die Position hat das Event nicht übersprungen");
    }

    /// <summary>Der Store wird im Test nur für Append und Lesen genutzt — kein Replay.</summary>
    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append und Lesen.");
    }

    /// <summary>
    /// Eigenes Marten-Schema für diesen Test (Isolation gegen Produktions-/andere Testdaten).
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; }

        public Fixture()
        {
            Store = DocumentStore.For(opts =>
            {
                opts.Connection(
                    "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
                opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;   // wie der Host; der Horizont setzt ihn voraus
                opts.DatabaseSchemaName = "audit_befund1_it";
                opts.Events.DatabaseSchemaName = "audit_befund1_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                Core.KonsumSchema.Registriere(opts);
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
