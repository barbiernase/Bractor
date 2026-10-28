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
/// Schreiber mit veralteter Version verliert kein Update; im Batch-Konsum wenden zwei Aktivierungen
/// derselben Partition einen Effekt nicht doppelt an (G-4), ein Poison-Stream wird geparkt und holt nach der Heilung nach, und ein Neuaufbau
/// nimmt einem noch laufenden Leser den Commit. Eine Quittung ohne Events (Noop, Ablehnung) gilt nur auf dem Kopf ihrer Entscheidung.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class GarantiePostgresTests : IClassFixture<GarantiePostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public GarantiePostgresTests(Fixture fx) => _fx = fx;

    private MartenEventStore EventStore(bool schnell = true)
        => new(schnell ? _fx.Store : _fx.Rich, new NoopFactory(), NullLogger<MartenEventStore>.Instance);

    [Theory, InlineData(false), InlineData(true)]   // Martens Anhänge-Modus: Standard (Rich) und schnell (Quick, so läuft der Host)
    public async Task Ein_Append_wird_ganz_oder_gar_nicht_gueltig(bool schnell)
    {
        var es = EventStore(schnell: schnell);
        var id = Guid.NewGuid();
        await es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() });

        // Zwei Events gegen eine veraltete Version: keines darf gültig werden.
        var akt = () => es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() });
        await akt.Should().ThrowAsync<Exception>();
        (await es.ReadStreamAsync(id, 0, default)).Should().HaveCount(1);

        await es.AppendEventsAsync(id, 1, new IEvent[] { new ImagePairInspiziert(), new ImagePairInspiziert() });
        (await es.ReadStreamAsync(id, 0, default)).Select(e => e.AggregateVersion).Should().Equal(1, 2, 3);
    }

    [Theory, InlineData(false), InlineData(true)]   // Martens Anhänge-Modus: Standard (Rich) und schnell (Quick, so läuft der Host)
    public async Task Effekt_und_Quittung_werden_gemeinsam_gueltig_oder_gar_nicht(bool schnell)
    {
        // Die Inbox (docs/04 §4.6): die Quittung reist in der Transaktion der Events. Scheitert der Append an der Version, steht auch keine
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

    [Theory, InlineData(false), InlineData(true)]   // Martens Anhänge-Modus: Standard (Rich) und schnell (Quick, so läuft der Host)
    public async Task Eine_Quittung_ohne_Events_gilt_nur_auf_dem_Kopf_ihrer_Entscheidung(bool schnell)
    {
        // Ein Noop oder eine Ablehnung schreibt nur die Quittung (Plan Phase 5: keine Marken im Log). Sie gilt nur, wenn der Stream noch auf dem
        //   Stand der Entscheidung steht — sonst hätte ein zweiter Schreiber auf einem veralteten Stand dauerhaft abgelehnt. Auch ohne Stream
        //   (Kopf 0): eine Ablehnung legt keinen an. Derselbe Weg im Group-Commit.
        var es = EventStore(schnell: schnell);
        var store = schnell ? _fx.Store : _fx.Rich;
        var neu = Guid.NewGuid();
        var ablehnung = Guid.NewGuid();
        await es.AppendEventsAsync(neu, 0, Array.Empty<IEvent>(), beilagen: [new Quittung(ablehnung, KommandoAusgang.Abgelehnt)]);
        (await es.AusgangAsync(ablehnung, default)).Should().Be(KommandoAusgang.Abgelehnt);
        (await es.KopfAsync(neu, default)).Should().Be(0, "eine Ablehnung legt keinen Stream an");

        var id = Guid.NewGuid();
        await es.AppendEventsAsync(id, 0, new IEvent[] { new ImagePairInspiziert() });
        var veraltet = Guid.NewGuid();
        var konflikt = () => es.AppendEventsAsync(id, 0, Array.Empty<IEvent>(), beilagen: [new Quittung(veraltet, KommandoAusgang.Abgelehnt)]);
        await konflikt.Should().ThrowAsync<ConcurrencyException>();
        (await es.AusgangAsync(veraltet, default)).Should().BeNull("die Entscheidung fiel auf Version 0, der Stream steht auf 1");

        var noop = Guid.NewGuid();
        await es.AppendEventsAsync(id, 1, Array.Empty<IEvent>(), beilagen: [new Quittung(noop, KommandoAusgang.Gewirkt)]);
        (await es.AusgangAsync(noop, default)).Should().Be(KommandoAusgang.Gewirkt);
        (await es.ReadStreamAsync(id, 0, default)).Should().HaveCount(1, "ein Noop schreibt kein Event");

        // Im Group-Commit: eine veraltete Quittung ohne Events reißt den ganzen Batch mit; die übrigen Teile sind nicht geschrieben.
        var writer = new MartenEventBatchWriter(store, NullLogger<MartenEventBatchWriter>.Instance);
        var anderer = Guid.NewGuid();
        var mit = Guid.NewGuid();
        var batch = () => writer.WriteBatchAsync(
        [
            new BatchAppend(anderer, 0, [new AppendTeil(new IEvent[] { new ImagePairInspiziert() }, null, mit.ToString(), null, null, [new Quittung(mit, KommandoAusgang.Gewirkt)])], null),
            new BatchAppend(id, 0, [new AppendTeil(Array.Empty<IEvent>(), null, null, null, null, [new Quittung(veraltet, KommandoAusgang.Abgelehnt)])], null),
        ], default);
        await batch.Should().ThrowAsync<Exception>();
        (await es.AusgangAsync(mit, default)).Should().BeNull("der Group-Commit ist ganz oder gar nicht gültig");
        (await es.KopfAsync(anderer, default)).Should().Be(0);
        await writer.WriteBatchAsync([new BatchAppend(id, 1, [new AppendTeil(Array.Empty<IEvent>(), null, null, null, null, [new Quittung(veraltet, KommandoAusgang.Abgelehnt)])], null)], default);
        (await es.AusgangAsync(veraltet, default)).Should().Be(KommandoAusgang.Abgelehnt, "auf dem aktuellen Kopf gilt sie");
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Die_Frist_eines_Appends_wird_mit_den_Events_gueltig_oder_gar_nicht(bool schnell)
    {
        // Die Zeile im Fälligkeits-Verzeichnis der Uhr reist im Append ihres FristGesetzt (docs/03 §3.3): scheitert der Append an der Version,
        //   gibt es auch keine Frist — es gibt keine Frist im Verzeichnis ohne ihr Event im Log und umgekehrt.
        var es = EventStore(schnell: schnell);
        var store = schnell ? _fx.Store : _fx.Rich;
        var k = Guid.NewGuid();
        async Task<int> Fristen()
        {
            await using var conn = store.Storage.Database.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand($"select count(*) from {Infrastructure.Uhr.FristVerzeichnis.Name(store)} where stream_id = $1", conn);
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = k });
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        FristFaellig Frist(int version) => new(version, "X", "{}", TimeSpan.FromHours(1), null);

        await es.AppendEventsAsync(k, 0, new IEvent[] { new ImagePairInspiziert() });
        var konflikt = () => es.AppendEventsAsync(k, 0, new IEvent[] { new ImagePairInspiziert() }, beilagen: [Frist(1)]);
        await konflikt.Should().ThrowAsync<ConcurrencyException>();
        (await Fristen()).Should().Be(0, "ohne Events keine Frist");

        await es.AppendEventsAsync(k, 1, new IEvent[] { new ImagePairInspiziert() }, beilagen: [Frist(2)]);
        (await Fristen()).Should().Be(1);
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

        (await store1.GetByPairIdAsync(new(stream)))!.Eintraege.Should().HaveCount(1, "der Effekt des Events gilt genau einmal");
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

        await leser.LaufAsync();
        (await store.GetByPairIdAsync(new(gesund)))!.Eintraege.Should().HaveCount(2, "ein vergifteter Stream hält die anderen nicht auf");
        (await store.GetByPairIdAsync(new(gift))).Should().BeNull();
        var stand = (await BatchHilfe.PartitionAsync(_fx.Store, proj, 0))!;
        stand.Geparkt.Should().ContainSingle(g => g.StreamId == gift, "der Stand ist sichtbar: durabel geparkt");
        stand.Position.Should().Be(await BatchHilfe.KopfAsync(_fx.Store), "nichts hält die Partition fest");

        await es.AppendEventsAsync(gift, 1, new IEvent[] { new ImagePairInspiziert() });
        heilt = true;                                       // der Fehler ist behoben (neuer Code, Daten korrigiert)
        uhr += Konsum.StandardTakt;                        // der nächste Poll versucht geparkte Streams einzeln
        await leser.LaufAsync();

        (await store.GetByPairIdAsync(new(gift)))!.Eintraege.Should().HaveCount(2, "nichts geht verloren: beide Events, je genau einmal");
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
            var es = new MartenEventStore(frisch, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
            var proj = "it-neuaufbau";
            var stream = Guid.NewGuid();
            await es.AppendEventsAsync(stream, 0, new IEvent[] { new ImagePairInspiziert() });
            await new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(frisch).SchreibeMitFortschrittAsync(PartitionsCheckpoint.Neu(proj, 0),
                new Dictionary<Guid, int>(), default);   // die Partition existiert (Version 1, Position 0)

            var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(frisch);
            var zuruecksetzen = true;
            var leser = BatchHilfe.HistorieLeser(es, store, proj, 0, EinePartition, absturz: async () =>
            {
                if (!zuruecksetzen) return;
                zuruecksetzen = false;
                await new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(frisch).FortschrittZuruecksetzenAsync(proj, 1, "g-neu", default);
            });

            await leser.LaufAsync();   // der erste Commit verliert, der Leser lädt den zurückgesetzten Stand und liest neu

            var stand = (await BatchHilfe.PartitionAsync(frisch, proj, 0))!;
            stand.Version.Should().Be(3, "angelegt (1), zurückgesetzt (2), der Commit nach dem Neuladen (3) — der verlorene zählt nicht");
            (await store.GetByPairIdAsync(new(stream)))!.Eintraege.Should().HaveCount(1, "genau einmal — der verlorene Commit hinterließ nichts");
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
        /// <summary>Der Store im schnellen Anhänge-Modus des Hosts (der Horizont der Vollständigkeit setzt ihn voraus).</summary>
        public IDocumentStore Store { get; } = Neu("garantie_it_schnell");
        /// <summary>Derselbe Store im Standard-Modus Rich (eigenes Schema) — nur für die Anhänge-Garantien je Modus.</summary>
        public IDocumentStore Rich { get; } = Neu("garantie_it", schnell: false);

        public static DocumentStore Neu(string schema, bool schnell = true) => DocumentStore.For(opts =>
        {
            if (schnell) opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = schema;
            opts.Events.DatabaseSchemaName = schema;
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            KonsumSchema.Registriere(opts);
            Domain.Infrastructure.Generated.GeneratedProjectionServices.RegistriereSchema(opts);
            Infrastructure.Persistence.IdSerialisierung.Registriere(opts);
            InboxTabelle.Registriere(opts);
            Infrastructure.Uhr.FristVerzeichnis.Registriere(opts);
        });

        public void Dispose() { Store.Dispose(); Rich.Dispose(); }
    }
}
