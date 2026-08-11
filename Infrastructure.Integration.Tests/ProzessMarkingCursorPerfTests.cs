using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Konto;
using Domain.Sammelauftrag;
using Domain.Sammelueberweisung;
using Infrastructure;                 // AggregateHandlerFactory
using Infrastructure.Aggregate;       // KommandoVerarbeitet / KommandoAbgelehnt
using Infrastructure.Persistence;     // MartenEventStore, MartenEventTypeRegistration, MartenProzessMarkingStore, ProzessMarkingDoc
using Infrastructure.Prozess;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// P5b — der ehrliche Performance-Beweis gegen ECHTES Postgres: „hat sich der Cursor gelohnt?" Der
/// <see cref="ProzessManager"/> wird DIREKT getrieben (kein Cluster/Consul → kein Cold-Boot-Flake), gegen
/// einen echten <see cref="MartenEventStore"/>. Gemessen werden je Fan-out-Breite N die DB-Reads UND die
/// Wall-Clock — einmal mit Voll-Fold (Cursor AUS) und einmal mit Tail-Cursor (AN). Anders als in-memory kostet
/// hier jeder Read einen Postgres-Roundtrip → der O(N²)→O(N)-Read-Unterschied schlägt voll auf die ZEIT durch.
/// </summary>
public class ProzessMarkingCursorPerfTests : IClassFixture<MarkingPerfFixture>
{
    private readonly MarkingPerfFixture _fx;
    private readonly ITestOutputHelper _out;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public ProzessMarkingCursorPerfTests(MarkingPerfFixture fx, ITestOutputHelper o) { _fx = fx; _out = o; }

    private sealed record Messung(long Reads, long Aufrufe, long Wakes, double Ms);

    private async Task<Messung> LaufAsync(int n, bool cursorAn, int historie = 0)
    {
        var korr = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var quelle = Guid.NewGuid();
        var ziele = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

        var zähler = new ZählenderStore(new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance));
        var welt = new KontoDispatch(zähler);
        await welt.EröffneKontoAsync(quelle, 1_000_000_000m);
        foreach (var z in ziele) await welt.EröffneKontoAsync(z, 0);
        // ★ „Aggregate mit Historie": jedes Ziel-Konto trägt bereits H Alt-Events (realistisch — ein benutztes
        //   Konto hat hunderte). Der Voll-Fold re-liest sie bei JEDER Weckung komplett; der Cursor genau einmal.
        if (historie > 0)
        {
            await welt.SeedeHistorieAsync(quelle, historie);
            foreach (var z in ziele) await welt.SeedeHistorieAsync(z, historie);
        }
        await zähler.AppendEventsAsync(trigger, 0, new IEvent[] { new SammelUeberweisungBeauftragt(quelle, ziele, 1) },
            correlationId: korr.ToString(), causationId: Guid.NewGuid().ToString(), aggregateType: "SammelueberweisungsProzess");

        var registry = new Dictionary<string, ProzessRegeln> { ["SammelueberweisungsProzess"] = new SammelueberweisungsProzess().Regeln };
        IProzessMarkingStore? marking = cursorAn ? new MartenProzessMarkingStore(_fx.Store) : null;
        var mgr = new ProzessManager(zähler, registry, welt.DispatchAsync, markingStore: marking);

