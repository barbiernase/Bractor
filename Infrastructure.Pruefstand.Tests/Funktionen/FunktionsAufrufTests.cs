using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Der Aufruf-Knoten <c>Rufe&lt;TFunktion&gt;</c> in der Prozess-Maschine: Katalog-Funktionen werden wie Aggregate gerufen
/// (Auftrag hin, OneOf-Ergebnis als Event im Log zurück), parallel beauftragt, mit Zeitlimit, Fehlschlag und Kompensation —
/// alles mit dem echten <see cref="ProzessManager"/> und dem echten <see cref="FunktionsAusfuehrer"/>, store-frei.
/// </summary>
public class FunktionsAufrufTests
{
    private static readonly TimeSpan Grenze = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Fork_und_Join_rufen_beide_Funktionen_parallel_und_senden_dann_das_Ergebnis()
    {
        var w = new FunktionsWelt();
        // Beweis der Parallelität: die Vorverarbeitung endet erst, wenn die Metadaten-Funktion GESTARTET ist.
        var metaGestartet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.Meta = (_, _) => { metaGestartet.TrySetResult(); return Task.FromResult<OneOf<MetaGelesen>>(new MetaGelesen("K7")); };
        w.Vor = async (a, _) => { await metaGestartet.Task.WaitAsync(Grenze); return new Vorverarbeitet(a.Pfad + ".png"); };

        await w.StarteAsync("bild.tiff");
        var st = await w.TreibeAsync();

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeTrue();
        w.Gesendet.Should().ContainSingle().Which.Should().Be(new MeldeFertig(Bildakte.Id, "bild.tiff.png", "K7"));
        w.Log.Stream(Bildakte.Id).Select(e => e.Payload).OfType<Fertig>().Should().ContainSingle();
    }

