using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 (Store-Semantik, echtes Marten, KEIN Cluster) — Befund 1 des Backend-Audits, für den Emittenten-Poll und den Batch-Konsum.
///
/// Der Poll-Backstop ist die letzte Rettung für ein verlorenes Signal vor Stille. Sein Cursor
/// (High-Water-Mark) darf NUR vorrücken, wenn die Verarbeitung des geweckten Streams BESTÄTIGT
/// aufgeholt zurückkommt. Rückt er über einen NICHT verarbeiteten Stream hinaus, weckt auf einem
/// terminalen Stream (keine neuen Events mehr) nie wieder etwas → die Projektion hängt still und
/// dauerhaft zurück. Das war nur gegen echtes Marten sichtbar (globale Sequenz + HWM aus dem
/// realen Event-Log), nie im In-Memory-Store — deshalb Ebene 2.
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class PollerBackstopPostgresTests : IClassFixture<PollerBackstopPostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public PollerBackstopPostgresTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task Unbestaetigte_Weckung_darf_die_HWM_nicht_ueber_den_Stream_hinaus_schieben()
    {
        // Grace = 0: dieser Test isoliert den Poll-Cursor-Guard (Befund 1) von der Straggler-Karenz
        // (Befund 3) — ein frisch angehängtes Event ist sofort HWM-fähig, sodass die (buggy) naive
        // Advance den Stream überhaupt überspringen KÖNNTE. Genau das verhindert der Fix.
        var store = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);

        // Isolation gegen früher in DIESEM Schema angehängte Events: ab der aktuellen globalen
        // HWM aufsetzen. Ebene-2-Tests laufen sequentiell (xunit.runner.json) → kein Interleave.
        var baseHwm = (await store.ReadChangedStreamsAsync(0, null, int.MaxValue, default)).HighWaterMark;

        // Ein terminaler Stream: ein Event, danach STILLE. Sein Signal geht verloren (kein Wake) —
        // der Poll ist die einzige verbleibende Rettung.
        var streamId = Guid.NewGuid();
        await store.AppendEventsAsync(streamId, 0, new IEvent[] { new ImagePairInspiziert() });

        // Stub-Adapter: die Verarbeitung scheitert beim ERSTEN Aufruf (transienter Fehler →
        // nicht caught-up), gelingt danach. Er notiert, welche Streams er wirklich verarbeitet hat.
        var verarbeitet = new List<Guid>();
        var ersterAufruf = true;
        Func<Guid, CancellationToken, Task<bool>> wake = (sid, ct) =>
        {
            if (ersterAufruf)
            {
                ersterAufruf = false;
                return Task.FromResult(false); // Verarbeitung gescheitert → unbestätigt.
            }
            verarbeitet.Add(sid);
            return Task.FromResult(true);
        };

        var poller = new Poller(store, wake, startHighWater: baseHwm);

        // Poll 1: weckt den Stream, aber die Verarbeitung scheitert (unbestätigt).
        await poller.PollOnceAsync();
        verarbeitet.Should().BeEmpty("die erste Verarbeitung ist gescheitert");

        // Poll 2: KEIN neues Event. Der Fix verlangt, dass die HWM in Poll 1 NICHT über den
        // unverarbeiteten Stream hinaus vorgerückt ist → der Stream wird erneut geweckt und jetzt
        // verarbeitet. Bug (Befund 1): die HWM ist bereits vorbei → der Stream wird nie wieder
        // geweckt → 'verarbeitet' bleibt leer und die Projektion hängt für immer zurück.
        await poller.PollOnceAsync();

        verarbeitet.Should().ContainSingle().Which.Should().Be(streamId,
            "ein unbestätigt gebliebener Stream muss beim nächsten Poll erneut geweckt werden");
    }

    /// <summary>
    /// PF-12: der Poll der Emittenten weckt nur Streams mit einem abonnierten Event — früher jeden geänderten Stream des Logs, und der
    /// Adapter legte für jeden einen Cursor an — und liest seitenweise über alle Seiten (nur Sequenz und Stream, keine Daten).
    /// </summary>
    [Fact]
    public async Task Der_Poll_weckt_nur_Streams_mit_abonniertem_Event_und_liest_ueber_alle_Seiten()
    {
        var store = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);
        var baseHwm = (await store.ReadChangedStreamsAsync(0, null, int.MaxValue, default)).HighWaterMark;
        var typen = BatchLeser.TypenAus(new[] { typeof(ImagePairInspiziert) });

        var abonniert = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var fremd = Guid.NewGuid();
        foreach (var s in abonniert.Take(3)) await store.AppendEventsAsync(s, 0, new IEvent[] { new ImagePairInspiziert() });
        await store.AppendEventsAsync(fremd, 0, new IEvent[] { new ImagePairKomplett() });
        foreach (var s in abonniert.Skip(3)) await store.AppendEventsAsync(s, 0, new IEvent[] { new ImagePairInspiziert() });

        // Seiten zu zwei Events: 2 + 2 + 1, die fremde Sequenz dazwischen zählt nicht.
        var seiten = new List<StreamChanges>();
        for (var hwm = baseHwm; ;)
        {
            var seite = await store.ReadChangedStreamsAsync(hwm, typen, 2, default);
            seiten.Add(seite);
            hwm = seite.HighWaterMark;
            if (!seite.Voll) break;
        }
        seiten.Select(x => x.StreamIds.Count).Should().Equal(2, 2, 1);
        seiten.SelectMany(x => x.StreamIds).Should().Equal(abonniert);

        var geweckt = new List<Guid>();
        var poller = new Poller(store, (sid, _) => { geweckt.Add(sid); return Task.FromResult(true); }, baseHwm, typen);
        await poller.PollOnceAsync();
        geweckt.Should().Equal(abonniert, "der fremde Stream hat kein abonniertes Event");
        poller.HighWater.Should().Be((await store.ReadChangedStreamsAsync(baseHwm, null, int.MaxValue, default)).HighWaterMark);
    }

    /// <summary>
    /// Batch-Konsum: ein Event, dessen Signal verloren geht, holt die nächste Weckung der Partition nach (der Poll weckt jede Partition alle
    /// 30 s) — der Leser liest ab seiner durablen Position, ohne Hinweis auf den Stream. Auch ein Event, das während eines Durchlaufs nach dem
    /// Lesen der Seite committet wird, überspringt die Position nicht: die gesetzte Sequenz wird VOR der Seite bestimmt.
    /// </summary>
    [Fact]
    public async Task Batch_Leser_holt_ein_Event_mit_verlorenem_Signal_beim_naechsten_Poll_nach()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);
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
        (await store.GetByPairIdAsync(waehrend)).Should().BeNull("die Seite war schon gelesen");

        await leser.LaufAsync();                                     // der Poll weckt die Partition
        (await store.GetByPairIdAsync(erster))!.Eintraege.Should().HaveCount(1);
        (await store.GetByPairIdAsync(waehrend))!.Eintraege.Should().HaveCount(1, "die Position hat das Event nicht übersprungen");
    }

    /// <summary>Der Store wird im Test nur für Append/ReadChangedStreams genutzt — kein Replay.</summary>
    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadChangedStreams.");
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
                opts.DatabaseSchemaName = "audit_befund1_it";
                opts.Events.DatabaseSchemaName = "audit_befund1_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                Core.KonsumSchema.Registriere(opts);
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
