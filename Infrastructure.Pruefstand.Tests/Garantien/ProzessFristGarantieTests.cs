using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Pruefstand.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Die Frist im Offen-Index (PF-4): ein Vorgang, der während eines offenen Zeitlimits still wird, steht nach der Heil-Weckung des Backstops
/// genau mit dem Ablauf im Index, geschrieben einmal; der Backstop findet ihn zur Frist, die Weckung lässt den Aufruf scheitern und räumt
/// den Eintrag ab. Wer vorher fertig wird, schreibt nie eine Frist; ein Zeitlimit kürzer als die Stille bis zur Heilung steht sofort dort.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ProzessFristGarantieTests
{
    private static (FunktionsWelt Welt, OffenIndexImSpeicher Index, TaskCompletionSource Freigabe) HaengendeWelt(TimeSpan limit)
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(limit: limit, offenIndex: index);
        var freigabe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.Vor = async (a, _) => { await freigabe.Task; return new Vorverarbeitet(a.Pfad); };
        return (w, index, freigabe);
    }

    [Fact]
    public async Task Ein_still_gewordener_Vorgang_steht_nach_der_Heilung_mit_seinem_Zeitlimit_im_Index_und_scheitert_genau_zum_Ablauf()
    {
        var (w, index, freigabe) = HaengendeWelt(TimeSpan.FromMinutes(5));
        var start = w.Log.Jetzt;
        await w.StarteAsync();
        await w.TreibeAsync(warteAufFunktionen: false, max: 3);
        index.Fristen.Should().BeEmpty("solange der Vorgang aktiv ist, schreibt er keine Frist");

        w.Log.Jetzt = start + Heilplan.Anfang;
        await w.Manager.WakeAsync(w.Korrelation, default, vomBackstop: true);   // Heil-Weckung nach Stille
        await w.Manager.WakeAsync(w.Korrelation, default, vomBackstop: true);
        index.Fristen.Where(f => f.Korrelation == w.Korrelation).Select(f => f.Frist).Should()
            .Equal(new DateTimeOffset?[] { start + TimeSpan.FromMinutes(5) }, "die Frist ist der Ablauf des Zeitlimits, einmal geschrieben");
        (await index.ListeFaelligeAsync(start + TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1), default)).Should().BeEmpty();
        (await index.ListeFaelligeAsync(start + TimeSpan.FromMinutes(5), default)).Should().Equal(w.Korrelation);

        w.Log.Jetzt = start + TimeSpan.FromMinutes(5);
        await w.Manager.WakeAsync(w.Korrelation, default, vomBackstop: true);
        await w.TreibeAsync(warteAufFunktionen: false, max: 3);

        var st = await w.Manager.LadeStatusAsync(w.Korrelation);
        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeFalse();
        w.ManagerLog.Select(e => e.Payload).OfType<SchrittGescheitert>().Should().ContainSingle().Which.Grund.Should().Contain("Zeitlimit");
        index.Offen.Should().NotContainKey(w.Korrelation, "beendet: kein Eintrag, keine Frist mehr");
        freigabe.SetResult();
    }

    [Fact]
    public async Task Ein_Zeitlimit_kuerzer_als_die_Stille_bis_zur_Heilung_steht_sofort_im_Index()
    {
        var (w, index, freigabe) = HaengendeWelt(TimeSpan.FromSeconds(5));
        var start = w.Log.Jetzt;
        await w.StarteAsync();
        await w.TreibeAsync(warteAufFunktionen: false, max: 3);

        index.Fristen.Select(f => f.Frist).Should().Equal(new DateTimeOffset?[] { start + TimeSpan.FromSeconds(5) });
        freigabe.SetResult();
    }

    [Fact]
    public async Task Ein_Vorgang_der_vor_der_Heilung_fertig_wird_schreibt_keine_Frist()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(limit: TimeSpan.FromMinutes(5), offenIndex: index);

        await w.StarteAsync();
        (await w.TreibeAsync()).Erfolg.Should().BeTrue();

        index.Fristen.Should().BeEmpty();
    }

    [Fact]
    public async Task Eine_Backstop_Weckung_eines_schon_beendeten_Vorgangs_raeumt_den_Eintrag_ab()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(offenIndex: index);
        await w.StarteAsync();
        (await w.TreibeAsync()).Beendet.Should().BeTrue();
        // Das Löschen ging verloren: der Eintrag steht mit abgelaufener Frist.
        index.Offen[w.Korrelation] = w.Log.Jetzt - TimeSpan.FromSeconds(1);

        await w.Manager.WakeAsync(w.Korrelation, default, vomBackstop: true);

        index.Offen.Should().NotContainKey(w.Korrelation, "sonst weckte ihn der Backstop jede Sekunde");
    }
}
