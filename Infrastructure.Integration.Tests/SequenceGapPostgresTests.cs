using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
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
    /// CONFIRM (fix-unabhängig, prüft das rohe DB-Phänomen): Eine später committete Transaktion mit
    /// NIEDRIGERER Sequenz wird erst sichtbar, NACHDEM eine höhere Sequenz bereits sichtbar war. Der
    /// naive <c>max(seq)</c>-Cursor rückt damit über eine noch unsichtbare, niedrigere Seq hinaus.
    /// </summary>
    [Fact]
    public async Task Reverse_Commit_Reihenfolge_macht_eine_niedrigere_Sequenz_erst_nach_einer_hoeheren_sichtbar()
    {
        // Grace = 0 → das Verhalten ist identisch zur alten, naiven max(seq)-Logik. So bleibt dieser
        // Test ein stabiler Regressions-Wächter für das ROHE DB-Phänomen (fix-unabhängig).
        var reader = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);

        var baseHwm = (await reader.ReadChangedStreamsAsync(0, default)).HighWaterMark;

        var streamA = Guid.NewGuid(); // zieht die NIEDRIGERE Seq (INSERT zuerst)
        var streamB = Guid.NewGuid(); // zieht die HÖHERE Seq (INSERT danach), committet aber ZUERST

        await using var connA = new NpgsqlConnection(Fixture.ConnString);
        await using var connB = new NpgsqlConnection(Fixture.ConnString);
        await connA.OpenAsync();
        await connB.OpenAsync();
        var txA = await connA.BeginTransactionAsync();
        var txB = await connB.BeginTransactionAsync();

        // INSERT A zuerst → niedrigere seq_id, in txA geflusht, NICHT committet (OwnsTransactionLifecycle=false).
        await using (var sA = _fx.Store.LightweightSession(SessionOptions.ForTransaction(txA)))
        {
            sA.Events.StartStream(streamA, new ImagePairInspiziert());
            await sA.SaveChangesAsync();
        }
        // INSERT B danach → höhere seq_id, in txB geflusht, NICHT committet.
        await using (var sB = _fx.Store.LightweightSession(SessionOptions.ForTransaction(txB)))
        {
            sB.Events.StartStream(streamB, new ImagePairInspiziert());
            await sB.SaveChangesAsync();
        }

        // B committet ZUERST → nur B sichtbar.
        await txB.CommitAsync();

        var scanNurB = await reader.ReadChangedStreamsAsync(baseHwm, default);
        scanNurB.StreamIds.Should().Contain(streamB);
        scanNurB.StreamIds.Should().NotContain(streamA, "A ist noch uncommitted");
        var naiveHwm = scanNurB.HighWaterMark; // = B's (höhere) Sequenz

        // Erst JETZT committet A → seine niedrigere Seq wird sichtbar, liegt aber UNTER naiveHwm.
        await txA.CommitAsync();

        var seqA = await SeqOf(streamA);
        var seqB = await SeqOf(streamB);
        seqA.Should().BeLessThan(seqB, "A wurde zuerst eingefügt → niedrigere Sequenz");
        naiveHwm.Should().Be(seqB, "der naive Cursor steht auf B's höherer Sequenz");
        seqA.Should().BeLessThanOrEqualTo(naiveHwm,
            "BESTÄTIGT Befund 3: A's Seq liegt unter dem bereits vorgerückten Cursor → ein Scan ab naiveHwm " +
            "(Sequence > naiveHwm) überspringt A für immer");

        // Der Beweis, dass der naive Read A wirklich verliert:
        var scanAbNaiv = await reader.ReadChangedStreamsAsync(naiveHwm, default);
        scanAbNaiv.StreamIds.Should().NotContain(streamA, "A wird ab der naiven HWM nie wieder gescannt");
    }

    /// <summary>
    /// FIX (Befund 3): Die Straggler-Karenz hält die HWM hinter einem zu jungen Event zurück — der
    /// Stream wird zwar geweckt (Heilung), aber der Cursor rückt NICHT über es hinaus, solange ein
    /// in-flight Nachzügler mit niedrigerer Seq noch landen könnte. Ohne Karenz rückt die HWM sofort
    /// vor (die alte, unsichere Logik).
    /// </summary>
    [Fact]
    public async Task Straggler_Karenz_haelt_die_HWM_hinter_einem_zu_jungen_Event_zurueck()
    {
        var ohneKarenz = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);
        var mitKarenz = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.FromMinutes(5));

        // Referenzpunkt: alles bis hier ist "alt genug" (max seq ohne Karenz).
        var trueMax = (await ohneKarenz.ReadChangedStreamsAsync(0, default)).HighWaterMark;

        var stream = Guid.NewGuid();
        await ohneKarenz.AppendEventsAsync(stream, 0, new Abstractions.IEvent[] { new ImagePairInspiziert() });

        // Mit Karenz: der Stream wird geweckt, aber die HWM bleibt bei trueMax (Event zu jung).
        var mit = await mitKarenz.ReadChangedStreamsAsync(trueMax, default);
        mit.StreamIds.Should().Contain(stream, "der geänderte Stream wird trotz Karenz geweckt (Heilung)");
        mit.HighWaterMark.Should().Be(trueMax,
            "die Straggler-Karenz rückt die HWM NICHT über das zu junge Event hinaus (schützt in-flight Nachzügler)");

        // Ohne Karenz: die HWM rückt sofort über das Event (die alte, für Befund 3 unsichere Logik).
        var ohne = await ohneKarenz.ReadChangedStreamsAsync(trueMax, default);
        ohne.HighWaterMark.Should().BeGreaterThan(trueMax, "ohne Karenz rückt die HWM sofort vor");
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
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