    [Fact]
    public async Task Das_Ergebnis_einer_Funktion_liegt_genau_einmal_im_Ausfuehrungs_Stream_mit_Kausalitaet_und_Korrelation()
    {
        var w = new FunktionsWelt();
        await w.StarteAsync();
        await w.TreibeAsync();

        // Jeder Funktions-Aufruf hat seinen Ausführungs-Stream (Id = Vorgang) mit genau einem Ergebnis.
        var vorgänge = w.Log.Stream(Bildakte.Id).Select(e => e.CausationId).ToHashSet();
        var ergebnisse = new[] { typeof(Vorverarbeitet), typeof(MetaGelesen) };
        foreach (var typ in ergebnisse)
        {
            var env = AlleStreams(w).SelectMany(s => s).Single(e => e.Payload.GetType() == typ);
            env.CausationId.Should().Be(env.AggregateId.ToString(), "der Ausführungs-Stream IST der Vorgang");
            env.CorrelationId.Should().Be(w.Korrelation.ToString());
            env.AggregateVersion.Should().Be(1);
            env.AggregateType.Should().StartWith("Funktion:");
        }
        vorgänge.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Ein_anderer_OneOf_Fall_waehlt_den_anderen_Zweig_und_der_ungenutzte_Join_haelt_den_Prozess_nicht_auf()
    {
        var w = new FunktionsWelt();
        w.Vor = (_, _) => Task.FromResult<OneOf<Vorverarbeitet, Unlesbar>>(new Unlesbar("leer"));

        await w.StarteAsync();
        var st = await w.TreibeAsync();

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeTrue("ein fachlicher Fall (Unlesbar) ist ein Ergebnis, kein Fehler");
        w.Gesendet.Should().ContainSingle().Which.Should().Be(new MeldeUnbrauchbar(Bildakte.Id, "leer"));
    }

    [Fact]
    public async Task Eine_werfende_Funktion_wird_wiederholt_und_scheitert_dann_durabel()
    {
        var w = new FunktionsWelt(wiederholungen: 2);
        var versuche = 0;
        w.Vor = (_, _) => { Interlocked.Increment(ref versuche); throw new InvalidOperationException("Kamera kaputt"); };

        await w.StarteAsync();
        var st = await w.TreibeAsync();

        versuche.Should().Be(3, "ein Versuch + zwei Wiederholungen");
        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeFalse();
        w.ManagerLog.Select(e => e.Payload).OfType<SchrittGescheitert>().Should().ContainSingle()
            .Which.Grund.Should().Contain("Kamera kaputt");
        AlleStreams(w).SelectMany(s => s).Select(e => e.Payload).OfType<KommandoAbgelehnt>().Should().ContainSingle();
        w.Gesendet.Should().BeEmpty("nach einem Fehlschlag feuert keine Vorwärts-Transition mehr");
    }

    [Fact]
    public async Task Ein_Zeitlimit_laesst_einen_haengenden_Aufruf_scheitern_und_ein_spaetes_Ergebnis_aendert_nichts()
    {
        var w = new FunktionsWelt(limit: TimeSpan.FromSeconds(30));
        var freigabe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.Vor = async (a, _) => { await freigabe.Task; return new Vorverarbeitet(a.Pfad); };

        await w.StarteAsync();
        var st = await w.TreibeAsync(warteAufFunktionen: false, max: 3);
        st.Beendet.Should().BeFalse("vor Ablauf des Limits wartet der Prozess auf die laufende Funktion");

        w.Log.Jetzt += TimeSpan.FromSeconds(31);
        st = await w.TreibeAsync(warteAufFunktionen: false, max: 5);

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeFalse();
        w.ManagerLog.Select(e => e.Payload).OfType<SchrittGescheitert>().Should().ContainSingle()
            .Which.Grund.Should().Contain("Zeitlimit");

        freigabe.SetResult();
        await w.Ausfuehrer.WarteAufAlleAsync();
        await w.Manager.WakeAsync(w.Korrelation);
        (await w.Manager.LadeStatusAsync(w.Korrelation)).Erfolg.Should().BeFalse("der Fehlschlag ist durabel entschieden");
        w.Gesendet.Should().BeEmpty();
    }

    [Fact]
    public async Task Innerhalb_des_Zeitlimits_laeuft_der_Prozess_normal_zu_Ende()
    {
        var w = new FunktionsWelt(limit: TimeSpan.FromSeconds(30));
        await w.StarteAsync();
        w.Log.Jetzt += TimeSpan.FromSeconds(5);
        var st = await w.TreibeAsync();
        st.Erfolg.Should().BeTrue();
    }

    [Fact]
    public async Task Scheitert_ein_spaeterer_Schritt_wird_die_Wirkung_der_Funktion_kompensiert()
    {
        var w = new FunktionsWelt(mitKompensation: true);
        w.LehneAb.Add(typeof(MeldeFertig));

        await w.StarteAsync("x.tiff");
        var st = await w.TreibeAsync();

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeFalse();
        w.Gesendet.Should().Contain(new RaeumeVorschauAuf(Bildakte.Id, "x.tiff"));
        w.Log.Stream(Bildakte.Id).Select(e => e.Payload).OfType<VorschauAufgeraeumt>().Should().ContainSingle();
    }

    [Fact]
    public async Task Offene_Auftraege_werden_bei_jeder_Weckung_erneut_uebergeben_aber_nur_einmal_gerechnet()
    {
        var w = new FunktionsWelt();
        var gerechnet = 0;
        var freigabe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.Vor = async (a, _) => { Interlocked.Increment(ref gerechnet); await freigabe.Task; return new Vorverarbeitet(a.Pfad); };

        await w.StarteAsync();
        for (var i = 0; i < 4; i++) await w.Manager.WakeAsync(w.Korrelation);   // viele Weckungen, Funktion läuft noch
        w.Uebergaben.Should().BeGreaterThan(2, "jede Weckung übergibt den offenen Auftrag erneut (heilt verlorene Ausführer)");

        freigabe.SetResult();
        var st = await w.TreibeAsync();
        gerechnet.Should().Be(1, "der Ausführer dedupliziert laufende Aufträge");
        st.Erfolg.Should().BeTrue();
    }

    [Fact]
    public async Task Ein_verlorener_Auftrag_heilt_bei_der_naechsten_Weckung()
    {
        var verworfen = 0;
        var w = new FunktionsWelt(umRufe: echt => (k, a, v, akt, ct) =>
            a is VorAuftrag && Interlocked.Increment(ref verworfen) == 1 ? Task.CompletedTask : echt(k, a, v, akt, ct));

        await w.StarteAsync();
        var st = await w.TreibeAsync();

        verworfen.Should().BeGreaterThan(1);
        st.Erfolg.Should().BeTrue();
        w.Gesendet.OfType<MeldeFertig>().Should().ContainSingle();
    }

    [Fact]
    public async Task Ein_schon_vorliegendes_Ergebnis_wird_nicht_neu_gerechnet()
    {
        var w = new FunktionsWelt();
        var gerechnet = 0;
        w.Meta = (_, _) => { Interlocked.Increment(ref gerechnet); return Task.FromResult<OneOf<MetaGelesen>>(new MetaGelesen("K")); };
        var vorgang = Guid.NewGuid();
        await w.Log.AppendEventsAsync(vorgang, 0, new IEvent[] { new MetaGelesen("schon da") }, w.Korrelation.ToString(), vorgang.ToString());

        await w.Ausfuehrer.FuehreAusAsync(w.Korrelation, new MetaAuftrag("p"), vorgang, null, CancellationToken.None);

        gerechnet.Should().Be(0);
        w.Log.Stream(vorgang).Should().ContainSingle();
        w.Weckungen.Should().Contain(w.Korrelation, "auch ein Duplikat weckt den Prozess (er faltet dann das vorhandene Ergebnis)");
    }

    [Fact]
    public async Task Der_Cursor_Pfad_kommt_zum_selben_Ergebnis_wie_der_Voll_Fold()
    {
        var voll = new FunktionsWelt();
        var cursor = new FunktionsWelt(mitCursor: true);
        foreach (var w in new[] { voll, cursor })
        {
            await w.StarteAsync("c.tiff");
            (await w.TreibeAsync()).Erfolg.Should().BeTrue();
        }
        cursor.Gesendet.Should().Equal(voll.Gesendet);
    }

    [Fact]
    public async Task Ohne_Ausfuehrer_scheitert_ein_Funktionsaufruf_laut()
    {
        var log = new LogImSpeicher();
        var m = new ProzessManager(log,
            new Dictionary<string, ProzessRegeln> { [nameof(BildAufbereitung)] = new BildAufbereitung().Regeln },
            (_, _, _, _) => Task.CompletedTask);
        var ausl = Guid.NewGuid();
        await log.AppendEventsAsync(ausl, 0, new IEvent[] { new BildEingegangen("p") });

        var akt = () => m.StarteAsync(Guid.NewGuid(), nameof(BildAufbereitung), ausl, 1);
        await akt.Should().ThrowAsync<InvalidOperationException>().WithMessage("*kein Funktions-Ausführer*");
    }

    // ── DSL, Struktur-Hash, Boot-Guards ──

    [Fact]
    public void Rufe_registriert_die_Funktion_und_Zeitlimit_und_Kompensation_bleiben_beide_erhalten()
    {
        var regeln = new BildAufbereitung(TimeSpan.FromSeconds(9), mitKompensation: true).Regeln;
        var r = regeln.Regeln[0];
        r.Sende.Should().BeNull();
        r.Ruft.Should().NotBeNull();
        r.GerufeneFunktionen.Should().Equal(typeof(IVorverarbeitung));
        r.Zeitlimit.Should().Be(TimeSpan.FromSeconds(9));
        r.RückgängigDurch.Should().NotBeNull();
        r.Ruft!(new IEvent[] { new BildEingegangen("p") }).Should().Equal(new VorAuftrag("p"));
        regeln.TeilnehmendeEvents.Should().Contain(new[] { typeof(Vorverarbeitet), typeof(MetaGelesen), typeof(Unlesbar) });
    }

    [Fact]
    public void Eine_Regel_ruft_genau_ein_Ziel()
    {
        var beide = () => new Regel(new[] { typeof(BildEingegangen) }, _ => Array.Empty<ICommand>(), null,
            ruft: _ => Array.Empty<IAuftrag>());
        var keins = () => new Regel(new[] { typeof(BildEingegangen) }, null, null);
        beide.Should().Throw<ArgumentException>();
        keins.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Der_Struktur_Hash_unterscheidet_Zeitlimits_und_laesst_Regeln_ohne_Funktion_unberuehrt()
    {
        ProzessRegelHash.Berechne(new BildAufbereitung().Regeln)
            .Should().NotBe(ProzessRegelHash.Berechne(new BildAufbereitung(TimeSpan.FromSeconds(1)).Regeln));

        // Eine reine Command-Regel: kein „ruft=" und kein „zeitlimit=" im Hash-Text → bestehende Caches bleiben gültig.
        var nurSende = Prozess<BildEingegangen>.Definiere(p =>
            p.Auf<BildEingegangen>().Sende<MeldeUnbrauchbar>(b => new MeldeUnbrauchbar(Bildakte.Id, b.Pfad)));
        var alt = new ProzessRegeln(nurSende.AuslöserTyp, nurSende.Regeln.Select(x =>
            new Regel(x.Bedingung, x.Sende, x.RückgängigDurch, x.Sammel, x.ProduziertCommands)).ToList());
        ProzessRegelHash.Berechne(nurSende).Should().Be(ProzessRegelHash.Berechne(alt));
    }

    [Fact]
    public void Der_Azyklizitaets_Guard_sieht_Zyklen_durch_Funktionen()
    {
        // BildEingegangen → IVorverarbeitung → Vorverarbeitet → (Regel) → IVorverarbeitung … ist kein DAG.
        var zyklisch = Prozess<BildEingegangen>.Definiere(p =>
        {
            p.Auf<BildEingegangen>().Rufe<IVorverarbeitung>(b => new VorAuftrag(b.Pfad));
            p.Auf<Vorverarbeitet>().Rufe<IVorverarbeitung>(v => new VorAuftrag(v.Vorschau));
        });
        IEnumerable<Type> Produziert(Type t) => t == typeof(IVorverarbeitung) ? new[] { typeof(Vorverarbeitet), typeof(Unlesbar) } : Array.Empty<Type>();

        var akt = () => ProzessAzyklizität.Prüfe("Z", zyklisch, Produziert);
        akt.Should().Throw<InvalidOperationException>().WithMessage("*IVorverarbeitung*");
        ProzessAzyklizität.Prüfe("ok", new BildAufbereitung().Regeln, Produziert);   // der echte Fork/Join ist azyklisch
    }

    [Fact]
    public void Der_Boot_Guard_verlangt_eine_Bindung_fuer_jede_gerufene_Funktion()
    {
        var registry = new Dictionary<string, ProzessRegeln> { ["Bild"] = new BildAufbereitung().Regeln };

        var ohne = () => FunktionsExtensions.PrüfeBindungen(registry, new[] { new FunktionsBindung(typeof(IVorverarbeitung)) });
        ohne.Should().Throw<InvalidOperationException>().WithMessage("*IMetadaten*AddFunktion*");

        FunktionsExtensions.PrüfeBindungen(registry,
            new[] { new FunktionsBindung(typeof(IVorverarbeitung)), new FunktionsBindung(typeof(IMetadaten), Slots: 4) });
    }

    private static IEnumerable<IReadOnlyList<EventEnvelope>> AlleStreams(FunktionsWelt w)
        => w.Log.AlleStreamIds().Select(w.Log.Stream);
}
