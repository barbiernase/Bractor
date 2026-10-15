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
/// Index-Eintrag ließ 51 Vorgänge ohne Eintrag liegen — weder Backstop noch Poll weckten sie): der Eintrag steht, bevor der Vorgang im Log
/// gestartet ist, und ein Eintrag ohne Vorgang räumt die Backstop-Weckung ab.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ProzessStartGarantieTests
{
    /// <summary>Notiert beim Eintrag, ob der Vorgang im Log schon gestartet war.</summary>
    private sealed class PruefenderIndex(Func<Guid, bool> gestartet) : IProzessOffenIndex
    {
        public readonly List<(Guid Korrelation, bool SchonGestartet)> Eintraege = new();
        public readonly HashSet<Guid> Offen = new();
        public Task MarkiereOffenAsync(Guid k, string name, CancellationToken ct) { Eintraege.Add((k, gestartet(k))); Offen.Add(k); return Task.CompletedTask; }
        public Task MarkiereBeendetAsync(Guid k, CancellationToken ct) { Offen.Remove(k); return Task.CompletedTask; }
        public Task<IReadOnlyList<Guid>> ListeOffeneAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(Offen.ToList());
    }

    [Fact]
    public async Task Der_Eintrag_steht_bevor_der_Vorgang_im_Log_gestartet_ist()
    {
        FunktionsWelt? w = null;
        var index = new PruefenderIndex(k => w!.Log.Stream(k).Any(e => e.Payload is ProzessGestartet));
        w = new FunktionsWelt(offenIndex: index);

        await w.StarteAsync();

        index.Eintraege.Should().Equal((w.Korrelation, false));
        (await w.TreibeAsync()).Beendet.Should().BeTrue();
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
