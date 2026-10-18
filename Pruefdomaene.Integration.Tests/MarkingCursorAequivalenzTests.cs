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
/// ein Marking mit falschem RegelHash wird verworfen (Voll-Fold, gleiches Ergebnis), und der Cursor liest beim Fan-out weniger. Varianten:
/// <c>kalt</c> — nach dem Start übernimmt eine neue Aktivierung (Store-Load statt frischem Vorgang); <c>mitRouter</c> — vor jeder
/// Selbst-Weckung weckt der Router mit dem geschriebenen Event als Quelle (gezielte Weckung, docs/04 §4.5); <c>verzögert</c> — das Ziel führt
/// den Command erst nach einer Weckung ohne Quelle aus (sie liest den Ziel-Stream zu früh), danach weckt der Send wie im Host mit Ziel und
/// Vorgang; <c>mitHinweis</c> — jede Selbst-Weckung nennt wie im Host die Version, an der der Send den Ausgang geschrieben hat, und der Fold
/// liest den Ziel-Stream erst ab dort.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class MarkingCursorAequivalenzTests : IClassFixture<MarkingCursorAequivalenzTests.Fixture>
{
    private readonly Fixture _fx;
    public MarkingCursorAequivalenzTests(Fixture fx) => _fx = fx;

    private sealed record Lauf(IReadOnlyList<(string Cmd, Guid Ziel, Guid Vorgang)> Feuerungen, bool Beendet, bool Erfolg, long GeleseneEvents,
        long Abfragen);

    /// <summary>Treibt einen Vorgang sequentiell (Starte + Wake bis Terminal) gegen den echten Marten-Store.</summary>
    private static async Task<Lauf> TreibeAsync(IDocumentStore marten, string prozess, Guid auslöserStream, Func<Welt, Task> seed,
        IProzessMarkingStore? marking, bool kalt = false, bool mitRouter = false, bool verzögert = false, bool mitHinweis = false)
    {
        var store = new ZählenderStore(new MartenEventStore(marten, new Pruefdomaene.AggregateHandlerFactory(), NullLogger<MartenEventStore>.Instance));
        var welt = new Welt(store) { Verzögert = verzögert, MitHinweis = mitHinweis };
        var korrelation = ProzessId.Für(prozess, auslöserStream, 1);
        welt.Korrelation = korrelation;
        await seed(welt);

        var registry = new Dictionary<string, ProzessRegeln> { [prozess] = Anwendung.Prozesse[prozess] };
        ProzessManager Neu() => new(store, registry, welt.DispatchAsync, markingStore: marking, markingSchreibIntervall: 1);
        var mgr = Neu();
        store.Gelesen = 0;
        store.Abfragen = 0;
        await mgr.StarteAsync(korrelation, prozess, auslöserStream, 1, default);
        if (kalt) mgr = Neu();   // eine neue Aktivierung: lädt das Marking aus dem Store, faltet einmal voll
        var schutz = 0;
        while (!(await mgr.LadeStatusAsync(korrelation, default)).Beendet)
        {
            var ausgefuehrt = new List<(Guid Ziel, Guid Vorgang, int ErsteVersion)>();
            if (verzögert)
            {
                await mgr.WakeAsync(korrelation, default);   // zu früh: das Ergebnis steht noch nicht im Ziel-Stream
                while (welt.Ausstehend.TryDequeue(out var a)) ausgefuehrt.Add(await welt.FuehreAusAsync(a.Korrelation, a.Cmd, a.Vorgang));
            }
            else
                while (welt.Ausgefuehrt.TryDequeue(out var a)) ausgefuehrt.Add(a);
            if (mitRouter)
                while (welt.Routen.TryDequeue(out var r)) await mgr.WakeAsync(korrelation, default, quelle: r.Stream, quellVersion: r.Version);
            // Die Selbst-Weckung nach dem Send (wie im Host): mit Ziel und Vorgang, mit Hinweis auch mit der Version des Ausgangs.
            if (verzögert || (mitHinweis && ausgefuehrt.Count > 0))
                foreach (var (ziel, vorgang, erste) in ausgefuehrt)
                    await mgr.WakeAsync(korrelation, default, quelle: ziel, quellVersion: mitHinweis ? erste : 0, vorgang: vorgang);
            else
                await mgr.WakeAsync(korrelation, default);
            if (++schutz > 1_000) throw new InvalidOperationException("Der Vorgang erreichte kein Terminal (Livelock?).");
        }
        var st = await mgr.LadeStatusAsync(korrelation, default);
        return new Lauf(welt.Feuerungen.ToList(), st.Beendet, st.Erfolg, store.Gelesen, store.Abfragen);
    }

    private async Task<(Lauf Aus, Lauf An)> BeideAsync(string prozess, Guid auslöser, Func<Welt, Task> seed, IProzessMarkingStore? an = null,
        bool kalt = false, bool mitRouter = false, bool verzögert = false, bool mitHinweis = false)
    {
        var aus = await TreibeAsync(_fx.Aus, prozess, auslöser, seed, marking: null);
        var mit = await TreibeAsync(_fx.An, prozess, auslöser, seed, an ?? new MartenProzessMarkingStore(_fx.An), kalt, mitRouter, verzögert,
            mitHinweis);
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

    [Theory, InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public async Task Diamant_kalt_oder_mit_Router_gleich_Cursor_aus(bool kalt, bool mitRouter)
    {
        var n = (kalt ? 1 : 0) + (mitRouter ? 2 : 0);
        Guid posten = G("e5e5e5e5", 10 * n + 1), kontingent = G("e5e5e5e5", 10 * n + 2), auftrag = G("e5e5e5e5", 10 * n + 3);
        var (aus, an) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 10));
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 5));
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        }, kalt: kalt, mitRouter: mitRouter);
        aus.Erfolg.Should().BeTrue();
        an.Abfragen.Should().BeLessThan(aus.Abfragen);
    }

    [Theory, InlineData(1, true, false, false), InlineData(2, false, true, false), InlineData(3, false, false, true), InlineData(4, false, true, true)]
    public async Task Kompensation_kalt_mit_Router_oder_verzoegert_gleich_Cursor_aus(int n, bool kalt, bool mitRouter, bool verzögert)
    {
        // Die Ablehnung des Kontingents hinterlässt nur eine Marke (kein Signal): sie findet nur, wer den Ziel-Stream nach dem Send liest.
        Guid posten = G("f6f6f6f6", 10 * n + 1), kontingent = G("f6f6f6f6", 10 * n + 2), auftrag = G("f6f6f6f6", 10 * n + 3);
        var (aus, _) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 10));
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 1));
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        }, kalt: kalt, mitRouter: mitRouter, verzögert: verzögert);
        aus.Erfolg.Should().BeFalse();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Fan_out_verzoegert_gleich_Cursor_aus(bool mitRouter)
    {
        var quelle = G("a7a7a7a7", mitRouter ? 101 : 1);
        var verteilung = G("a7a7a7a7", mitRouter ? 102 : 2);
        var ziele = Enumerable.Range(10, 6).Select(n => G("a7a7a7a7", (mitRouter ? 100 : 0) + n)).ToList();
        var (aus, _) = await BeideAsync(nameof(VerteilProzess), verteilung, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(quelle, 1000));
            foreach (var z in ziele) await w.AnlegenAsync(new EroeffnePosten(z, 0));
            await w.AnlegenAsync(new StarteVerteilung(verteilung, quelle, ziele, 5));
        }, mitRouter: mitRouter, verzögert: true);
        aus.Erfolg.Should().BeTrue();
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
        // Ein frischer Vorgang liest einen Ziel-Stream nur nach einem Feuern dorthin — nie „auf Verdacht“: höchstens eine Abfrage je
        //   Feuerung, dazu je Weckung das Manager-Log, das die Test-Schleife selbst liest (LadeStatusAsync).
        an.Abfragen.Should().BeLessThan(aus.Abfragen / 2, $"Cursor {an.Abfragen} Abfragen, Voll-Fold {aus.Abfragen}");
    }

    [Theory, InlineData(1, false, false, false), InlineData(2, true, false, false), InlineData(3, false, true, false), InlineData(4, false, false, true)]
    public async Task Mit_Hinweis_gleich_Cursor_aus(int n, bool kalt, bool mitRouter, bool verzögert)
    {
        // Diamant mit Kompensation und Fan-out mit Count-Join: jede Selbst-Weckung nennt die Version des Ausgangs.
        Guid posten = G("a8a8a8a8", 10 * n + 1), kontingent = G("a8a8a8a8", 10 * n + 2), auftrag = G("a8a8a8a8", 10 * n + 3);
        var (aus, _) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 10));
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 1));   // reicht nicht → Ablehnung → Kompensation in denselben Posten
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        }, kalt: kalt, mitRouter: mitRouter, verzögert: verzögert, mitHinweis: true);
        aus.Erfolg.Should().BeFalse();

        var quelle = G("a9a9a9a9", 100 * n);
        var verteilung = G("a9a9a9a9", 100 * n + 1);
        var ziele = Enumerable.Range(10, 6).Select(i => G("a9a9a9a9", 100 * n + i)).ToList();
        var (ausV, _) = await BeideAsync(nameof(VerteilProzess), verteilung, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(quelle, 1000));
            foreach (var z in ziele) await w.AnlegenAsync(new EroeffnePosten(z, 0));
            await w.AnlegenAsync(new StarteVerteilung(verteilung, quelle, ziele, 5));
        }, kalt: kalt, mitRouter: mitRouter, verzögert: verzögert, mitHinweis: true);
        ausV.Erfolg.Should().BeTrue();
    }

    [Fact]
    public async Task Langer_Ziel_Stream_liest_weder_mit_noch_ohne_Hinweis_die_Geschichte()
    {
        // Ein Posten, auf den vorher 300 andere Vorgänge reserviert haben (fremde Korrelationen): mit Hinweis liest der Fold ab dem eigenen
        //   Ausgang, ohne Hinweis ab dem Beginn des Vorgangs (davor gibt es nichts Eigenes) — in beiden Fällen nicht die 300 davor.
        const int fremde = 300;
        async Task<Lauf> Lauf(string präfix, bool mitHinweis)
        {
            Guid posten = G(präfix, 1), kontingent = G(präfix, 2), auftrag = G(präfix, 3);
            var (_, an) = await BeideAsync(nameof(AuftragsProzess), auftrag, async w =>
            {
                await w.AnlegenAsync(new EroeffnePosten(posten, 10_000));
                for (var i = 0; i < fremde; i++) await w.FremdAsync(new ReservierePosten(posten, 1));
                await w.AnlegenAsync(new RichteKontingentEin(kontingent, 5));
                await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
            }, mitHinweis: mitHinweis);
            an.Erfolg.Should().BeTrue();
            return an;
        }
        var ohne = await Lauf("b9b9b9b9", mitHinweis: false);
        var mit = await Lauf("c9c9c9c9", mitHinweis: true);
        ohne.GeleseneEvents.Should().BeLessThan(fremde, $"ohne Hinweis liest der Fold ab dem Beginn des Vorgangs ({ohne.GeleseneEvents} Events)");
        mit.GeleseneEvents.Should().BeLessThan(fremde, $"mit Hinweis ab dem eigenen Ausgang ({mit.GeleseneEvents} Events)");
    }

    // ── Wachstum: was ein Vorgang liest, hängt nicht an der Geschichte seiner Ziel-Streams (docs/13 PF-23/PF-24) ──

    /// <summary>Ein Marking-Store, der nichts behält — wie ein Neustart, bevor der Stand je durabel geschrieben wurde.</summary>
    private sealed class VergesslicherStore : IProzessMarkingStore
    {
        public Task<ProzessMarking?> LadeAsync(Guid k, CancellationToken ct) => Task.FromResult<ProzessMarking?>(null);
        public Task SchreibeAsync(ProzessMarking m, CancellationToken ct) => Task.CompletedTask;
        public Task LöscheAsync(Guid k, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Ein Auftrag, dessen Kontingent nicht reicht (→ Kompensation in den Posten), auf einem Posten mit <paramref name="fremde"/> fremden Events davor.</summary>
    private async Task<Lauf> KompensationAufLangemPostenAsync(string präfix, int fremde, IProzessMarkingStore marking, bool kalt)
    {
        Guid posten = G(präfix, 1), kontingent = G(präfix, 2), auftrag = G(präfix, 3);
        var an = await TreibeAsync(_fx.An, nameof(AuftragsProzess), auftrag, async w =>
        {
            await w.AnlegenAsync(new EroeffnePosten(posten, 1_000_000));
            await w.FremdVieleAsync(new ReservierePosten(posten, 1), fremde);
            await w.AnlegenAsync(new RichteKontingentEin(kontingent, 1));   // reicht nicht → Ablehnung → Kompensation in den Posten
            await w.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        }, marking, kalt: kalt, mitHinweis: true);
        an.Erfolg.Should().BeFalse();
        an.Feuerungen.Select(f => f.Cmd).Should().Contain(nameof(GibPostenFrei));
        return an;
    }

    [Fact]
    public async Task Eine_Kompensation_liest_nicht_die_Geschichte_des_Ziel_Streams()
    {
        // Früher las die Prüfung „ist der Gegenzug schon erledigt?“ den ganzen Ziel-Stream ab 0 — bei einem heißen Posten wuchs jede Kompensation
        //   mit seiner Geschichte. Jetzt steht das Ergebnis im Marking.
        var kurz = await KompensationAufLangemPostenAsync("ab01ab01", 10, new MartenProzessMarkingStore(_fx.An), kalt: false);
        var lang = await KompensationAufLangemPostenAsync("ab02ab02", 2_000, new MartenProzessMarkingStore(_fx.An), kalt: false);
        (lang.GeleseneEvents - kurz.GeleseneEvents).Should().BeLessThan(50,
            $"die Kompensation liest gleich viel, ob der Posten 10 oder 2 000 fremde Events trägt ({kurz.GeleseneEvents} gegen {lang.GeleseneEvents})");
    }

    [Fact]
    public async Task Ein_kalter_Vorgang_ohne_gespeicherten_Stand_liest_nur_was_seit_seinem_Beginn_geschrieben_wurde()
    {
        // Nach einem Neustart, bevor der Stand je durabel war: der Fold beginnt ohne Cursor — früher ab Version 0 jedes Ziel-Streams.
        var kurz = await KompensationAufLangemPostenAsync("ab03ab03", 10, new VergesslicherStore(), kalt: true);
        var lang = await KompensationAufLangemPostenAsync("ab04ab04", 2_000, new VergesslicherStore(), kalt: true);
        (lang.GeleseneEvents - kurz.GeleseneEvents).Should().BeLessThan(50,
            $"ein kalter Vorgang liest gleich viel, ob der Posten 10 oder 2 000 Events Geschichte hat ({kurz.GeleseneEvents} gegen {lang.GeleseneEvents})");
    }

    /// <summary>Ein Auftrag, dessen Reservierung ausgeführt ist und dessen Belegung aussteht (der Vorgang wartet) — mit eigenem Manager.</summary>
    private async Task<(ProzessManager Manager, ZählenderStore Store, Welt Welt, Guid Korrelation, Guid Posten, Func<ProzessManager> Neu)>
        WartenderAuftragAsync(string präfix, int markingSchreibIntervall)
    {
        Guid posten = G(präfix, 1), kontingent = G(präfix, 2), auftrag = G(präfix, 3);
        var store = new ZählenderStore(new MartenEventStore(_fx.An, new Pruefdomaene.AggregateHandlerFactory(), NullLogger<MartenEventStore>.Instance));
        var korrelation = ProzessId.Für(nameof(AuftragsProzess), auftrag, 1);
        var welt = new Welt(store) { Verzögert = true, Korrelation = korrelation };
        await welt.AnlegenAsync(new EroeffnePosten(posten, 1_000_000));
        await welt.AnlegenAsync(new RichteKontingentEin(kontingent, 5));
        await welt.AnlegenAsync(new StarteAuftrag(auftrag, posten, kontingent, 3));
        var registry = new Dictionary<string, ProzessRegeln> { [nameof(AuftragsProzess)] = Anwendung.Prozesse[nameof(AuftragsProzess)] };
        var marking = new MartenProzessMarkingStore(_fx.An);
        ProzessManager Neu() => new(store, registry, welt.DispatchAsync, markingStore: marking, markingSchreibIntervall: markingSchreibIntervall);
        var mgr = Neu();
        await mgr.StarteAsync(korrelation, nameof(AuftragsProzess), auftrag, 1, default);
        while (welt.Ausstehend.TryDequeue(out var a) && a.Cmd is ReservierePosten) await welt.FuehreAusAsync(a.Korrelation, a.Cmd, a.Vorgang);
        return (mgr, store, welt, korrelation, posten, Neu);
    }

    [Fact]
    public async Task Fremde_Events_hinter_dem_eigenen_Ausgang_liest_ein_Vorgang_nur_einmal()
    {
        // Andere schreiben viel in den Posten, während der Vorgang wartet: der Cursor rückt bis zum Kopf des Streams, nicht nur bis zum letzten
        //   eigenen Event — sonst läse jede Weckung alles Fremde dahinter erneut.
        var (mgr, store, welt, korrelation, posten, _) = await WartenderAuftragAsync("ab05ab05", markingSchreibIntervall: 1);
        await mgr.WakeAsync(korrelation, default);   // die Reservierung ist gefaltet, die Belegung steht aus
        await welt.FremdVieleAsync(new ReservierePosten(posten, 1), 1_000);
        store.Gelesen = 0;
        for (var i = 0; i < 5; i++) await mgr.WakeAsync(korrelation, default, vomBackstop: true);
        store.Gelesen.Should().BeLessThan(1_100, $"die 1 000 fremden Events einmal, nicht je Weckung ({store.Gelesen} gelesen)");
        (await mgr.LadeStatusAsync(korrelation, default)).Beendet.Should().BeFalse("die Belegung steht noch aus");
    }

    [Fact]
    public async Task Beim_Passivieren_sichert_der_Manager_seinen_Stand()
    {
        // Selten durabel geschrieben (alle 32 Weckungen): ohne Sichern begänne die nächste Aktivierung beim Beginn des Vorgangs und läse alles,
        //   was seither in den Posten kam.
        var (mgr, store, welt, korrelation, posten, neu) = await WartenderAuftragAsync("ab06ab06", markingSchreibIntervall: 32);
        await welt.FremdVieleAsync(new ReservierePosten(posten, 1), 1_000);
        await mgr.WakeAsync(korrelation, default);   // liest den Posten einmal bis zum Kopf
        await mgr.SichereMarkingAsync(korrelation, default);
        var kalt = neu();
        store.Gelesen = 0;
        await kalt.WakeAsync(korrelation, default);
        store.Gelesen.Should().BeLessThan(100, $"die neue Aktivierung liest ab dem gesicherten Stand ({store.Gelesen} gelesen)");
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
        }, an: new FalscherHashStore(), kalt: true);   // kalt: nur eine neue Aktivierung lädt das Marking aus dem Store
        an.Erfolg.Should().BeTrue("trotz korruptem Fremd-Marking läuft der Vorgang korrekt (Hash-Mismatch → Voll-Fold)");
    }

    // ── Die Welt: führt Commands wie der Aggregat-Actor aus (Decide → Events, Noop → KommandoVerarbeitet, Ablehnung → KommandoAbgelehnt) ──

    private sealed class Welt
    {
        private readonly IEventStoreRepository _store;
        private readonly Pruefdomaene.AggregateHandlerFactory _fabrik = new();
        private readonly Dictionary<Guid, (IState State, IAggregateHandler Handler, HashSet<Guid> Erledigt)> _aggregate = new();
        public List<(string Cmd, Guid Ziel, Guid Vorgang)> Feuerungen { get; } = new();
        public Guid Korrelation { get; set; }
        /// <summary>Was der Router wecken würde: je geschriebenes Domänen-Event (Marken erzeugen kein Signal) Stream und Version.</summary>
        public Queue<(Guid Stream, int Version)> Routen { get; } = new();
        /// <summary>Verzögert: ein Command wird erst ausgeführt, wenn der Test es sagt (<see cref="FuehreAusAsync"/>).</summary>
        public bool Verzögert { get; init; }
        public Queue<(Guid Korrelation, ICommand Cmd, Guid Vorgang)> Ausstehend { get; } = new();
        /// <summary>Mit Hinweis: was sofort ausgeführt wurde (Ziel, Vorgang, erste geschriebene Version) — die Test-Schleife weckt damit wie der Send.</summary>
        public bool MitHinweis { get; init; }
        public Queue<(Guid Ziel, Guid Vorgang, int ErsteVersion)> Ausgefuehrt { get; } = new();

        public Welt(IEventStoreRepository store) => _store = store;

        private static IState Neu(ICommand c) => c switch
        {
            EroeffnePosten => new Posten.Posten(),
            RichteKontingentEin => new Kontingent.Kontingent(),
            StarteAuftrag => new Auftrag.Auftrag(),
            StarteVerteilung => new Verteilung.Verteilung(),
            _ => throw new InvalidOperationException($"{c.GetType().Name} legt kein Aggregat an"),
        };

        /// <summary>
        /// Legt ein Aggregat an (Seed). Wie im Cluster trägt nur der Auslöser die Korrelation des Vorgangs; die übrigen Aggregate entstanden in
        /// eigenen Vorgängen — ihre Streams enthalten vor dem ersten Feuern also nichts von diesem.
        /// </summary>
        public async Task AnlegenAsync(ICommand anlegen)
        {
            var s = Neu(anlegen);
            s.Id = anlegen.AggregateId;
            var h = _fabrik.CreateHandler(s);
            _aggregate[s.Id] = (s, h, new HashSet<Guid>());
            var evs = h.HandleCommand(anlegen).ToList();
            var korrelation = anlegen is StarteAuftrag or StarteVerteilung ? Korrelation : Guid.NewGuid();
            await _store.AppendEventsAsync(s.Id, 0, evs, korrelation.ToString(), Guid.NewGuid().ToString(), Typ(anlegen));
            foreach (var e in evs) { h.ApplyEvent(e); s.Version++; }
        }

        private static string Typ(ICommand c) => Anwendung.Routing.CommandToAggregate[c.GetType()];

        public async Task DispatchAsync(Guid korr, ICommand cmd, Guid vorgang, CancellationToken ct)
        {
            if (Verzögert) { Ausstehend.Enqueue((korr, cmd, vorgang)); return; }
            var a = await FuehreAusAsync(korr, cmd, vorgang);
            if (MitHinweis) Ausgefuehrt.Enqueue(a);
        }

        /// <summary>Ein Befehl eines FREMDEN Vorgangs (eigene Korrelation): füllt den Ziel-Stream, zählt nicht als Feuerung.</summary>
        public async Task FremdAsync(ICommand cmd)
        {
            var (s, h, _) = _aggregate[cmd.AggregateId];
            var evs = h.HandleCommand(cmd).Where(e => e is not ITransientEvent).ToList();
            await _store.AppendEventsAsync(cmd.AggregateId, s.Version, evs, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Typ(cmd));
            foreach (var e in evs) { h.ApplyEvent(e); s.Version++; }
        }

        /// <summary>Viele Befehle fremder Vorgänge in einem Append (die Geschichte eines heißen Streams, schnell angelegt).</summary>
        public async Task FremdVieleAsync(ICommand cmd, int anzahl)
        {
            var (s, h, _) = _aggregate[cmd.AggregateId];
            var evs = new List<IEvent>();
            for (var i = 0; i < anzahl; i++)
                foreach (var e in h.HandleCommand(cmd).Where(e => e is not ITransientEvent)) { h.ApplyEvent(e); evs.Add(e); }
            await _store.AppendEventsAsync(cmd.AggregateId, s.Version, evs, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Typ(cmd));
            s.Version += evs.Count;
        }

        /// <summary>Führt den Befehl aus wie der Actor; liefert die Version des ersten geschriebenen Events (0 = als Wiederholung verworfen).</summary>
        public async Task<(Guid Ziel, Guid Vorgang, int ErsteVersion)> FuehreAusAsync(Guid korr, ICommand cmd, Guid vorgang)
        {
            var (s, h, erledigt) = _aggregate[cmd.AggregateId];
            if (!erledigt.Add(vorgang)) return (cmd.AggregateId, vorgang, 0);   // Inbox-Dedup des Actors
            Feuerungen.Add((cmd.GetType().Name, cmd.AggregateId, vorgang));
            var erste = s.Version + 1;

            var alle = h.HandleCommand(cmd).ToList();
            var persistent = alle.Where(e => e is not ITransientEvent).ToList();
            var ablehnungen = alle.Where(e => e is ITransientEvent).ToList();
            IReadOnlyList<IEvent> schreibe = ablehnungen.Count > 0 && persistent.Count == 0
                ? new IEvent[] { new KommandoAbgelehnt(vorgang, ablehnungen[0].GetType().Name) }
                : persistent.Count > 0 ? persistent : new IEvent[] { new KommandoVerarbeitet(vorgang) };   // Noop: nur die Marke
            await _store.AppendEventsAsync(cmd.AggregateId, s.Version, schreibe, korr.ToString(), vorgang.ToString(), Typ(cmd));
            foreach (var e in schreibe)
            {
                if (e is not IProzessIntern) h.ApplyEvent(e);
                s.Version++;
                if (e is not IProzessIntern) Routen.Enqueue((cmd.AggregateId, s.Version));
            }
            return (cmd.AggregateId, vorgang, erste);
        }
    }

    /// <summary>
    /// Zählt die gelesenen Events (Beleg, dass der Cursor nur den Tail liest). Ein korrelierter Read zählt jede Zeile, die Postgres dabei im
    /// Stream übergeht (vom Beginn des Reads bis zum Kopf — die Korrelation hat keinen Index), nicht nur die gelieferten.
    /// </summary>
    private sealed class ZählenderStore : IEventStoreRepository
    {
        private readonly IEventStoreRepository _innen;
        public long Gelesen;
        public long Abfragen;
        public ZählenderStore(IEventStoreRepository innen) => _innen = innen;

        public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null,
            string? causationId = null, string? aggregateType = null, string? akteur = null, IReadOnlyList<Beilage>? beilagen = null)
            => _innen.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType, akteur, beilagen);
        public Task<KommandoAusgang?> AusgangAsync(Guid commandId, CancellationToken ct) => _innen.AusgangAsync(commandId, ct);
        public Task<StreamChanges> ReadChangedStreamsAsync(long afterGlobalSequence, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct) => _innen.ReadChangedStreamsAsync(afterGlobalSequence, typen, limit, ct);
        public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
            => _innen.ReadPartitionAsync(nach, partition, partitionen, typen, ausser, limit, ct);
        public Task<TState?> LoadStateAsync<TState>(Guid aggregateId) where TState : class, IState, new() => _innen.LoadStateAsync<TState>(aggregateId);
        public async Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
        {
            var r = await _innen.ReadStreamAsync(streamId, fromVersion, ct);
            Interlocked.Add(ref Gelesen, r.Count);
            Interlocked.Increment(ref Abfragen);
            return r;
        }
        public async Task<KorrelierteEvents> ReadKorreliertAsync(Guid streamId, int fromVersion, string korrelation, Guid beginn, CancellationToken ct)
        {
            var r = await _innen.ReadKorreliertAsync(streamId, fromVersion, korrelation, beginn, ct);
            Interlocked.Add(ref Gelesen, Math.Max(0, r.Kopf - r.Ab + 1));
            Interlocked.Increment(ref Abfragen);
            return r;
        }
        public async Task<EventEnvelope?> ReadEventAsync(Guid streamId, int version, CancellationToken ct)
        {
            var r = await _innen.ReadEventAsync(streamId, version, ct);
            Interlocked.Add(ref Gelesen, r is null ? 0 : 1);
            Interlocked.Increment(ref Abfragen);
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
