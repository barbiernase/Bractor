using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.Persistence;
using JasperFx.Events;
using Marten;
using Marten.Linq.MatchesSql;
using Microsoft.Extensions.Logging.Abstractions;
using Pruefdomaene.Posten;
using Anwendung = Infrastructure.Komposition.Anwendung;
using IEvent = Abstractions.IEvent;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Event-Reads des <see cref="MartenEventStore"/> lesen mit festem SQL statt über Martens LINQ (PF-21, docs/03 §3.3). Garantie: sie liefern
/// Feld für Feld dasselbe wie Martens <c>QueryAllRawEvents()</c> mit derselben Bedingung (das Soll, so wie die Reads vorher lasen) — alte Bytes
/// über das Upcasting, Kopfzeilen (Aggregat-Typ, Akteur, Spur), Korrelation, Versionsgrenzen, archivierte Streams und Platzhalter gescheiterter
/// Appends (Tombstones), gegen echtes Marten.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class EventReadGarantieTests : IClassFixture<EventReadGarantieTests.Fixture>
{
    private readonly Fixture _fx;
    public EventReadGarantieTests(Fixture fx) => _fx = fx;

    private MartenEventStore Es => new(_fx.Store, new Pruefdomaene.AggregateHandlerFactory(), NullLogger<MartenEventStore>.Instance);

    /// <summary>Das Soll: Martens LINQ über die rohen Events (wie die Reads vor PF-21), ohne Tombstones, über das Upcasting aufgewertet.</summary>
    private async Task<List<EventEnvelope>> SollAsync(Guid stream, Func<JasperFx.Events.IEvent, bool> bedingung)
    {
        await using var s = _fx.Store.QuerySession();
        var roh = await s.Events.QueryAllRawEvents().Where(e => e.StreamId == stream).OrderBy(e => e.Version).ToListAsync();
        string? H(JasperFx.Events.IEvent e, string k) => e.Headers != null && e.Headers.TryGetValue(k, out var v) ? v?.ToString() : null;
        return roh.Where(bedingung).Where(e => e.Data is not Tombstone)
            .SelectMany(e => Anwendung.Upcasting.Aufwerten(e.Data).Select((p, si) => new EventEnvelope
            {
                EventId = e.Id, AggregateId = e.StreamId, AggregateVersion = (int)e.Version, SubIndex = si, CreatedAtUtc = e.Timestamp,
                CorrelationId = e.CorrelationId ?? "", CausationId = e.CausationId ?? "", AggregateType = H(e, "aggregate_type") ?? "",
                UserId = H(e, ImAuftrag.Header) ?? ImAuftrag.Ohne, Ursache = H(e, Spur.UrsacheHeader), Wirker = H(e, Spur.WirkerHeader),
                Ueber = H(e, Spur.UeberHeader), Stand = H(e, Spur.StandHeader), Payload = p,
            }))
            .ToList();
    }

    private static void Gleich(IReadOnlyList<EventEnvelope> ist, IReadOnlyList<EventEnvelope> soll, string fall)
        => ist.Should().BeEquivalentTo(soll, o => o.WithStrictOrdering().PreferringRuntimeMemberTypes(), fall);

    [Fact]
    public async Task Festes_SQL_liest_Feld_fuer_Feld_dasselbe_wie_Martens_LINQ()
    {
        var es = Es;
        var a = Guid.NewGuid();
        // Version 1: ALTE Bytes (frühere Gestalt, Basis-Diskriminator) — das Upcasting wertet sie beim Lesen auf.
        await using (var s = _fx.Store.LightweightSession())
        {
            s.Events.StartStream(a, new PostenEroeffnet_V1(Bestand: 50));
            await s.SaveChangesAsync();
        }
        // Version 2–3: Korrelation K1, mit Akteur und Spur in den Kopfzeilen; Version 4: K2, ohne Spur; Version 5: eine Noop-Marke.
        using (Spur.Von("u-stream:1", "decide:Posten/ReservierePosten", "regel:AuftragsProzess/0"))
            await es.AppendEventsAsync(a, 1, new IEvent[] { new PostenReserviert(3), new PostenReserviert(4) }, "K1", "C1", "Posten", "Disponent");
        await es.AppendEventsAsync(a, 3, new IEvent[] { new PostenFreigegeben(2) }, "K2", "C2", "Posten");
        await es.AppendEventsAsync(a, 4, new IEvent[] { new KommandoVerarbeitet(Guid.NewGuid()) }, "K2", "C3", "Posten");

        Gleich(await es.ReadStreamAsync(a, 0, default), await SollAsync(a, _ => true), "ganzer Stream ab 0");
        Gleich(await es.ReadStreamAsync(a, 3, default), await SollAsync(a, e => e.Version >= 3), "ab Version 3");
        Gleich(await es.ReadStreamAsync(a, 99, default), await SollAsync(a, e => e.Version >= 99), "hinter dem Kopf");
        Gleich(await es.ReadKorreliertAsync(a, 0, "K1", default), await SollAsync(a, e => e.CorrelationId == "K1"), "korreliert K1");
        Gleich(await es.ReadKorreliertAsync(a, 4, "K2", default), await SollAsync(a, e => e.Version >= 4 && e.CorrelationId == "K2"),
            "korreliert K2 ab 4");
        Gleich(await es.ReadKorreliertAsync(a, 0, "gibt-es-nicht", default), await SollAsync(a, _ => false), "fremde Korrelation");
        foreach (var v in new[] { 1, 2, 5, 99 })
        {
            var ist = await es.ReadEventAsync(a, v, default);
            var soll = (await SollAsync(a, e => e.Version == v)).FirstOrDefault();
            ist.Should().BeEquivalentTo(soll, o => o.PreferringRuntimeMemberTypes(), $"Event an Version {v}");
        }
        (await es.ReadStreamAsync(a, 0, default)).Should().HaveCount(5);
        (await es.ReadStreamAsync(a, 0, default))[0].Payload.Should().BeOfType<PostenEroeffnet>().Which.AnfangsBestand.Should().Be(50);
        (await es.ReadStreamAsync(a, 2, default))[0].Wirker.Should().Be("decide:Posten/ReservierePosten");
    }

    [Fact]
    public async Task Archivierte_Streams_und_Platzhalter_gescheiterter_Appends_liest_keiner()
    {
        var es = Es;
        var archiviert = Guid.NewGuid();
        await es.AppendEventsAsync(archiviert, 0, new IEvent[] { new PostenEroeffnet(5) }, "K", "C", "Posten");
        await using (var s = _fx.Store.LightweightSession())
        {
            s.Events.ArchiveStream(archiviert);
            await s.SaveChangesAsync();
        }
        Gleich(await es.ReadStreamAsync(archiviert, 0, default), await SollAsync(archiviert, e => !e.IsArchived), "archivierter Stream");
        (await es.ReadStreamAsync(archiviert, 0, default)).Should().BeEmpty();

        // Ein Versionskonflikt lässt Marten die reservierten Sequenzen mit Tombstones in einem eigenen Stream füllen (G-7).
        var konflikt = Guid.NewGuid();
        await es.AppendEventsAsync(konflikt, 0, new IEvent[] { new PostenEroeffnet(5) }, "K", "C", "Posten");
        try { await es.AppendEventsAsync(konflikt, 0, new IEvent[] { new PostenReserviert(1), new PostenReserviert(1) }, "K", "C", "Posten"); }
        catch (ConcurrencyException) { }
        Guid tombstoneStream;
        await using (var s = _fx.Store.QuerySession())
            tombstoneStream = (await s.Events.QueryAllRawEvents().Where(e => e.EventTypeName == "tombstone").ToListAsync())
                .Select(e => e.StreamId).FirstOrDefault();
        if (tombstoneStream != Guid.Empty)
        {
            Gleich(await es.ReadStreamAsync(tombstoneStream, 0, default), await SollAsync(tombstoneStream, _ => true), "Tombstone-Stream");
            (await es.ReadStreamAsync(tombstoneStream, 0, default)).Should().BeEmpty();
        }
        Gleich(await es.ReadStreamAsync(konflikt, 0, default), await SollAsync(konflikt, _ => true), "Stream nach dem Konflikt");
    }

    [Fact]
    public async Task Seiten_der_Projektionen_lesen_dasselbe_wie_Martens_LINQ()
    {
        var es = Es;
        // Die Startposition muss auf einem echten Event stehen — sonst sähe der Test eine falsche Grenze (> gegen >=) nicht.
        await es.AppendEventsAsync(Guid.NewGuid(), 0, new IEvent[] { new PostenEroeffnet(1) }, "K", "C", "Posten");
        var anfang = (await es.ReadChangedStreamsAsync(0, null, int.MaxValue, default)).HighWaterMark;
        await using (var q = _fx.Store.QuerySession())
            (await q.Events.QueryAllRawEvents().Where(e => e.Sequence == anfang).ToListAsync()).Should().ContainSingle("an der Startposition steht ein Event");
        var streams = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();
        foreach (var s in streams)
        {
            await es.AppendEventsAsync(s, 0, new IEvent[] { new PostenEroeffnet(10), new PostenReserviert(1) }, "K", "C", "Posten");
            using (Spur.Von("u:1", "decide:Posten/GibPostenFrei", null))
                await es.AppendEventsAsync(s, 2, new IEvent[] { new PostenFreigegeben(1) }, "K", "C2", "Posten", "Disponent");
        }
        long[] erledigt;
        await using (var q = _fx.Store.QuerySession())
            erledigt = (await q.Events.QueryAllRawEvents().Where(e => e.Sequence > anfang).OrderBy(e => e.Sequence).ToListAsync())
                .Select(e => e.Sequence).Where((_, i) => i % 5 == 0).ToArray();

        foreach (var partition in new[] { 0, 1 })
        foreach (var typen in new[] { null, new[] { "posten_reserviert", "posten_freigegeben" } })
        foreach (var ausser in new[] { Array.Empty<long>(), erledigt })
        foreach (var limit in new[] { 3, 500 })
        {
            var ist = await es.ReadPartitionAsync(anfang, partition, 2, typen, ausser, limit, default);
            // Das Soll: dieselbe Abfrage über Martens LINQ (so las ReadPartitionAsync vor PF-22).
            await using var q = _fx.Store.QuerySession();
            var anfrage = q.Events.QueryAllRawEvents().Where(e => e.Sequence > anfang)
                .Where(e => e.MatchesSql($"{PartitionsAusdruck.Sql("d.stream_id")} % ? = ? and not (d.seq_id = any(?))", 2, partition, ausser));
            if (typen is not null) anfrage = anfrage.Where(e => typen.Contains(e.EventTypeName));
            var roh = await anfrage.OrderBy(e => e.Sequence).Take(limit).ToListAsync();
            var fall = $"Partition {partition}, Typen {(typen is null ? "alle" : "zwei")}, {ausser.Length} erledigt, Grenze {limit}";
            ist.Voll.Should().Be(roh.Count >= limit, fall);
            ist.Events.Select(e => e.Sequenz).Should().Equal(roh.Where(e => e.Data is not Tombstone).Select(e => e.Sequence), fall);
            foreach (var e in ist.Events)
            {
                var soll = await SollAsync(e.Event.AggregateId, x => x.Version == e.Event.AggregateVersion);
                e.Event.Should().BeEquivalentTo(soll.Single(), o => o.PreferringRuntimeMemberTypes(), fall);
            }
        }
    }

    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; } = Pruefstand.Store("pruef_event_read");
        public Fixture() => Store.Advanced.Clean.CompletelyRemoveAllAsync().GetAwaiter().GetResult();
        public void Dispose() => Store.Dispose();
    }
}
