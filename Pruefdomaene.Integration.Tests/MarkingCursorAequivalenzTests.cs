using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.Persistence;
using Infrastructure.Prozess;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;
using Anwendung = Infrastructure.Komposition.Anwendung;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Der Äquivalenz-Beweis des Marking-Cursors gegen ECHTES Marten (docs/13 T-1, docs/04 §4.5): der inkrementelle Tail-Fold trifft bei
/// JEDER Weckung dieselbe Feuer-Entscheidung wie der Voll-Fold ab 0 — für Diamant (Fork + Join), Kompensation und Fan-out mit
/// Count-Join. Der echte <see cref="ProzessManager"/> wird DIREKT getrieben (kein Cluster → deterministische Reihenfolge), einmal ohne
/// Marking-Store (Voll-Fold) und einmal mit <see cref="MartenProzessMarkingStore"/> (Cursor), je in einem eigenen, geleerten Schema.
/// Alle Guids sind über beide Läufe gleich, damit die deterministischen Vorgänge exakt vergleichbar sind. Dazu die Sicherheits-Probe:
/// ein Marking mit falschem RegelHash wird verworfen (Voll-Fold, gleiches Ergebnis), und der Cursor liest beim Fan-out weniger.
/// </summary>
public class MarkingCursorAequivalenzTests : IClassFixture<MarkingCursorAequivalenzTests.Fixture>
{
    private readonly Fixture _fx;
    public MarkingCursorAequivalenzTests(Fixture fx) => _fx = fx;

    private sealed record Lauf(IReadOnlyList<(string Cmd, Guid Ziel, Guid Vorgang)> Feuerungen, bool Beendet, bool Erfolg, long GeleseneEvents);

    /// <summary>Treibt einen Vorgang sequentiell (Starte + Wake bis Terminal) gegen den echten Marten-Store.</summary>
    private static async Task<Lauf> TreibeAsync(IDocumentStore marten, string prozess, Guid auslöserStream, Func<Welt, Task> seed,
        IProzessMarkingStore? marking)
    {
        var store = new ZählenderStore(new MartenEventStore(marten, new Pruefdomaene.AggregateHandlerFactory(), NullLogger<MartenEventStore>.Instance));
        var welt = new Welt(store);
        var korrelation = ProzessId.Für(prozess, auslöserStream, 1);
        welt.Korrelation = korrelation;
        await seed(welt);

        var registry = new Dictionary<string, ProzessRegeln> { [prozess] = Anwendung.Prozesse[prozess] };
        var mgr = new ProzessManager(store, registry, welt.DispatchAsync, markingStore: marking, markingSchreibIntervall: 1);
        store.Gelesen = 0;
        await mgr.StarteAsync(korrelation, prozess, auslöserStream, 1, default);
        var schutz = 0;
        while (!(await mgr.LadeStatusAsync(korrelation, default)).Beendet)
        {
            await mgr.WakeAsync(korrelation, default);
            if (++schutz > 10_000) throw new InvalidOperationException("Der Vorgang erreichte kein Terminal (Livelock?).");
        }
        var st = await mgr.LadeStatusAsync(korrelation, default);
        return new Lauf(welt.Feuerungen.ToList(), st.Beendet, st.Erfolg, store.Gelesen);
    }

    private async Task<(Lauf Aus, Lauf An)> BeideAsync(string prozess, Guid auslöser, Func<Welt, Task> seed, IProzessMarkingStore? an = null)
    {
        var aus = await TreibeAsync(_fx.Aus, prozess, auslöser, seed, marking: null);
        var mit = await TreibeAsync(_fx.An, prozess, auslöser, seed, an ?? new MartenProzessMarkingStore(_fx.An));
        mit.Feuerungen.Should().Equal(aus.Feuerungen,
            "der Cursor-Fold muss bei jeder Weckung dieselbe (Command, Ziel, Vorgang)-Entscheidung treffen wie der Voll-Fold");
        mit.Beendet.Should().Be(aus.Beendet);
        mit.Erfolg.Should().Be(aus.Erfolg);
        return (aus, mit);
    }

    private static Guid G(string präfix, int n) => Guid.Parse($"{präfix}-0000-0000-0000-{n:D12}");

