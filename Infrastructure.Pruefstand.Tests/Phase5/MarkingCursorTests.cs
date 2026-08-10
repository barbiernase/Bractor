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
/// P5(b) — der Prozess-Marking-Cursor. Der KERN-Beweis ist die ÄQUIVALENZ (Handoff §6): dieselbe Saga
/// zweimal durch den echten <see cref="ProzessManager"/> — Cursor AUS (Voll-Fold ab 0) vs. Cursor AN
/// (Cache + Tail) — muss dieselbe Feuer-Sequenz UND denselben Terminal-Ausgang liefern, für linear/Join
/// (Überweisung), Fan-out/Count-Join (Sammelüberweisung) und Kompensation (gesperrtes Ziel). Dazu die
/// §3-Sicherheitsprobe (stale → verpufft, nie Doppeleffekt), die Fallback-Probe (stale RegelHash → Voll-Fold)
/// und die Skalierungs-Probe (Fan-out: der Read-Zähler zeigt O(N) statt O(N²)).
/// </summary>
public class MarkingCursorTests
{
    private static IReadOnlyDictionary<string, ProzessRegeln> Registry(params IProzessDefinition[] defs)
    {
        var d = new Dictionary<string, ProzessRegeln>();
        foreach (var def in defs) d[def.GetType().Name] = def.Regeln;
        return d;
    }

    private static readonly CancellationToken Ct = CancellationToken.None;

    // ── M0.5: der Fan-out-Prozess läuft grün auf dem heutigen Voll-Fold (der Nutznießer + Benchmark-Ziel) ──

    [Fact]
    public async Task M0_5_Sammelueberweisung_laeuft_auf_dem_VollFold_gruen()
    {
        var reg = Registry(new SammelueberweisungsProzess());
        var h = new ProzessSagaHarness(reg);   // kein Marking-Store → Voll-Fold

        var quelle = Guid.NewGuid();
        var ziele = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var auftrag = Guid.NewGuid();

        await h.EröffneKontoAsync(quelle, 100);
        foreach (var z in ziele) await h.EröffneKontoAsync(z, 0);
        await h.AuslöserAsync(auftrag, new SammelUeberweisungBeauftragt(quelle, ziele, 10), "Sammelueberweisungsauftrag");

        var korr = ProzessId.Für(nameof(SammelueberweisungsProzess), auftrag, 1);
        await h.RunAsync(korr, nameof(SammelueberweisungsProzess), auftrag, 1);

        (await h.Store.LoadStateAsync<Konto>(quelle))!.Saldo.Should().Be(50, "5×10 reserviert und gebucht");
        foreach (var z in ziele)
            (await h.Store.LoadStateAsync<Konto>(z))!.Saldo.Should().Be(10);
        (await Terminal(h, korr)).Should().Be(true);
    }

    // ── M1: Äquivalenz Cursor AUS vs. AN ──

    [Fact]
    public async Task M1_Ueberweisung_linear_join_Cursor_aus_gleich_an()
    {
        var quelle = Guid.NewGuid(); var ziel = Guid.NewGuid(); var auftrag = Guid.NewGuid();
        var korr = ProzessId.Für(nameof(UeberweisungsProzess), auftrag, 1);

        var aus = await FahreUeberweisung(quelle, ziel, auftrag, zielGesperrt: false, mitCursor: false);
        var an  = await FahreUeberweisung(quelle, ziel, auftrag, zielGesperrt: false, mitCursor: true);

        an.Gefeuert.Should().Equal(aus.Gefeuert, "Cursor-Fold trifft je Weckung dieselbe Feuer-Entscheidung");
        an.Erfolg.Should().Be(aus.Erfolg);
        an.SaldoQuelle.Should().Be(aus.SaldoQuelle);
        an.SaldoZiel.Should().Be(aus.SaldoZiel);
        aus.Erfolg.Should().BeTrue();
    }

    [Fact]
    public async Task M1_Ueberweisung_Kompensation_Cursor_aus_gleich_an()
    {
        var quelle = Guid.NewGuid(); var ziel = Guid.NewGuid(); var auftrag = Guid.NewGuid();

        var aus = await FahreUeberweisung(quelle, ziel, auftrag, zielGesperrt: true, mitCursor: false);
        var an  = await FahreUeberweisung(quelle, ziel, auftrag, zielGesperrt: true, mitCursor: true);

        an.Gefeuert.Should().Equal(aus.Gefeuert, "auch Ablehnung + Kompensation faltet der Cursor identisch");
        an.Erfolg.Should().Be(aus.Erfolg);
        aus.Erfolg.Should().BeFalse("gesperrtes Ziel → Kompensation → Fehlgeschlagen");
        an.SaldoQuelle.Should().Be(aus.SaldoQuelle);
        aus.SaldoQuelle.Should().Be(100, "die Reservierung wurde ausgeglichen, nichts gebucht");
    }

