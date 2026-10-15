using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Pruefstand.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Die Frist im Offen-Index (PF-4): ein offenes Zeitlimit steht dort genau auf seinem Ablauf, geschrieben einmal statt je Weckung; der
/// Backstop findet den Vorgang zur Frist (nicht erst beim nächsten Heil-Scan), und die Weckung wegen der Frist lässt den Aufruf scheitern
/// und räumt den Eintrag ab. Ohne Zeitlimit wird nichts geschrieben.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ProzessFristGarantieTests
{
    [Fact]
    public async Task Ein_offenes_Zeitlimit_steht_einmal_als_Frist_im_Index_und_weckt_genau_zum_Ablauf()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(limit: TimeSpan.FromSeconds(30), offenIndex: index);
        var freigabe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.Vor = async (a, _) => { await freigabe.Task; return new Vorverarbeitet(a.Pfad); };
        var start = w.Log.Jetzt;

        await w.StarteAsync();
        await w.TreibeAsync(warteAufFunktionen: false, max: 3);

        index.Fristen.Where(f => f.Korrelation == w.Korrelation).Select(f => f.Frist).Should()
            .Equal(new DateTimeOffset?[] { start + TimeSpan.FromSeconds(30) }, "die Frist ist der Ablauf des Zeitlimits, einmal geschrieben");
        (await index.ListeFaelligeAsync(start + TimeSpan.FromSeconds(29), default)).Should().BeEmpty();
        (await index.ListeFaelligeAsync(start + TimeSpan.FromSeconds(30), default)).Should().Equal(w.Korrelation);

        w.Log.Jetzt = start + TimeSpan.FromSeconds(30);
        await w.Manager.WakeAsync(w.Korrelation, default, fristFaellig: true);
        await w.TreibeAsync(warteAufFunktionen: false, max: 3);

        var st = await w.Manager.LadeStatusAsync(w.Korrelation);
        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeFalse();
        w.ManagerLog.Select(e => e.Payload).OfType<SchrittGescheitert>().Should().ContainSingle().Which.Grund.Should().Contain("Zeitlimit");
        index.Offen.Should().NotContainKey(w.Korrelation, "beendet: kein Eintrag, keine Frist mehr");
        freigabe.SetResult();
    }

    [Fact]
    public async Task Ohne_Zeitlimit_wird_keine_Frist_geschrieben()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(offenIndex: index);

        await w.StarteAsync();
        (await w.TreibeAsync()).Erfolg.Should().BeTrue();

        index.Fristen.Should().BeEmpty();
    }

    [Fact]
    public async Task Eine_faellige_Frist_eines_schon_beendeten_Vorgangs_raeumt_den_Eintrag_ab()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(offenIndex: index);
        await w.StarteAsync();
        (await w.TreibeAsync()).Beendet.Should().BeTrue();
        // Das Löschen ging verloren: der Eintrag steht mit abgelaufener Frist.
        index.Offen[w.Korrelation] = w.Log.Jetzt - TimeSpan.FromSeconds(1);

        await w.Manager.WakeAsync(w.Korrelation, default, fristFaellig: true);

        index.Offen.Should().NotContainKey(w.Korrelation, "sonst weckte ihn der Backstop jede Sekunde");
    }
}
