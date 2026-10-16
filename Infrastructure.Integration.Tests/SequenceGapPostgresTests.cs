using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using JasperFx.Events;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 (Store-Semantik, echtes Marten 8.20) — Befund 3 des Backend-Audits.
///
/// Marten zieht die globale <c>seq_id</c> beim INSERT INNERHALB der Transaktion. Zwei nebenläufige
/// Commits können in umgekehrter Seq-Reihenfolge SICHTBAR werden: T1 zieht die niedrigere Seq, T2 die
/// höhere — committet aber ZUERST. Ein Poll-Scan im Fenster dazwischen sieht die höhere Seq, nicht die
/// niedrigere (noch uncommitted), und würde die HWM naiv auf die höhere setzen → die niedrigere Seq
/// wird nie wieder gescannt. Der Poller IST der lücken-heilende Backstop → genau hier fatal.
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
public class SequenceGapPostgresTests : IClassFixture<SequenceGapPostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public SequenceGapPostgresTests(Fixture fx) => _fx = fx;

    /// <summary>
    /// PF-14: steht hinter der Marke nichts, liefern Partitions-Read und Emittenten-Poll eine leere Seite und halten die Marke (gesetzte
    /// Sequenz und „dahinter?“ in einer Abfrage, die Seite entfällt); steht dahinter nur ein zu junges Event, liefern sie es (Latenz), schieben
    /// die Marke aber nicht vor — die Abkürzung ändert die Karenz-Regel nicht.
    /// </summary>
    [Fact]
    public async Task Hinter_der_Marke_nichts_heisst_leere_Seite_und_ein_junges_Event_kommt_ohne_die_Marke_zu_schieben()
    {
        var ohneKarenz = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);
        var mitKarenz = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.FromMinutes(5));
        await ohneKarenz.AppendEventsAsync(Guid.NewGuid(), 0, new Abstractions.IEvent[] { new ImagePairInspiziert() });
        await BatchHilfe.UhrenversatzAbwartenAsync();
        var kopf = (await ohneKarenz.ReadChangedStreamsAsync(0, null, int.MaxValue, default)).HighWaterMark;

        var leer = await mitKarenz.ReadPartitionAsync(kopf, 0, 1, null, Array.Empty<long>(), 100, default);
        leer.Events.Should().BeEmpty();
        leer.Gesetzt.Should().Be(kopf);
        leer.Voll.Should().BeFalse();
        var leerPoll = await mitKarenz.ReadChangedStreamsAsync(kopf, null, 100, default);
        leerPoll.StreamIds.Should().BeEmpty();
        leerPoll.HighWaterMark.Should().Be(kopf);

        var stream = Guid.NewGuid();
        await ohneKarenz.AppendEventsAsync(stream, 0, new Abstractions.IEvent[] { new ImagePairInspiziert() });
        var jung = await mitKarenz.ReadPartitionAsync(kopf, 0, 1, null, Array.Empty<long>(), 100, default);
        jung.Events.Select(e => e.Event.AggregateId).Should().Contain(stream, "ein junges Event wird geliefert (Latenz)");
        jung.Gesetzt.Should().Be(kopf, "die Karenz hält die Marke vor dem jungen Event");
        var jungPoll = await mitKarenz.ReadChangedStreamsAsync(kopf, null, 100, default);
        jungPoll.StreamIds.Should().Contain(stream);
        jungPoll.HighWaterMark.Should().Be(kopf);
    }

    /// <summary>
    /// Derselbe Nachzügler im Batch-Konsum: der Leser wendet die höhere, früher committete Sequenz sofort an, rückt seine Position aber nicht
    /// über die Karenz hinaus — die niedrigere Sequenz, die erst danach sichtbar wird, liest der nächste Durchlauf. Jedes Event genau einmal,
    /// auch nach einer Neuaktivierung (der Guard fängt die Wiederholung ab).
    /// </summary>
    [Fact]
    public async Task Batch_Leser_ueberholt_einen_Nachzuegler_nicht_und_wendet_jedes_Event_genau_einmal_an()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.FromMinutes(5));
        var konsum = new Konsum(1, 500);
        var proj = "it-nachzuegler-" + Guid.NewGuid().ToString("N");
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, konsum);
        var kopf = await BatchHilfe.KopfAsync(_fx.Store);
        var (streamA, streamB) = (Guid.NewGuid(), Guid.NewGuid());

        await using var connA = new NpgsqlConnection(Fixture.ConnString);
        await using var connB = new NpgsqlConnection(Fixture.ConnString);
        await connA.OpenAsync();
        await connB.OpenAsync();
        var txA = await connA.BeginTransactionAsync();
        var txB = await connB.BeginTransactionAsync();
        await using (var sA = _fx.Store.LightweightSession(SessionOptions.ForTransaction(txA)))
        {
            sA.Events.StartStream(streamA, new ImagePairInspiziert());
            await sA.SaveChangesAsync();
        }
        await using (var sB = _fx.Store.LightweightSession(SessionOptions.ForTransaction(txB)))
        {
            sB.Events.StartStream(streamB, new ImagePairInspiziert());
            await sB.SaveChangesAsync();
        }
        await txB.CommitAsync();                                     // die höhere Sequenz wird zuerst sichtbar

        var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var leser = BatchHilfe.HistorieLeser(es, store, proj, 0, konsum);
        await leser.LaufAsync();
        (await store.GetByPairIdAsync(streamB))!.Eintraege.Should().HaveCount(1, "jung, aber angewandt (Latenz)");
        leser.Position.Should().Be(kopf, "die Position überholt die noch unsichtbare kleinere Sequenz nicht");

        await txA.CommitAsync();                                     // erst jetzt wird die kleinere Sequenz sichtbar
        await leser.LaufAsync();
        await BatchHilfe.HistorieLeser(es, new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store), proj, 0, konsum).LaufAsync();

        (await store.GetByPairIdAsync(streamA))!.Eintraege.Should().HaveCount(1);
        (await store.GetByPairIdAsync(streamB))!.Eintraege.Should().HaveCount(1, "die Neuaktivierung liest es erneut, der Guard überspringt es");
    }

    private async Task<long> SeqOf(Guid streamId)
    {
        await using var s = _fx.Store.QuerySession();
        var evts = await s.Events.QueryAllRawEvents().Where(e => e.StreamId == streamId).ToListAsync();
        return evts.Single().Sequence;
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadChangedStreams.");
    }

    public sealed class Fixture : IDisposable
    {
        public const string ConnString =
            "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";

        public IDocumentStore Store { get; }

        public Fixture()
        {
            Store = DocumentStore.For(opts =>
            {
                opts.Connection(ConnString);
                opts.DatabaseSchemaName = "audit_befund3_it";
                opts.Events.DatabaseSchemaName = "audit_befund3_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                Core.KonsumSchema.Registriere(opts);
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