    [Fact]
    public async Task M1_Sammelueberweisung_fanout_countjoin_Cursor_aus_gleich_an()
    {
        var quelle = Guid.NewGuid();
        var ziele = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var auftrag = Guid.NewGuid();

        var aus = await FahreSammel(quelle, ziele, auftrag, mitCursor: false);
        var an  = await FahreSammel(quelle, ziele, auftrag, mitCursor: true);

        an.Gefeuert.Should().Equal(aus.Gefeuert, "Fan-out + Count-Join faltet der Cursor identisch");
        an.Erfolg.Should().Be(aus.Erfolg);
        aus.Erfolg.Should().BeTrue();
        an.SaldoQuelle.Should().Be(aus.SaldoQuelle);
    }

    // ── §6 Probe 2: stale RegelHash → sauberer Voll-Fold, gleiches Ergebnis ──

    [Fact]
    public async Task Stale_RegelHash_faellt_auf_VollFold_zurueck_gleiches_Ergebnis()
    {
        var quelle = Guid.NewGuid(); var ziel = Guid.NewGuid(); var auftrag = Guid.NewGuid();
        var korr = ProzessId.Für(nameof(UeberweisungsProzess), auftrag, 1);

        // Marking-Store mit einem FALSCHEN Hash vorbelegen → der Manager muss ihn verwerfen und ab 0 falten.
        var store = new InMemoryProzessMarkingStore();
        await store.SchreibeAsync(new ProzessMarking
        {
            Id = korr, RegelHash = "deadbeef",
            StreamCursor = new Dictionary<Guid, int> { [quelle] = 999, [ziel] = 999 },
            Marking = new MarkingKompakt(),
        }, Ct);

        var reg = Registry(new UeberweisungsProzess());
        var h = new ProzessSagaHarness(reg, store);
        await h.EröffneKontoAsync(quelle, 100);
        await h.EröffneKontoAsync(ziel, 0);
        await h.AuslöserAsync(auftrag, new UeberweisungBeauftragt(quelle, ziel, 30), "Ueberweisungsauftrag");

        await h.RunAsync(korr, nameof(UeberweisungsProzess), auftrag, 1);

        // Trotz des stale (hohen) Cursors korrekt: der falsche Hash verwarf ihn → Voll-Fold ab 0.
        (await h.Store.LoadStateAsync<Konto>(quelle))!.Saldo.Should().Be(70);
        (await h.Store.LoadStateAsync<Konto>(ziel))!.Saldo.Should().Be(30);
        (await Terminal(h, korr)).Should().Be(true);
    }

    // ── §6 Probe 4: ein bewusst STALE Marking feuert erneut → verpufft (kein Doppeleffekt) ──

    [Fact]
    public async Task Stale_Marking_feuert_erneut_aber_verpufft_kein_Doppeleffekt()
    {
        var quelle = Guid.NewGuid(); var ziel = Guid.NewGuid(); var auftrag = Guid.NewGuid();
        var korr = ProzessId.Für(nameof(UeberweisungsProzess), auftrag, 1);
        var reg = Registry(new UeberweisungsProzess());

        var h = new ProzessSagaHarness(reg, new InMemoryProzessMarkingStore());
        await h.EröffneKontoAsync(quelle, 100);
        await h.EröffneKontoAsync(ziel, 0);
        await h.AuslöserAsync(auftrag, new UeberweisungBeauftragt(quelle, ziel, 30), "Ueberweisungsauftrag");
        // NUR teilweise treiben (reservieren + gutschreiben erledigt, buchen noch offen) → der Prozess ist
        // AKTIV, nicht terminal; nur dann faltet eine weitere Weckung überhaupt (ein beendeter Prozess kehrt sofort zurück).
        var terminal = await h.TeilRunAsync(korr, nameof(UeberweisungsProzess), auftrag, 1, wakes: 1);
        terminal.Should().BeFalse("die Buchung ist noch offen — der Prozess läuft");

        var reserviertVorher = (await h.Store.LoadStateAsync<Konto>(quelle))!.Reserviert;
        var saldoZ = (await h.Store.LoadStateAsync<Konto>(ziel))!.Saldo;
        saldoZ.Should().Be(30, "gutgeschrieben");

        // Stale Marking: RICHTIGER Hash (sonst Voll-Fold), aber Cursor hoch + leere Ergebnisse → jede
        // Transition sieht „pending" → der Manager feuert die erste (reservieren) ERNEUT. Der Empfänger
        // dedupliziert über die Framework-Inbox → verpufft, kein Doppeleffekt.
        var stale = new InMemoryProzessMarkingStore();
        await stale.SchreibeAsync(new ProzessMarking
        {
            Id = korr,
            RegelHash = ProzessRegelHash.Berechne(reg[nameof(UeberweisungsProzess)]),
            StreamCursor = new Dictionary<Guid, int> { [quelle] = 9999, [ziel] = 9999 },
            Marking = new MarkingKompakt(),   // leer → alles sieht unaufgelöst aus
        }, Ct);

        var reFires = await h.ExtraWeckungAsync(korr, stale);

        reFires.Should().BeGreaterThan(0, "das stale Marking lässt eine erledigte Transition erneut feuern");
        (await h.Store.LoadStateAsync<Konto>(quelle))!.Reserviert.Should().Be(reserviertVorher,
            "der Empfänger dedupliziert (Inbox) → das Re-Feuern verpufft, kein doppeltes Reservieren");
        (await h.Store.LoadStateAsync<Konto>(ziel))!.Saldo.Should().Be(saldoZ, "kein Doppeleffekt");
    }