        zähler.Reset();
        var sw = Stopwatch.StartNew();
        long wakes = 0;
        await mgr.StarteAsync(korr, "SammelueberweisungsProzess", trigger, 1, Ct);
        while (!(await mgr.LadeStatusAsync(korr, Ct)).Beendet)
        {
            await mgr.WakeAsync(korr, Ct);
            if (++wakes > 5_000_000) throw new InvalidOperationException("kein Terminal");
        }
        sw.Stop();
        return new Messung(zähler.Reads, zähler.Aufrufe, wakes, sw.Elapsed.TotalMilliseconds);
    }

    [Fact]
    public async Task Benchmark_Postgres_Vollfold_vs_Cursor()
    {
        var größen = new[] { 15, 30, 60 };
        await LaufAsync(5, cursorAn: false);   // Warmup (Schema/JIT/Connection-Pool)

        var sb = new StringBuilder();
        sb.AppendLine("P5b Postgres-Benchmark — Sammelüberweisung (Fan-out N), echter MartenEventStore, Manager direkt");
        sb.AppendLine();
        sb.AppendLine("   N | Voll Events | Cursor Events | Ev-Faktor | Voll Calls | Cursor Calls | Call-Faktor |   Voll ms | Cursor ms | Zeit");
        sb.AppendLine("-----+-------------+---------------+-----------+------------+--------------+-------------+-----------+-----------+------");

        foreach (var n in größen)
        {
            var aus = await LaufAsync(n, cursorAn: false);
            var an = await LaufAsync(n, cursorAn: true);
            Assert.Equal(aus.Wakes, an.Wakes);   // gleiche Feuer-/Terminal-Arbeit → nur Reads/Zeit unterscheiden sich

            var evFaktor = (double)aus.Reads / Math.Max(1, an.Reads);
            var callFaktor = (double)aus.Aufrufe / Math.Max(1, an.Aufrufe);
            var zeitFaktor = aus.Ms / Math.Max(0.001, an.Ms);
            sb.AppendLine($"{n,4} | {aus.Reads,11:n0} | {an.Reads,13:n0} | {evFaktor,7:0.0}× | {aus.Aufrufe,10:n0} | {an.Aufrufe,12:n0} | {callFaktor,9:0.0}× | {aus.Ms,9:0.0} | {an.Ms,9:0.0} | {zeitFaktor,4:0.0}×");
            Assert.True(an.Reads < aus.Reads, $"N={n}: Cursor {an.Reads} < Voll {aus.Reads} erwartet");
        }

        var text = sb.ToString();
        _out.WriteLine(text);
        var pfad = Environment.GetEnvironmentVariable("P5B_BENCH_OUT");
        if (!string.IsNullOrEmpty(pfad)) File.WriteAllText(pfad, text);
    }

    /// <summary>
    /// Der Fall, für den der Cursor gebaut ist: Ziel-Aggregate mit HISTORIE (H Alt-Events je Konto). Der
    /// Voll-Fold re-liest die ganze Historie bei JEDER Weckung (datengebunden → O(N·H) pro Weckung); der Cursor
    /// liest sie genau einmal. Hier schlägt die Event-Ersparnis voll auf die WALL-CLOCK durch.
    /// </summary>
    [Fact]
    public async Task Benchmark_Postgres_MitHistorie_Vollfold_vs_Cursor()
    {
        const int N = 20;
        const int H = 300;   // jedes Ziel-Konto trägt 300 Alt-Events
        await LaufAsync(3, cursorAn: false, historie: 20);   // Warmup

        var aus = await LaufAsync(N, cursorAn: false, historie: H);
        var an = await LaufAsync(N, cursorAn: true, historie: H);
        Assert.Equal(aus.Wakes, an.Wakes);

        var sb = new StringBuilder();
        sb.AppendLine($"P5b Postgres-Benchmark MIT HISTORIE — Fan-out N={N}, je Ziel-Konto {H} Alt-Events");
        sb.AppendLine();
        sb.AppendLine("        | gelesene Events | Read-Aufrufe |    Wall-Clock");
        sb.AppendLine("--------+-----------------+--------------+--------------");
        sb.AppendLine($" Voll   | {aus.Reads,15:n0} | {aus.Aufrufe,12:n0} | {aus.Ms,10:0.0} ms");
        sb.AppendLine($" Cursor | {an.Reads,15:n0} | {an.Aufrufe,12:n0} | {an.Ms,10:0.0} ms");
        sb.AppendLine();
        sb.AppendLine($" Faktor: {(double)aus.Reads / Math.Max(1, an.Reads):0.0}× weniger Events, " +
                      $"{aus.Ms / Math.Max(0.001, an.Ms):0.0}× schneller (Wall-Clock)");

        var text = sb.ToString();
        _out.WriteLine(text);
        var pfad = Environment.GetEnvironmentVariable("P5B_BENCH_OUT2");
        if (!string.IsNullOrEmpty(pfad)) File.WriteAllText(pfad, text);

        // Bei Historie MUSS der Cursor auch die Zeit deutlich schlagen (datengebunden, nicht roundtrip-gebunden).
        Assert.True(an.Ms < aus.Ms, $"Cursor {an.Ms:0}ms < Voll {aus.Ms:0}ms erwartet (mit Historie)");
    }

    // ── ein IEventStoreRepository-Dekorator, der die gelesenen Ziel-Events zählt ──
    private sealed class ZählenderStore : IEventStoreRepository
    {
        private readonly IEventStoreRepository _inner;
        public long Reads { get; private set; }      // gelesene Events (Datenvolumen)
        public long Aufrufe { get; private set; }     // ReadStreamAsync-Aufrufe (DB-Roundtrips)
        public ZählenderStore(IEventStoreRepository inner) => _inner = inner;
        public void Reset() { Reads = 0; Aufrufe = 0; }

        public async Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid s, int from, CancellationToken ct)
        {
            var r = await _inner.ReadStreamAsync(s, from, ct);
            Reads += r.Count;
            Aufrufe++;
            return r;
        }
        public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null, string? causationId = null, string? aggregateType = null)
            => _inner.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType);
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct) => _inner.ReadChangedStreamsAsync(after, ct);
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => _inner.LoadStateAsync<TState>(id);
    }

    // ── der Konto-Dispatch (emittierter Pfad des AggregateActorBase), gegen den echten Store ──
    private sealed class KontoDispatch
    {
        private readonly IEventStoreRepository _store;
        private readonly AggregateHandlerFactory _factory = new();
        private readonly Dictionary<Guid, (IState State, IAggregateHandler Handler, HashSet<Guid> Verarb, HashSet<Guid> Abg)> _läufe = new();

        public KontoDispatch(IEventStoreRepository store) => _store = store;

        private (IState, IAggregateHandler, HashSet<Guid>, HashSet<Guid>) Hole(Guid id)
        {
            if (!_läufe.TryGetValue(id, out var l))
            {
                IState s = new Konto { Id = id };
                l = (s, _factory.CreateHandler(s), new HashSet<Guid>(), new HashSet<Guid>());
                _läufe[id] = l;
            }
            return l;
        }

        public async Task EröffneKontoAsync(Guid id, decimal saldo)
        {
            var (s, h, _, _) = Hole(id);
            var evs = h.HandleCommand(new EroeffneKonto(id, saldo)).ToList();
            await _store.AppendEventsAsync(id, 0, evs, id.ToString(), Guid.NewGuid().ToString(), "Konto");
            foreach (var e in evs) { h.ApplyEvent(e); s.Version++; }
        }

        /// <summary>Fügt H benigne Alt-Events (Betrag 0, Fremd-Kausalität) an — Historie, die der Fold liest, aber ignoriert.</summary>
        public async Task SeedeHistorieAsync(Guid id, int anzahl)
        {
            var (s, _, _, _) = Hole(id);
            var filler = Enumerable.Range(0, anzahl).Select(_ => (IEvent)new ReservierungFreigegeben(0)).ToList();
            await _store.AppendEventsAsync(id, s.Version, filler, id.ToString(), Guid.NewGuid().ToString(), "Konto");
            s.Version += anzahl;   // Version bündig halten (OCC des nächsten echten Appends); Betrag 0 → Saldo/Reserviert unberührt
        }

        public async Task DispatchAsync(Guid korr, ICommand cmd, Guid vorgang, CancellationToken ct)
        {
            var (s, h, verarb, abg) = Hole(cmd.AggregateId);
            if (verarb.Contains(vorgang) || abg.Contains(vorgang)) return;

            var alle = h.HandleCommand(cmd).ToList();
            var erwartet = s.Version;

            if (alle.Count == 0)
            {
                await _store.AppendEventsAsync(cmd.AggregateId, erwartet, new IEvent[] { new KommandoVerarbeitet(vorgang) }, korr.ToString(), vorgang.ToString(), "Konto");
                s.Version++; verarb.Add(vorgang); return;
            }
            var persistent = alle.Where(e => e is not ITransientEvent).ToList();
            var ablehnungen = alle.Where(e => e is ITransientEvent).ToList();
            if (ablehnungen.Count > 0 && persistent.Count == 0)
            {
                await _store.AppendEventsAsync(cmd.AggregateId, erwartet, new IEvent[] { new KommandoAbgelehnt(vorgang, ablehnungen[0].GetType().Name) }, korr.ToString(), vorgang.ToString(), "Konto");
                s.Version++; abg.Add(vorgang); return;
            }
            var zuSchreiben = persistent.Append((IEvent)new KommandoVerarbeitet(vorgang)).ToList();
            await _store.AppendEventsAsync(cmd.AggregateId, erwartet, zuSchreiben, korr.ToString(), vorgang.ToString(), "Konto");
            foreach (var e in persistent) { if (e is not IProzessIntern) h.ApplyEvent(e); s.Version++; }
            s.Version++; verarb.Add(vorgang);
        }
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException();
    }
}

/// <summary>Ein Marten-Store gegen das Dev-Postgres (eigenes Schema), mit Event-Registrierung + Metadaten + Marking-Doc-Schema.</summary>
public sealed class MarkingPerfFixture : IDisposable
{
    public IDocumentStore Store { get; }

    public MarkingPerfFixture()
    {
        Store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "p5b_bench_it";
            opts.Events.DatabaseSchemaName = "p5b_bench_it";
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;

            MartenEventTypeRegistration.RegisterEventTypes(opts);
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;

            opts.Schema.For<ProzessMarkingDoc>().Identity(x => x.Id);
        });
    }

    public void Dispose() => Store.Dispose();
}