    [Fact]
    public async Task Diamant_Cursor_an_gleich_Cursor_aus()
    {
        Guid posten = G("a1a1a1a1", 1), kontingent = G("a1a1a1a1", 2), auftrag = G("a1a1a1a1", 3);
        var (aus, _) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 10));
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 5));
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        });
        aus.Erfolg.Should().BeTrue();
        aus.Feuerungen.Select(f => f.Cmd).Should().BeEquivalentTo(nameof(ReservierePosten), nameof(BelegeKontingent), nameof(BestaetigeAuftrag));
    }

    [Fact]
    public async Task Kompensation_Cursor_an_gleich_Cursor_aus()
    {
        Guid posten = G("b2b2b2b2", 1), kontingent = G("b2b2b2b2", 2), auftrag = G("b2b2b2b2", 3);
        var (aus, _) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 10));
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 1));   // reicht nicht → Ablehnung → Kompensation
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        });
        aus.Erfolg.Should().BeFalse();
        aus.Feuerungen.Select(f => f.Cmd).Should().Contain(nameof(GibPostenFrei)).And.NotContain(nameof(BestaetigeAuftrag));
    }

    [Fact]
    public async Task Fan_out_mit_Count_Join_Cursor_an_gleich_Cursor_aus_und_liest_weniger()
    {
        var quelle = G("c3c3c3c3", 1);
        var verteilung = G("c3c3c3c3", 2);
        var ziele = Enumerable.Range(10, 12).Select(n => G("c3c3c3c3", n)).ToList();
        var (aus, an) = await BeideAsync(nameof(VerteilProzess), verteilung, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(quelle, 1000));
            foreach (var z in ziele) await w.AnlegenAsync(new EroeffnePosten(z, 0));
            await w.AnlegenAsync(new StarteVerteilung(verteilung, quelle, ziele, 5));
        });
        aus.Erfolg.Should().BeTrue();
        aus.Feuerungen.Count(f => f.Cmd == nameof(BuchePostenZu)).Should().Be(ziele.Count, "je Ziel genau ein Vorgang");
        aus.Feuerungen.Count(f => f.Cmd == nameof(SchliesseVerteilungAb)).Should().Be(1, "der Count-Join feuert einmal nach allen");
        an.GeleseneEvents.Should().BeLessThan(aus.GeleseneEvents,
            $"der Cursor liest nur den Tail (Cursor {an.GeleseneEvents}, Voll-Fold {aus.GeleseneEvents} Events)");
    }

    /// <summary>Ein Store, der ein Marking mit falschem RegelHash und absichtlich kaputtem Inhalt liefert — der Manager MUSS es verwerfen.</summary>
    private sealed class FalscherHashStore : IProzessMarkingStore
    {
        public Task<ProzessMarking?> LadeAsync(Guid k, CancellationToken ct) => Task.FromResult<ProzessMarking?>(new ProzessMarking
        {
            Id = k,
            RegelHash = "VERALTETER-HASH",
            Marking = new MarkingKompakt
            {
                // Cursor „weit vorne" + eine erfundene Wirkung: würde er NICHT verworfen, übersähe der Tail-Read echte Events.
                StreamCursor = new Dictionary<Guid, int> { [k] = 9999 },
                Vorgänge = new Dictionary<string, VorgangMarke> { ["quatsch"] = new VorgangMarke { Wirkung = true } },
            },
        });
        public Task SchreibeAsync(ProzessMarking m, CancellationToken ct) => Task.CompletedTask;
        public Task LöscheAsync(Guid k, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Falscher_RegelHash_verwirft_den_Cache_und_faltet_voll()
    {
        Guid posten = G("d4d4d4d4", 1), kontingent = G("d4d4d4d4", 2), auftrag = G("d4d4d4d4", 3);
        var (_, an) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 10));
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 5));
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        }, an: new FalscherHashStore());
        an.Erfolg.Should().BeTrue("trotz korruptem Fremd-Marking läuft der Vorgang korrekt (Hash-Mismatch → Voll-Fold)");
    }

    // ── Die Welt: führt Commands wie der Aggregat-Actor aus (Decide → Events + Inbox-Marke, Ablehnung → KommandoAbgelehnt) ──

    private sealed class Welt
    {
        private readonly IEventStoreRepository _store;
        private readonly Pruefdomaene.AggregateHandlerFactory _fabrik = new();
        private readonly Dictionary<Guid, (IState State, IAggregateHandler Handler, HashSet<Guid> Erledigt)> _aggregate = new();
        public List<(string Cmd, Guid Ziel, Guid Vorgang)> Feuerungen { get; } = new();
        public Guid Korrelation { get; set; }

        public Welt(IEventStoreRepository store) => _store = store;

        private static IState Neu(ICommand c) => c switch
        {
            EroeffnePosten => new Posten.Posten(),
            RichteKontingentEin => new Kontingent.Kontingent(),
            StarteAuftrag => new Auftrag.Auftrag(),
            StarteVerteilung => new Verteilung.Verteilung(),
            _ => throw new InvalidOperationException($"{c.GetType().Name} legt kein Aggregat an"),
        };

        /// <summary>Legt ein Aggregat an (Seed; der Auslöser trägt die Korrelation des Vorgangs wie im Cluster).</summary>
        public async Task AnlegenAsync(ICommand anlegen)
        {
            var s = Neu(anlegen);
            s.Id = anlegen.AggregateId;
            var h = _fabrik.CreateHandler(s);
            _aggregate[s.Id] = (s, h, new HashSet<Guid>());
            var evs = h.HandleCommand(anlegen).ToList();
            await _store.AppendEventsAsync(s.Id, 0, evs, Korrelation.ToString(), Guid.NewGuid().ToString(), Typ(anlegen));
            foreach (var e in evs) { h.ApplyEvent(e); s.Version++; }
        }

        private static string Typ(ICommand c) => Anwendung.Routing.CommandToAggregate[c.GetType()];

        public async Task DispatchAsync(Guid korr, ICommand cmd, Guid vorgang, CancellationToken ct)
        {
            var (s, h, erledigt) = _aggregate[cmd.AggregateId];
            if (!erledigt.Add(vorgang)) return;   // Inbox-Dedup des Actors
            Feuerungen.Add((cmd.GetType().Name, cmd.AggregateId, vorgang));

            var alle = h.HandleCommand(cmd).ToList();
            var persistent = alle.Where(e => e is not ITransientEvent).ToList();
            var ablehnungen = alle.Where(e => e is ITransientEvent).ToList();
            IReadOnlyList<IEvent> schreibe = ablehnungen.Count > 0 && persistent.Count == 0
                ? new IEvent[] { new KommandoAbgelehnt(vorgang, ablehnungen[0].GetType().Name) }
                : persistent.Append(new KommandoVerarbeitet(vorgang)).ToList();
            await _store.AppendEventsAsync(cmd.AggregateId, s.Version, schreibe, korr.ToString(), vorgang.ToString(), Typ(cmd));
            foreach (var e in schreibe) { if (e is not IProzessIntern) h.ApplyEvent(e); s.Version++; }
        }
    }

    /// <summary>Zählt die gelesenen Events (Beleg, dass der Cursor nur den Tail liest).</summary>
    private sealed class ZählenderStore : IEventStoreRepository
    {
        private readonly IEventStoreRepository _innen;
        public long Gelesen;
        public ZählenderStore(IEventStoreRepository innen) => _innen = innen;

        public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null,
            string? causationId = null, string? aggregateType = null, string? akteur = null)
            => _innen.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType, akteur);
        public Task<StreamChanges> ReadChangedStreamsAsync(long afterGlobalSequence, CancellationToken ct) => _innen.ReadChangedStreamsAsync(afterGlobalSequence, ct);
        public Task<TState?> LoadStateAsync<TState>(Guid aggregateId) where TState : class, IState, new() => _innen.LoadStateAsync<TState>(aggregateId);
        public async Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
        {
            var r = await _innen.ReadStreamAsync(streamId, fromVersion, ct);
            Interlocked.Add(ref Gelesen, r.Count);
            return r;
        }
    }

    /// <summary>Zwei eigene Schemata (Cursor aus / an), beim Start geleert — die festen Guids sind so über Läufe wiederholbar.</summary>
    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Aus { get; } = Pruefstand.Store("pruef_marking_aus");
        public IDocumentStore An { get; } = Pruefstand.Store("pruef_marking_an");

        public Fixture()
        {
            Aus.Advanced.Clean.CompletelyRemoveAllAsync().GetAwaiter().GetResult();
            An.Advanced.Clean.CompletelyRemoveAllAsync().GetAwaiter().GetResult();
        }

        public void Dispose() { Aus.Dispose(); An.Dispose(); }
    }
}
