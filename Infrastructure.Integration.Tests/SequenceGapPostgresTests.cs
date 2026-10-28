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
/// höhere — committet aber ZUERST. Ein Partitions-Read im Fenster dazwischen sieht die höhere Seq, nicht die
/// niedrigere (noch uncommitted), und würde die Position naiv auf die höhere setzen → die niedrigere Seq
/// wird nie wieder gelesen. Der Poll IST der lücken-heilende Backstop → genau hier fatal.
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class SequenceGapPostgresTests : IClassFixture<SequenceGapPostgresTests.Fixture>, IAsyncDisposable
{
    private readonly Fixture _fx;
    public SequenceGapPostgresTests(Fixture fx) => _fx = fx;

    /// <summary>
    /// CONFIRM (fix-unabhängig, prüft das rohe DB-Phänomen): Eine später committete Transaktion mit NIEDRIGERER Sequenz wird erst sichtbar,
    /// NACHDEM eine höhere Sequenz bereits sichtbar war. Ein naiver <c>max(seq)</c>-Cursor rückt damit über eine noch unsichtbare, niedrigere
    /// Seq hinaus.
    /// </summary>
    [Fact]
    public async Task Reverse_Commit_Reihenfolge_macht_eine_niedrigere_Sequenz_erst_nach_einer_hoeheren_sichtbar()
    {
        var (streamA, streamB, txA) = await NachzueglerAsync();
        var naiveHwm = await SichtbarMaxAsync();   // = B's (höhere) Sequenz
        await txA.CommitAsync();

        var seqA = await SeqOf(streamA);
        var seqB = await SeqOf(streamB);
        seqA.Should().BeLessThan(seqB, "A wurde zuerst eingefügt → niedrigere Sequenz");
        naiveHwm.Should().Be(seqB, "der naive Cursor steht auf B's höherer Sequenz");
        seqA.Should().BeLessThan(naiveHwm, "BESTÄTIGT Befund 3: A's Seq liegt unter dem naiven Cursor — ein Scan ab dort überspränge A für immer");
    }

    /// <summary>
    /// G-9: eine Transaktion, die lange offen ist — länger als jede zeitliche Karenz (früher 3 s) —, wird nie überholt. Solange sie läuft,
    /// bleibt die gesetzte Sequenz des Partitions-Reads vor ihrer Sequenz; die jüngere, schon sichtbare wird trotzdem geliefert (Latenz).
    /// Danach liest der nächste Durchlauf sie, und die gesetzte Sequenz rückt über beide.
    /// </summary>
    [Fact]
    public async Task Eine_lange_offene_Transaktion_wird_nicht_ueberholt()
    {
        var es = Neu();
        var basis = await SichtbarMaxAsync();
        var (streamA, streamB, txA) = await NachzueglerAsync();
        await Task.Delay(TimeSpan.FromSeconds(4));                   // länger als die frühere Karenz: eine Uhr hielte jetzt nicht mehr
        var seqB = await SeqOf(streamB);

        var seite = await es.ReadPartitionAsync(basis, 0, 1, null, Array.Empty<long>(), 100, default);
        seite.Events.Select(e => e.Event.AggregateId).Should().Contain(streamB, "die sichtbare, jüngere Sequenz wird geliefert (Latenz)");
        seite.Gesetzt.Should().BeLessThan(seqB, "die gesetzte Sequenz überholt die noch laufende Transaktion mit der kleineren Sequenz nicht");

        await txA.CommitAsync();
        var seqA = await SeqOf(streamA);
        var nachher = await es.ReadPartitionAsync(seite.Gesetzt, 0, 1, null, Array.Empty<long>(), 100, default);
        nachher.Events.Select(e => e.Event.AggregateId).Should().Contain(streamA, "ab der gesetzten Sequenz ist der Nachzügler jetzt zu lesen — nichts ging verloren");
        seqA.Should().BeGreaterThan(seite.Gesetzt);
        nachher.Gesetzt.Should().BeGreaterThanOrEqualTo(seqB, "ist die Transaktion beendet, rückt die gesetzte Sequenz über beide");
    }

    /// <summary>
    /// PF-14: steht hinter der Marke nichts, liefert der Partitions-Read eine leere Seite und hält die Marke (gesetzte
    /// Sequenz und „dahinter?“ in einer Runde, die Seite entfällt); ein neues Event ohne ältere laufende Transaktion kommt und ist gesetzt.
    /// </summary>
    [Fact]
    public async Task Hinter_der_Marke_nichts_heisst_leere_Seite_und_ein_neues_Event_kommt()
    {
        var es = Neu();
        await es.AppendEventsAsync(Guid.NewGuid(), 0, new Abstractions.IEvent[] { new ImagePairInspiziert() });
        var kopf = await SichtbarMaxAsync();

        var leer = await es.ReadPartitionAsync(kopf, 0, 1, null, Array.Empty<long>(), 100, default);
        leer.Events.Should().BeEmpty();
        leer.Gesetzt.Should().Be(kopf);
        leer.Voll.Should().BeFalse();

        var stream = Guid.NewGuid();
        await es.AppendEventsAsync(stream, 0, new Abstractions.IEvent[] { new ImagePairInspiziert() });
        var neu = await es.ReadPartitionAsync(kopf, 0, 1, null, Array.Empty<long>(), 100, default);
        neu.Events.Select(e => e.Event.AggregateId).Should().Contain(stream);
        neu.Gesetzt.Should().BeGreaterThan(kopf, "ohne ältere laufende Transaktion ist das neue Event sofort endgültig");
    }

    /// <summary>
    /// Derselbe Nachzügler im Batch-Konsum: der Leser wendet die höhere, früher committete Sequenz sofort an, rückt seine Position aber nicht
    /// über die noch laufende Transaktion hinaus — die niedrigere Sequenz, die erst danach sichtbar wird, liest der nächste Durchlauf. Jedes Event genau einmal,
    /// auch nach einer Neuaktivierung (der Guard fängt die Wiederholung ab).
    /// </summary>
    [Fact]
    public async Task Batch_Leser_ueberholt_einen_Nachzuegler_nicht_und_wendet_jedes_Event_genau_einmal_an()
    {
        var es = Neu();
        var konsum = new Konsum(1, 500);
        var proj = "it-nachzuegler-" + Guid.NewGuid().ToString("N");
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, konsum);
        var kopf = await BatchHilfe.KopfAsync(_fx.Store);

        var (streamA, streamB, txA) = await NachzueglerAsync();       // die höhere Sequenz ist sichtbar, die kleinere läuft noch

        var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var leser = BatchHilfe.HistorieLeser(es, store, proj, 0, konsum);
        await leser.LaufAsync();
        (await store.GetByPairIdAsync(new(streamB)))!.Eintraege.Should().HaveCount(1, "jung, aber angewandt (Latenz)");
        leser.Position.Should().Be(kopf, "die Position überholt die noch unsichtbare kleinere Sequenz nicht");

        await txA.CommitAsync();                                     // erst jetzt wird die kleinere Sequenz sichtbar
        await leser.LaufAsync();
        await BatchHilfe.HistorieLeser(es, new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store), proj, 0, konsum).LaufAsync();

        (await store.GetByPairIdAsync(new(streamA)))!.Eintraege.Should().HaveCount(1);
        (await store.GetByPairIdAsync(new(streamB)))!.Eintraege.Should().HaveCount(1, "die Neuaktivierung liest es erneut, der Guard überspringt es");
    }

    private MartenEventStore Neu() => new(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);

    /// <summary>
    /// Zwei Transaktionen: A fügt zuerst ein (kleinere Sequenz) und bleibt offen, B fügt danach ein (größere Sequenz) und committet. Die
    /// Verbindung von A lebt bis zum Ende des Tests (der Aufrufer committet; sonst rollt das Aufräumen sie zurück).
    /// </summary>
    private async Task<(Guid A, Guid B, NpgsqlTransaction TxA)> NachzueglerAsync()
    {
        var (streamA, streamB) = (Guid.NewGuid(), Guid.NewGuid());
        var connA = new NpgsqlConnection(Fixture.ConnString);
        _aufraeumen.Add(connA);
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
        await txB.CommitAsync();
        return (streamA, streamB, txA);
    }

    private readonly List<NpgsqlConnection> _aufraeumen = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _aufraeumen) await c.DisposeAsync();
    }

    /// <summary>Die höchste sichtbare Sequenz — der naive Cursor.</summary>
    private async Task<long> SichtbarMaxAsync()
    {
        await using var s = _fx.Store.QuerySession();
        return (await s.QueryAsync<long>("select coalesce(max(seq_id), 0) from audit_befund3_it.mt_events")).Single();
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
            => throw new NotSupportedException("Test nutzt nur Append und Lesen.");
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
                opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;   // wie der Host; der Horizont setzt ihn voraus
                opts.DatabaseSchemaName = "audit_befund3_it";
                opts.Events.DatabaseSchemaName = "audit_befund3_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                Core.KonsumSchema.Registriere(opts);
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
