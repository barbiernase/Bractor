using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Auftrag;
using Domain.Konto;
using Domain.Sammelauftrag;
using Domain.Sammelueberweisung;
using Domain.Ueberweisung;
using FluentAssertions;
using Infrastructure.Prozess;
using Infrastructure.Testing;
using Xunit;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// P5b M1 — der Äquivalenz-Beweis (Backend-Handoff §6): der inkrementelle Cursor-Fold trifft bei JEDER Weckung
/// DIESELBE Feuer-Entscheidung wie der Voll-Fold ab 0 — für linear, Join, Count-Join UND Fan-out. Der Harness
/// treibt den ECHTEN <see cref="ProzessManager"/> gegen die ECHTEN Aggregate (in-memory), einmal mit Cursor AUS
/// (kein Store → Voll-Fold) und einmal mit Cursor AN (<see cref="InMemoryProzessMarkingStore"/>). Alle Guids sind
/// FIX über beide Läufe, damit auch die deterministischen Vorgänge exakt vergleichbar sind.
///
/// Zusätzlich die Sicherheits-Proben aus §6: fehlendes/stale Marking → sauberer Voll-Fold (gleiches Ergebnis);
/// falscher RegelHash → Cache verworfen; und der Read-Zähler-Beleg O(N²)→O(N) für den Fan-out.
/// </summary>
public class ProzessMarkingCursorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed record SagaLauf(
        IReadOnlyList<(string Cmd, Guid Ziel, Guid Vorgang)> Feuerungen,
        bool Beendet, bool Erfolg, long GeleseneEvents, KontoWelt Welt);

    /// <summary>Treibt eine Saga sequenziell (StarteAsync + WakeAsync bis Terminal) mit optionalem Marking-Cursor.</summary>
    private static async Task<SagaLauf> TreibeAsync(
        Guid korrelation, string prozessName, ProzessRegeln regeln,
        Guid triggerStream, IEvent auslöser, Func<KontoWelt, Task> seed,
        IProzessMarkingStore? markingStore)
    {
        var store = new MutableEventStore();
        var welt = new KontoWelt(store);
        await seed(welt);
        await store.AppendEventsAsync(triggerStream, 0, new[] { auslöser },
            correlationId: korrelation.ToString(), causationId: Guid.NewGuid().ToString(), aggregateType: prozessName);

        var registry = new Dictionary<string, ProzessRegeln> { [prozessName] = regeln };
        var mgr = new ProzessManager(store, registry, welt.DispatchAsync, markingStore: markingStore);

        await mgr.StarteAsync(korrelation, prozessName, triggerStream, 1, Ct);
        int guard = 0;
        while (!(await mgr.LadeStatusAsync(korrelation, Ct)).Beendet)
        {
            await mgr.WakeAsync(korrelation, Ct);
            if (++guard > 200_000) throw new InvalidOperationException("Saga erreichte kein Terminal (Livelock?).");
        }
        var st = await mgr.LadeStatusAsync(korrelation, Ct);
        return new SagaLauf(welt.Feuerungen.ToList(), st.Beendet, st.Erfolg, store.GeleseneEvents, welt);
    }

    private static void FeuerungenGleich(SagaLauf aus, SagaLauf an)
    {
        an.Feuerungen.Should().Equal(aus.Feuerungen,
            "der Cursor-Fold muss bei jeder Weckung dieselbe (Command, Ziel, Vorgang)-Entscheidung treffen wie der Voll-Fold");
        an.Beendet.Should().Be(aus.Beendet);
        an.Erfolg.Should().Be(aus.Erfolg);
    }

    // ───────────────────────── Äquivalenz: linear + Join (Überweisung) ─────────────────────────

    [Fact]
    public async Task Ueberweisung_CursorAn_gleich_CursorAus_linear_und_Join()
    {
        var korr = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var trigger = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var quelle = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var ziel = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var regeln = new UeberweisungsProzess().Regeln;

        Func<KontoWelt, Task> seed = async w =>
        {
            await w.EröffneKontoAsync(quelle, 100);
            await w.EröffneKontoAsync(ziel, 0);
        };
        IEvent Auslöser() => new UeberweisungBeauftragt(quelle, ziel, 30);

        var aus = await TreibeAsync(korr, "UeberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: null);
        var an  = await TreibeAsync(korr, "UeberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: new InMemoryProzessMarkingStore());

        FeuerungenGleich(aus, an);
        aus.Erfolg.Should().BeTrue("die Überweisung läuft sauber durch (reservieren → gutschreiben → buchen)");
        // Effekt-Gleichheit (kein Doppeleffekt durch den Cursor).
        an.Welt.Saldo(quelle).Should().Be(aus.Welt.Saldo(quelle));
        an.Welt.Saldo(ziel).Should().Be(aus.Welt.Saldo(ziel));
    }

    // ───────────────────────── Äquivalenz: Fan-out + Count-Join (Sammelüberweisung) ─────────────────────────

    [Fact]
    public async Task Sammelueberweisung_CursorAn_gleich_CursorAus_Fanout_und_CountJoin()
    {
        var korr = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var trigger = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        var quelle = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        var ziele = Enumerable.Range(1, 5)
            .Select(i => Guid.Parse($"bbbbbbbb-0000-0000-0000-00000000000{i}")).ToList();
        var regeln = new SammelueberweisungsProzess().Regeln;

        Func<KontoWelt, Task> seed = async w =>
        {
            await w.EröffneKontoAsync(quelle, 1000);
            foreach (var z in ziele) await w.EröffneKontoAsync(z, 0);
        };
        IEvent Auslöser() => new SammelUeberweisungBeauftragt(quelle, ziele, 20);

        var aus = await TreibeAsync(korr, "SammelueberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: null);
        var an  = await TreibeAsync(korr, "SammelueberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: new InMemoryProzessMarkingStore());

        FeuerungenGleich(aus, an);
        aus.Erfolg.Should().BeTrue();
        aus.Feuerungen.Count(f => f.Cmd == nameof(SchreibeGut)).Should().Be(5, "Fan-out über 5 Ziele");
        aus.Feuerungen.Count(f => f.Cmd == nameof(BucheReservierung)).Should().Be(1, "Count-Join bucht einmal nach allen 5");
        foreach (var z in ziele) an.Welt.Saldo(z).Should().Be(aus.Welt.Saldo(z));
    }

    // ───────────────────────── Äquivalenz: Kompensation (Ziel gesperrt → Rückabwicklung) ─────────────────────────

    [Fact]
    public async Task Ueberweisung_Kompensation_CursorAn_gleich_CursorAus()
    {
        var korr = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var trigger = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
        var quelle = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
        var ziel = Guid.Parse("cccccccc-0000-0000-0000-000000000004");
        var regeln = new UeberweisungsProzess().Regeln;

        Func<KontoWelt, Task> seed = async w =>
        {
            await w.EröffneKontoAsync(quelle, 100);
            await w.EröffneKontoAsync(ziel, 0, gesperrt: true);   // SchreibeGut wird abgelehnt → Kompensation
        };
        IEvent Auslöser() => new UeberweisungBeauftragt(quelle, ziel, 30);

        var aus = await TreibeAsync(korr, "UeberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: null);
        var an  = await TreibeAsync(korr, "UeberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: new InMemoryProzessMarkingStore());

        FeuerungenGleich(aus, an);
        aus.Erfolg.Should().BeFalse("das gesperrte Ziel lehnt die Gutschrift ab → Prozess scheitert und kompensiert");
        aus.Feuerungen.Should().Contain(f => f.Cmd == nameof(GebeReservierungFrei), "die Reservierung wird zurückgenommen");
        // Nach sauberer Kompensation ist die Quelle wieder unbelastet (Reservierung freigegeben, nichts gebucht).
        an.Welt.Saldo(quelle).Should().Be(aus.Welt.Saldo(quelle)).And.Be(100);
    }

    // ───────────────────────── §6-Probe: fehlendes Marking → Voll-Fold-Fallback ─────────────────────────

    /// <summary>Ein Store, der NIE etwas behält (jeder Load = Miss) — erzwingt bei aktivem Cursor den Voll-Fold-Fallback je Weckung.</summary>
    private sealed class VerlierenderStore : IProzessMarkingStore
    {
        public Task<ProzessMarking?> LadeAsync(Guid k, CancellationToken ct) => Task.FromResult<ProzessMarking?>(null);
        public Task SchreibeAsync(ProzessMarking m, CancellationToken ct) => Task.CompletedTask;
        public Task LöscheAsync(Guid k, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Fehlendes_Marking_faellt_sauber_auf_Vollfold_zurueck()
    {
        var korr = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
        var trigger = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
        var quelle = Guid.Parse("dddddddd-0000-0000-0000-000000000003");
        var ziele = Enumerable.Range(1, 4).Select(i => Guid.Parse($"eeeeeeee-0000-0000-0000-00000000000{i}")).ToList();
        var regeln = new SammelueberweisungsProzess().Regeln;

        Func<KontoWelt, Task> seed = async w =>
        {
            await w.EröffneKontoAsync(quelle, 1000);
            foreach (var z in ziele) await w.EröffneKontoAsync(z, 0);
        };
        IEvent Auslöser() => new SammelUeberweisungBeauftragt(quelle, ziele, 20);

        var aus = await TreibeAsync(korr, "SammelueberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: null);
        // Cursor „aktiv", aber der Store verliert alles → jede Weckung faltet voll ab 0. Muss identisch sein.
        var verlustig = await TreibeAsync(korr, "SammelueberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: new VerlierenderStore());

        FeuerungenGleich(aus, verlustig);
        verlustig.Erfolg.Should().BeTrue();
    }

    // ───────────────────────── §6-Probe: falscher RegelHash → Cache verworfen ─────────────────────────

    /// <summary>Ein Store, der ein Marking mit falschem RegelHash + absichtlich kaputtem Inhalt liefert — der Manager MUSS es verwerfen.</summary>
    private sealed class FalscherHashStore : IProzessMarkingStore
    {
        public Task<ProzessMarking?> LadeAsync(Guid k, CancellationToken ct) => Task.FromResult<ProzessMarking?>(new ProzessMarking
        {
            Id = k,
            RegelHash = "VERALTETER-HASH",
            Marking = new MarkingKompakt
            {
                // Absichtlich korrupt: Cursor „weit vorne" + eine erfundene Wirkung. Würde er NICHT verworfen,
                // übersähe der Tail-Read echte Events und der Fold zerfiele. Der Hash-Mismatch rettet.
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
        var korr = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
        var trigger = Guid.Parse("ffffffff-0000-0000-0000-000000000002");
        var quelle = Guid.Parse("ffffffff-0000-0000-0000-000000000003");
        var ziel = Guid.Parse("ffffffff-0000-0000-0000-000000000004");
        var regeln = new UeberweisungsProzess().Regeln;

        Func<KontoWelt, Task> seed = async w =>
        {
            await w.EröffneKontoAsync(quelle, 100);
            await w.EröffneKontoAsync(ziel, 0);
        };
        IEvent Auslöser() => new UeberweisungBeauftragt(quelle, ziel, 30);

        var aus = await TreibeAsync(korr, "UeberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: null);
        var an  = await TreibeAsync(korr, "UeberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: new FalscherHashStore());

        FeuerungenGleich(aus, an);
        an.Erfolg.Should().BeTrue("trotz korruptem Fremd-Marking läuft die Saga korrekt (Hash-Mismatch → Voll-Fold)");
    }

    // ───────────────────────── §6-Probe: Read-Zähler O(N²) → O(N) beim Fan-out ─────────────────────────

    [Fact]
    public async Task Fanout_CursorAn_liest_deutlich_weniger_Events_als_CursorAus()
    {
        var korr = Guid.Parse("12121212-0000-0000-0000-000000000001");
        var trigger = Guid.Parse("12121212-0000-0000-0000-000000000002");
        var quelle = Guid.Parse("12121212-0000-0000-0000-000000000003");
        var ziele = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();
        var regeln = new SammelueberweisungsProzess().Regeln;

        Func<KontoWelt, Task> seed = async w =>
        {
            await w.EröffneKontoAsync(quelle, 100_000);
            foreach (var z in ziele) await w.EröffneKontoAsync(z, 0);
        };
        IEvent Auslöser() => new SammelUeberweisungBeauftragt(quelle, ziele, 10);

        var aus = await TreibeAsync(korr, "SammelueberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: null);
        var an  = await TreibeAsync(korr, "SammelueberweisungsProzess", regeln, trigger, Auslöser(), seed, markingStore: new InMemoryProzessMarkingStore());

        FeuerungenGleich(aus, an);
        // Der Voll-Fold re-scannt bei jeder der ~2N Weckungen alle wachsenden Ziel-Streams → quadratisch;
        // der Cursor liest jedes Ziel-Event genau einmal (Tail). Die Trennung ist bei N=12 bereits deutlich.
        an.GeleseneEvents.Should().BeLessThan(aus.GeleseneEvents / 2,
            $"Cursor las {an.GeleseneEvents}, Voll-Fold {aus.GeleseneEvents} Events");
    }
}