    // ── §6 Probe 5 / M3: Fan-out-Skalierung — der Read-Zähler zeigt O(N) statt O(N²) ──

    [Fact]
    public async Task Fanout_Reads_skalieren_mit_Cursor_linear_statt_quadratisch()
    {
        // Zwei Breiten messen: ohne Cursor wächst die Read-Last super-linear (~N²), mit Cursor ~linear.
        var (ausN, anN)   = await MissFanout(40);
        var (aus2N, an2N) = await MissFanout(80);

        // Cursor AN liest bei doppelter Breite grob doppelt (linear); AUS deutlich mehr (quadratisch).
        double ratioAus = (double)aus2N / ausN;
        double ratioAn  = (double)an2N / anN;

        ratioAn.Should().BeLessThan(2.8, "mit Cursor wächst die Read-Last ~linear mit N");
        ratioAus.Should().BeGreaterThan(3.2, "ohne Cursor wächst sie ~quadratisch (2× Breite → ~4× Reads)");
        an2N.Should().BeLessThan(aus2N / 3, "bei N=80 liest der Cursor um Größenordnungen weniger");
    }

    // ── M3: großer Fan-out (N=1000) — O(N) Read-Volumen statt O(N²), Ergebnis identisch zum Voll-Fold ──

    [Fact]
    public async Task M3_GrosserFanout_N1000_liest_linear_und_identisch_zum_VollFold()
    {
        const int n = 1000;
        var quelle = Guid.NewGuid();
        var ziele = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToArray();
        var auftrag = Guid.NewGuid();
        var korr = ProzessId.Für(nameof(SammelueberweisungsProzess), auftrag, 1);
        var reg = Registry(new SammelueberweisungsProzess());

        async Task<(long Events, List<(string, Guid, Guid)> Feuer, decimal Saldo)> Lauf(bool mitCursor)
        {
            var h = new ProzessSagaHarness(reg, mitCursor ? new InMemoryProzessMarkingStore() : null);
            await h.EröffneKontoAsync(quelle, 10_000_000);
            foreach (var z in ziele) await h.EröffneKontoAsync(z, 0);
            await h.AuslöserAsync(auftrag, new SammelUeberweisungBeauftragt(quelle, ziele, 1), "Sammelueberweisungsauftrag");
            h.Store.ResetZaehler();
            await h.RunAsync(korr, nameof(SammelueberweisungsProzess), auftrag, 1);
            return (h.Store.GeleseneEvents, h.Gefeuert, (await h.Store.LoadStateAsync<Konto>(quelle))!.Saldo);
        }

        var aus = await Lauf(false);
        var an = await Lauf(true);

        // Identität: gleiche Feuer-Sequenz, gleicher Saldo (der Cursor ist reiner Beschleuniger).
        an.Feuer.Should().Equal(aus.Feuer, "der Cursor-Fold trifft dieselben Feuer-Entscheidungen wie der Voll-Fold");
        an.Saldo.Should().Be(aus.Saldo);

        // Skalierung: der Voll-Fold liest O(N²) Event-Hüllen (~jede Weckung alle Streams ab 0), der Cursor O(N).
        // Beleg über den Read-Zähler: der Cursor liest um mehr als eine Größenordnung weniger, und sein
        // Read-Volumen bleibt in der Größenordnung von N (jedes Ziel-Event genau einmal über den Tail).
        an.Events.Should().BeLessThan(aus.Events / 20, "der Cursor senkt das Read-Volumen um >1 Größenordnung");
        an.Events.Should().BeLessThan(50L * n, "das Cursor-Read-Volumen bleibt O(N)");
        aus.Events.Should().BeGreaterThan(50L * n * n / 100, "der Voll-Fold ist erkennbar super-linear (~N²)");
    }

