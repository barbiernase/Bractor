using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Vorgang;
using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Ebene 1 (reines Routing, kein Cluster) — der Kern der PROZESS-VERKETTUNG: das TERMINALE Domänen-Event von
/// Prozess A (<see cref="VorgangGenehmigt"/>) ist zugleich ein registrierter AUSLÖSER von Prozess B, also
/// startet der <see cref="KorrelationsRouter"/> B als EIGENE, frisch korrelierte Instanz.
///
/// Damit ist bewiesen, dass Verkettung KEINE neue Infrastruktur braucht — sie fällt aus der bestehenden
/// Auslöser-vs-Teilnehmer-Entscheidung im Router. (Dass B danach live durchläuft, deckt der Integrationstest.)
/// </summary>
public class ProzessVerkettungRoutingTests
{
    // RouteAsync liest den Store nicht — ein leerer Stub genügt für den Konstruktor.
    private sealed class LeererStore : IEventStoreRepository
    {
        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid s, int v, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EventEnvelope>>(Array.Empty<EventEnvelope>());
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct)
            => Task.FromResult(new StreamChanges(new List<Guid>(), 0));
        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
            => throw new NotSupportedException();
    }

    private static (KorrelationsRouter Router, List<(Guid Korr, Guid Stream, int Version, string? Start)> Geweckt) Baue()
    {
        var geweckt = new List<(Guid, Guid, int, string?)>();
        var router = new KorrelationsRouter(
            store: new LeererStore(),
            auslöserZuProzess: new Dictionary<Type, string> { [typeof(VorgangGenehmigt)] = "AktivierungsProzess" },
            wecke: (korr, s, v, start, _) => { geweckt.Add((korr, s, v, start)); return Task.CompletedTask; });
        return (router, geweckt);
    }

    [Fact]
    public async Task Terminales_Domaenen_Event_von_Prozess_A_startet_Prozess_B_als_eigene_Instanz()
    {
        var (router, geweckt) = Baue();
        var vorgangStream = Guid.NewGuid();
        var prozessAKorrelation = Guid.NewGuid();   // die Korrelation, die VorgangGenehmigt in seinen Metadaten trägt

        // VorgangGenehmigt (das Ergebnis von A's letzter Transition) taucht auf — der Router routet es.
        await router.RouteAsync(vorgangStream, 1, new VorgangGenehmigt(vorgangStream), prozessAKorrelation.ToString());

        geweckt.Should().ContainSingle("das Auslöser-Event weckt genau die neue Prozess-Instanz");
        var w = geweckt[0];
        w.Start.Should().Be("AktivierungsProzess",
            "VorgangGenehmigt ist ein registrierter Auslöser von B → Start (nicht Teilnehmer-Weckung)");
        w.Korr.Should().Be(ProzessId.Für("AktivierungsProzess", vorgangStream, 1),
            "die Start-Korrelation ist deterministisch aus Prozess-Name + Quell-Stream/Version");
        w.Korr.Should().NotBe(prozessAKorrelation,
            "B ist eine EIGENE Instanz — die Korrelation des ersten Prozesses wird NICHT weitergereicht");
    }

    [Fact]
    public async Task Ein_nicht_registriertes_Teilnehmer_Event_weckt_ueber_die_getragene_Korrelation()
    {
        var (router, geweckt) = Baue();
        var stream = Guid.NewGuid();
        var laufendeKorrelation = Guid.NewGuid();

        // VorgangAktiviert ist KEIN Auslöser (nicht in auslöserZuProzess) → Teilnehmer-Zweig: über die Metadaten.
        await router.RouteAsync(stream, 2, new VorgangAktiviert(stream), laufendeKorrelation.ToString());

        geweckt.Should().ContainSingle();
        geweckt[0].Start.Should().BeNull("Teilnehmer-Event → keine Start-Korrelation");
        geweckt[0].Korr.Should().Be(laufendeKorrelation, "die Weckung nutzt die in den Metadaten getragene Korrelation");
    }
}
