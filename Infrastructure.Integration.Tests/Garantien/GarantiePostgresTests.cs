using Abstractions;
using Core;
using Domain.ImagePair;
using Domain.Projections;
using Domain.Projections.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// Garantien der Store-Semantik unter Störung, gegen ECHTES Postgres (docs/11 §11.2, §11.5): ein Append ist atomar, ein zweiter Schreiber
/// mit veralteter Version verliert kein Update, ein Poison-Stream hält die Hochwassermarke des Polls fest, ohne andere Streams zu
/// blockieren, ein gescheiterter Append (Tombstones) legt den Poll nicht still (G-7), und zwei Aktivierungen derselben Projektion wenden einen
/// Effekt nicht doppelt an (G-4).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class GarantiePostgresTests : IClassFixture<GarantiePostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public GarantiePostgresTests(Fixture fx) => _fx = fx;

    private MartenEventStore EventStore(TimeSpan? karenz = null)
        => new(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: karenz ?? TimeSpan.Zero);

    [Fact]
    public async Task Ein_Append_wird_ganz_oder_gar_nicht_gueltig()
    {
        var es = EventStore();
        var id = Guid.NewGuid();
        await es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() });

        // Zwei Events (Effekt + Marke) gegen eine veraltete Version: keines darf gültig werden.
        var akt = () => es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() });
        await akt.Should().ThrowAsync<Exception>();
        (await es.ReadStreamAsync(id, 0, default)).Should().HaveCount(1);

        await es.AppendEventsAsync(id, 1, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() });
        (await es.ReadStreamAsync(id, 0, default)).Select(e => e.AggregateVersion).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Zwei_Schreiber_mit_derselben_Version_genau_einer_gewinnt_kein_Update_geht_still_verloren()
    {
        var es = EventStore();
        var id = Guid.NewGuid();
        await es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() });

        var versuche = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            try { await es.AppendEventsAsync(id, 1, new IEvent[] { new ImagePairInspiziert() }); return true; }
            catch (ConcurrencyException) { return false; }
        })));

        versuche.Count(x => x).Should().Be(1, "der zweite Schreiber scheitert laut an der Versionsprüfung");
        (await es.ReadStreamAsync(id, 0, default)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Ein_Poison_Stream_haelt_die_Hochwassermarke_und_blockiert_keine_anderen_Streams()
    {
        var es = EventStore();
        var basis = (await es.ReadChangedStreamsAsync(0, default)).HighWaterMark;
        Guid gift = Guid.NewGuid(), gesund = Guid.NewGuid();
        await es.AppendEventsAsync(gift, 0, new IEvent[] { new ImagePairInspiziert() });
        await es.AppendEventsAsync(gesund, 0, new IEvent[] { new ImagePairInspiziert() });

        var giftHeilt = false;
        var verarbeitet = new List<Guid>();
        var poller = new Poller(es, (s, _) =>
        {
            if (s == gift && !giftHeilt) throw new InvalidOperationException("Dispatch wirft dauerhaft");
            verarbeitet.Add(s);
            return Task.FromResult(true);
        }, startHighWater: basis);

        await poller.PollOnceAsync();
        await poller.PollOnceAsync();
        verarbeitet.Should().Contain(gesund, "ein vergifteter Stream hält die anderen nicht auf");
        poller.HighWater.Should().Be(basis, "nichts geht verloren: die Marke bleibt vor dem unverarbeiteten Stream");

        giftHeilt = true;                     // der Fehler ist behoben (neuer Code, Daten korrigiert)
        await poller.PollOnceAsync();
        verarbeitet.Should().Contain(gift);
        poller.HighWater.Should().BeGreaterThan(basis);
    }

    [Fact]
    public async Task Ein_gescheiterter_Append_blockiert_den_Poll_nicht()
    {
        // Ein Versionskonflikt beim Append lässt Marten die reservierten Sequenzen mit Tombstones füllen (eigener Stream). Der Poll weckt jeden
        // geänderten Stream — auch diesen —, und der Adapter liest ihn wie jeden anderen: das darf die Hochwassermarke nicht festhalten.
        var es = EventStore();
        var basis = (await es.ReadChangedStreamsAsync(0, default)).HighWaterMark;
        var konflikt = Guid.NewGuid();
        await es.AppendEventsAsync(konflikt, 0, new IEvent[] { new ImagePairInspiziert() });
        try { await es.AppendEventsAsync(konflikt, 0, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() }); } catch (ConcurrencyException) { }
        var danach = Guid.NewGuid();
        await es.AppendEventsAsync(danach, 0, new IEvent[] { new ImagePairInspiziert() });

        var poller = new Poller(es, async (s, ct) => { await es.ReadStreamAsync(s, 0, ct); return true; }, startHighWater: basis);
        await poller.PollOnceAsync();

        var kopf = (await es.ReadChangedStreamsAsync(basis, default)).HighWaterMark;
        poller.HighWater.Should().Be(kopf, "ein gescheiterter Append hinterlässt keinen Stream, an dem jeder Poll für immer hängen bleibt");
    }

    [Fact(Skip = "G-4 offen (docs/13)")]
    public async Task Zwei_Aktivierungen_derselben_Projektion_wenden_einen_Effekt_nicht_doppelt_an()
    {
        // Zwei Aktivierungen desselben (Projektion, Stream) — im Cluster nach einer Netzstörung möglich. Die erste liest die Marke und puffert
        // ihren Effekt, hält dann an; die zweite läuft ganz durch; danach committet die erste. Der Effekt ist ein Append (nicht idempotent).
        var es = EventStore();
        var stream = Guid.NewGuid();
        var proj = "it-zwei-aktivierungen-" + Guid.NewGuid().ToString("N");
        await es.AppendEventsAsync(stream, 0, new IEvent[] { new ImagePairInspiziert() });

        var halt = new TaskCompletionSource();
        var angehalten = new TaskCompletionSource();
        var store1 = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var store2 = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var erste = new ProjectionAdapter(es, store1, null, proj, Dispatch(store1),
            crashAfterEffectBeforeMark: async () => { angehalten.TrySetResult(); await halt.Task; });
        var zweite = new ProjectionAdapter(es, store2, null, proj, Dispatch(store2));

        var ersteLaeuft = Task.Run(() => erste.WakeAsync(stream));
        await angehalten.Task;
        await zweite.WakeAsync(stream);
        halt.SetResult();
        try { await ersteLaeuft; } catch { /* ein Konflikt der ersten Aktivierung ist die erwartete Antwort */ }

        (await store1.GetByPairIdAsync(stream))!.Eintraege.Should().HaveCount(1, "der Effekt des Events gilt genau einmal");
    }

    private static Func<EventEnvelope, ProjectionWriter, Infrastructure.PubSub.EmitAusgaenge, Task> Dispatch(IAppendHistorieEintrag effektStore)
    {
        var projection = new ImagePairHistorieProjektion();
        var faehigkeiten = new FaehigkeitenAus(effektStore);
        return (e, writer, _) => projection.DispatchAsync(e, writer, _ => Task.CompletedTask, faehigkeiten);
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException("Test nutzt nur Append/Read.");
    }

    /// <summary>Eigenes Marten-Schema (Isolation gegen andere Tests und die Sim-Daten).</summary>
    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; } = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "garantie_it";
            opts.Events.DatabaseSchemaName = "garantie_it";
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            opts.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);
        });

        public void Dispose() => Store.Dispose();
    }
}