    // ── Helfer ──

    private sealed record Ergebnis(
        List<(string Cmd, Guid Vorgang, Guid Ziel)> Gefeuert, bool Erfolg, decimal SaldoQuelle, decimal SaldoZiel);

    private async Task<Ergebnis> FahreUeberweisung(Guid quelle, Guid ziel, Guid auftrag, bool zielGesperrt, bool mitCursor)
    {
        var reg = Registry(new UeberweisungsProzess());
        var h = new ProzessSagaHarness(reg, mitCursor ? new InMemoryProzessMarkingStore() : null);
        await h.EröffneKontoAsync(quelle, 100);
        await h.EröffneKontoAsync(ziel, 0, gesperrt: zielGesperrt);
        await h.AuslöserAsync(auftrag, new UeberweisungBeauftragt(quelle, ziel, 30), "Ueberweisungsauftrag");

        var korr = ProzessId.Für(nameof(UeberweisungsProzess), auftrag, 1);
        await h.RunAsync(korr, nameof(UeberweisungsProzess), auftrag, 1);

        return new Ergebnis(h.Gefeuert, await Terminal(h, korr),
            (await h.Store.LoadStateAsync<Konto>(quelle))!.Saldo,
            (await h.Store.LoadStateAsync<Konto>(ziel))!.Saldo);
    }

    private async Task<Ergebnis> FahreSammel(Guid quelle, Guid[] ziele, Guid auftrag, bool mitCursor)
    {
        var reg = Registry(new SammelueberweisungsProzess());
        var h = new ProzessSagaHarness(reg, mitCursor ? new InMemoryProzessMarkingStore() : null);
        await h.EröffneKontoAsync(quelle, 1000);
        foreach (var z in ziele) await h.EröffneKontoAsync(z, 0);
        await h.AuslöserAsync(auftrag, new SammelUeberweisungBeauftragt(quelle, ziele, 10), "Sammelueberweisungsauftrag");

        var korr = ProzessId.Für(nameof(SammelueberweisungsProzess), auftrag, 1);
        await h.RunAsync(korr, nameof(SammelueberweisungsProzess), auftrag, 1);

        return new Ergebnis(h.Gefeuert, await Terminal(h, korr),
            (await h.Store.LoadStateAsync<Konto>(quelle))!.Saldo, 0);
    }

    private async Task<(long Aus, long An)> MissFanout(int n)
    {
        var quelle = Guid.NewGuid();
        var ziele = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToArray();
        var auftrag = Guid.NewGuid();
        var korr = ProzessId.Für(nameof(SammelueberweisungsProzess), auftrag, 1);
        var reg = Registry(new SammelueberweisungsProzess());

        async Task<long> Lauf(bool mitCursor)
        {
            var h = new ProzessSagaHarness(reg, mitCursor ? new InMemoryProzessMarkingStore() : null);
            await h.EröffneKontoAsync(quelle, 100_000);
            foreach (var z in ziele) await h.EröffneKontoAsync(z, 0);
            await h.AuslöserAsync(auftrag, new SammelUeberweisungBeauftragt(quelle, ziele, 1), "Sammelueberweisungsauftrag");
            h.Store.ResetZaehler();   // nur die Treibe-Phase messen (Aufbau raus)
            await h.RunAsync(korr, nameof(SammelueberweisungsProzess), auftrag, 1);
            return h.Store.GeleseneEvents;
        }

        return (await Lauf(false), await Lauf(true));
    }

    private static async Task<bool> Terminal(ProzessSagaHarness h, Guid korr)
    {
        var log = await h.Store.ReadStreamAsync(korr, 0, Ct);
        var beendet = log.Select(e => e.Payload).OfType<ProzessBeendet>().LastOrDefault();
        return beendet?.Erfolg ?? throw new InvalidOperationException("Prozess nicht beendet");
    }
}
