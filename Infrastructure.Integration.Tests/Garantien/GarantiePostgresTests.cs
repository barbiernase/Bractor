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
/// Garantien der Store-Semantik unter Störung, gegen ECHTES Postgres (docs/11 §11.2, §11.5, §11.6): ein Append ist atomar, ein zweiter
/// Schreiber mit veralteter Version verliert kein Update, ein Poison-Stream hält die Hochwassermarke des Emittenten-Polls fest, ohne andere
/// Streams zu blockieren, ein gescheiterter Append (Tombstones) legt den Poll nicht still (G-7); im Batch-Konsum wenden zwei Aktivierungen
/// derselben Partition einen Effekt nicht doppelt an (G-4), ein Poison-Stream wird geparkt und holt nach der Heilung nach, und ein Neuaufbau
/// nimmt einem noch laufenden Leser den Commit.
/// </summary>
public class GarantiePostgresTests : IClassFixture<GarantiePostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public GarantiePostgresTests(Fixture fx) => _fx = fx;

    private MartenEventStore EventStore(TimeSpan? karenz = null, bool schnell = false)
        => new(schnell ? _fx.Schnell : _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: karenz ?? TimeSpan.Zero);

    [Theory, InlineData(false), InlineData(true)]   // Martens Anhänge-Modus: Standard (Rich) und schnell (Quick, so läuft der Host)
    public async Task Effekt_und_Quittung_werden_gemeinsam_gueltig_oder_gar_nicht(bool schnell)
    {
        // Die Inbox (docs/04 §4.4): die Quittung reist in der Transaktion der Events. Scheitert der Append an der Version, steht auch keine
        //   Quittung; kennt die Inbox die CommandId schon, steht auch kein Event — und der Aufrufer erfährt es an der Ausnahme.
        var es = EventStore(schnell: schnell);
        var id = Guid.NewGuid();
        var erster = Guid.NewGuid();
        await es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new Quittung(erster, KommandoAusgang.Gewirkt)]);
        (await es.AusgangAsync(erster, default)).Should().Be(KommandoAusgang.Gewirkt);

        var veraltet = Guid.NewGuid();
        var konflikt = () => es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new Quittung(veraltet, KommandoAusgang.Gewirkt)]);
        await konflikt.Should().ThrowAsync<ConcurrencyException>();
        (await es.AusgangAsync(veraltet, default)).Should().BeNull("ohne Events keine Quittung");

        var wiederholung = () => es.AppendEventsAsync(id, 1, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new Quittung(erster, KommandoAusgang.Abgelehnt)]);
        (await wiederholung.Should().ThrowAsync<KommandoSchonEntschiedenException>()).Which.CommandId.Should().Be(erster);
        (await es.ReadStreamAsync(id, 0, default)).Should().HaveCount(1, "ohne neue Quittung keine Events");
        (await es.AusgangAsync(erster, default)).Should().Be(KommandoAusgang.Gewirkt, "der erste Ausgang bleibt");
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Vorgangs_Beilagen_werden_mit_den_Events_gueltig_oder_gar_nicht(bool schnell)
    {
        // Der Offen-Index-Eintrag reist im Append des ProzessGestartet, sein Ende im Append des ProzessBeendet (docs/03 §3.3): scheitert der
        //   Append an der Version, gibt es auch keinen Eintrag bzw. kein Ende.
        var es = EventStore(schnell: schnell);
        var store = schnell ? _fx.Schnell : _fx.Store;
        var k = Guid.NewGuid();
        async Task<bool> Offen() { await using var q = store.QuerySession(); return await q.LoadAsync<ProzessOffen>(k) is not null; }

        await es.AppendEventsAsync(k, 0, new IEvent[] { new ImagePairInspiziert() });
        var konflikt = () => es.AppendEventsAsync(k, 0, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new VorgangOffen(k, "P")]);
        await konflikt.Should().ThrowAsync<ConcurrencyException>();
        (await Offen()).Should().BeFalse("ohne Events kein Eintrag");

        await es.AppendEventsAsync(k, 1, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new VorgangOffen(k, "P")]);
        (await Offen()).Should().BeTrue();

        var endeKonflikt = () => es.AppendEventsAsync(k, 1, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new VorgangBeendet(k)]);
        await endeKonflikt.Should().ThrowAsync<ConcurrencyException>();
        (await Offen()).Should().BeTrue("ohne Events kein Ende");

        await es.AppendEventsAsync(k, 2, new IEvent[] { new ImagePairInspiziert() }, beilagen: [new VorgangBeendet(k)]);
        (await Offen()).Should().BeFalse();
    }

    [Theory, InlineData(false), InlineData(true)]   // Martens Anhänge-Modus: Standard (Rich) und schnell (Quick, so läuft der Host)
    public async Task Zwei_Schreiber_mit_derselben_Version_genau_einer_gewinnt_kein_Update_geht_still_verloren(bool schnell)
    {
        var es = EventStore(schnell: schnell);
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
        var basis = (await es.ReadChangedStreamsAsync(0, null, int.MaxValue, default)).HighWaterMark;
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
        var basis = (await es.ReadChangedStreamsAsync(0, null, int.MaxValue, default)).HighWaterMark;
        var konflikt = Guid.NewGuid();
        await es.AppendEventsAsync(konflikt, 0, new IEvent[] { new ImagePairInspiziert() });
        try { await es.AppendEventsAsync(konflikt, 0, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() }); } catch (ConcurrencyException) { }
        var danach = Guid.NewGuid();
        await es.AppendEventsAsync(danach, 0, new IEvent[] { new ImagePairInspiziert() });

        var poller = new Poller(es, async (s, ct) => { await es.ReadStreamAsync(s, 0, ct); return true; }, startHighWater: basis);
        await BatchHilfe.UhrenversatzAbwartenAsync();   // Karenz 0: das letzte Event soll schon gesetzt sein
        await poller.PollOnceAsync();

        var kopf = (await es.ReadChangedStreamsAsync(basis, null, int.MaxValue, default)).HighWaterMark;
        poller.HighWater.Should().Be(kopf, "ein gescheiterter Append hinterlässt keinen Stream, an dem jeder Poll für immer hängen bleibt");
    }

    private static readonly Konsum EinePartition = new(1, 500);

    [Fact]
    public async Task Zwei_Aktivierungen_derselben_Projektion_wenden_einen_Effekt_nicht_doppelt_an()
    {
        // Zwei Aktivierungen desselben (Projektion, Partition) — im Cluster nach einer Netzstörung möglich. Die erste liest die Seite und puffert
        // ihren Effekt, hält dann an; die zweite läuft ganz durch; danach committet die erste. Der Effekt ist ein Append (nicht idempotent).
        var es = EventStore();
        var proj = "it-zwei-aktivierungen-" + Guid.NewGuid().ToString("N");
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, EinePartition);
        var stream = Guid.NewGuid();
        await es.AppendEventsAsync(stream, 0, new IEvent[] { new ImagePairInspiziert() });

        var halt = new TaskCompletionSource();
        var angehalten = new TaskCompletionSource();
        var store1 = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var store2 = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var erste = BatchHilfe.HistorieLeser(es, store1, proj, 0, EinePartition,
            absturz: async () => { angehalten.TrySetResult(); await halt.Task; });
        var zweite = BatchHilfe.HistorieLeser(es, store2, proj, 0, EinePartition);

        var ersteLaeuft = Task.Run(() => erste.LaufAsync());
        await angehalten.Task;
        await zweite.LaufAsync();
        halt.SetResult();
        await ersteLaeuft;   // ihr Commit verliert an der Version der Partition, sie lädt neu — der Guard überspringt das Event

        (await store1.GetByPairIdAsync(stream))!.Eintraege.Should().HaveCount(1, "der Effekt des Events gilt genau einmal");
    }

    [Fact]
    public async Task Ein_Poison_Stream_wird_im_Batch_geparkt_der_Rest_committet_nach_der_Heilung_holt_er_nach()
    {
        var es = EventStore();
        var proj = "it-poison-" + Guid.NewGuid().ToString("N");
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, EinePartition);
        Guid gift = Guid.NewGuid(), gesund = Guid.NewGuid();
        await es.AppendEventsAsync(gift, 0, new IEvent[] { new ImagePairInspiziert() });
        await es.AppendEventsAsync(gesund, 0, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() });

        var heilt = false;
        var uhr = DateTimeOffset.UnixEpoch;
        var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var leser = BatchHilfe.HistorieLeser(es, store, proj, 0, EinePartition, jetzt: () => uhr,
            vorher: e => e.AggregateId == gift && !heilt ? throw new InvalidOperationException("Dispatch wirft dauerhaft") : Task.CompletedTask);

        await BatchHilfe.UhrenversatzAbwartenAsync();
        await leser.LaufAsync();
        (await store.GetByPairIdAsync(gesund))!.Eintraege.Should().HaveCount(2, "ein vergifteter Stream hält die anderen nicht auf");
        (await store.GetByPairIdAsync(gift)).Should().BeNull();
        var stand = (await BatchHilfe.PartitionAsync(_fx.Store, proj, 0))!;
        stand.Geparkt.Should().ContainSingle(g => g.StreamId == gift, "der Stand ist sichtbar: durabel geparkt");
        stand.Position.Should().Be(await BatchHilfe.KopfAsync(_fx.Store), "nichts hält die Partition fest");

        await es.AppendEventsAsync(gift, 1, new IEvent[] { new ImagePairInspiziert() });
        heilt = true;                                       // der Fehler ist behoben (neuer Code, Daten korrigiert)
        uhr += Konsum.PollIntervall;                        // der nächste Poll versucht geparkte Streams einzeln
        await leser.LaufAsync();

        (await store.GetByPairIdAsync(gift))!.Eintraege.Should().HaveCount(2, "nichts geht verloren: beide Events, je genau einmal");
        (await BatchHilfe.PartitionAsync(_fx.Store, proj, 0))!.Geparkt.Should().BeEmpty();
        (await BatchHilfe.GuardAsync(_fx.Store, proj, gift)).Should().Be(2);
    }

    [Fact]
    public async Task Ein_Neuaufbau_nimmt_einem_laufenden_Leser_den_Commit()
    {
        // Ein Leser puffert seinen Batch; dazwischen setzt ein Neuaufbau die Partition zurück (neue Version, nicht gelöscht). Der Commit des
        // Lesers darf weder seinen alten Stand wieder anlegen noch seine Effekte schreiben. Eigenes, frisches Schema: nach dem Zurücksetzen liest
        // der Leser ab Sequenz 0.
        using var frisch = Fixture.Neu("garantie_neuaufbau_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var es = new MartenEventStore(frisch, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);
            var proj = "it-neuaufbau";
            var stream = Guid.NewGuid();
            await es.AppendEventsAsync(stream, 0, new IEvent[] { new ImagePairInspiziert() });
            await new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(frisch).CommitAsync(PartitionsCheckpoint.Neu(proj, 0),
                new Dictionary<Guid, int>(), default);   // die Partition existiert (Version 1, Position 0)

            var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(frisch);
            var zuruecksetzen = true;
            var leser = BatchHilfe.HistorieLeser(es, store, proj, 0, EinePartition, absturz: async () =>
            {
                if (!zuruecksetzen) return;
                zuruecksetzen = false;
                await new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(frisch).ResetAllAsync(proj, default);
            });

            await leser.LaufAsync();   // der erste Commit verliert, der Leser lädt den zurückgesetzten Stand und liest neu

            var stand = (await BatchHilfe.PartitionAsync(frisch, proj, 0))!;
            stand.Version.Should().Be(3, "angelegt (1), zurückgesetzt (2), der Commit nach dem Neuladen (3) — der verlorene zählt nicht");
            (await store.GetByPairIdAsync(stream))!.Eintraege.Should().HaveCount(1, "genau einmal — der verlorene Commit hinterließ nichts");
        }
        finally
        {
            await frisch.Advanced.Clean.CompletelyRemoveAllAsync();
        }
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException("Test nutzt nur Append/Read.");
    }

    /// <summary>Eigenes Marten-Schema (Isolation gegen andere Tests und die Sim-Daten).</summary>
    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; } = Neu("garantie_it");
        /// <summary>Derselbe Store im schnellen Anhänge-Modus des Hosts (eigenes Schema).</summary>
        public IDocumentStore Schnell { get; } = Neu("garantie_it_schnell", schnell: true);

        public static DocumentStore Neu(string schema, bool schnell = false) => DocumentStore.For(opts =>
        {
            if (schnell) opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = schema;
            opts.Events.DatabaseSchemaName = schema;
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            KonsumSchema.Registriere(opts);
            InboxTabelle.Registriere(opts);
        });

        public void Dispose() { Store.Dispose(); Schnell.Dispose(); }
    }
}
