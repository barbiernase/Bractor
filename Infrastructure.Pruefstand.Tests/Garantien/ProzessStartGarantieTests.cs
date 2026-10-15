using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Pruefstand.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Der Offen-Index ist eine Obermenge der offenen Vorgänge (Störungslauf PF-12: ein harter Abbruch zwischen <c>ProzessGestartet</c> und dem
/// Index-Eintrag ließ 51 Vorgänge ohne Eintrag liegen — weder Backstop noch Poll weckten sie): Eintrag und Ende reisen als Beilagen im Append
/// des <c>ProzessGestartet</c> bzw. <c>ProzessBeendet</c> (eine Transaktion, docs/03 §3.3), und ein Eintrag ohne Vorgang räumt die
/// Backstop-Weckung ab.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ProzessStartGarantieTests
{
    [Fact]
    public async Task Eintrag_und_Ende_reisen_im_Append_von_Start_und_Ende()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(offenIndex: index);

        await w.StarteAsync();

        var start = w.Log.Appends.Single(a => a.Events.Any(e => e is ProzessGestartet));
        start.Beilagen.Should().Equal(new VorgangOffen(w.Korrelation, "BildAufbereitung"));
        index.Offen.Should().ContainKey(w.Korrelation);

        (await w.TreibeAsync()).Beendet.Should().BeTrue();
        var ende = w.Log.Appends.Single(a => a.Events.Any(e => e is ProzessBeendet));
        ende.Beilagen.Should().Equal(new VorgangBeendet(w.Korrelation));
        index.Offen.Should().BeEmpty();
    }

    [Fact]
    public async Task Ein_Eintrag_ohne_gestarteten_Vorgang_raeumt_die_Backstop_Weckung_ab()
    {
        var index = new OffenIndexImSpeicher();
        var w = new FunktionsWelt(offenIndex: index);
        await index.MarkiereOffenAsync(w.Korrelation, "BildAufbereitung", default);   // Abbruch zwischen Eintrag und ProzessGestartet

        await w.Manager.WakeAsync(w.Korrelation, default, vomBackstop: true);

        index.Offen.Should().NotContainKey(w.Korrelation);
    }
}
