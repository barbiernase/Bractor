using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Projections;
using Infrastructure.Pruefstand.BatchKonsum;

namespace Infrastructure.Pruefstand.Tests.BatchKonsum;

/// <summary>
/// Die Store-Klassen zur Laufzeit (Plan Pub/Sub-Neustrukturierung §2.4, R14): der Store einer Projektion wird aus ihren Fähigkeiten aufgelöst
/// (dieselbe Instanz, genau eine Klasse, Klasse 3 nur mit Erlaubnis), und die Naht schreibt je Klasse richtig — Klasse 1 Wirkungen und
/// Fortschritt zusammen, Klasse 2/3 erst die Wirkungen, dann den Fortschritt beim Framework, in der Generation aus dem Fortschritt.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class StoreKlassenTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void Ein_Store_der_Klasse_3_braucht_die_ausdrueckliche_Erlaubnis()
    {
        var senke = new Ziel3();
        var ohne = () => Projektionslauf.Ziel("Zaehler", false, senke, senke);
        ohne.Should().Throw<InvalidOperationException>().WithMessage("*Klasse 3*MindestensEinmal*");
        Projektionslauf.Ziel("Zaehler", true, senke, senke).Should().BeSameAs(senke);
    }

    [Fact]
    public void Die_Faehigkeiten_einer_Projektion_muessen_einen_Store_aufloesen()
    {
        var zwei = () => Projektionslauf.Ziel("P", false, new Ziel2(), new Ziel2());
        zwei.Should().Throw<InvalidOperationException>().WithMessage("*genau einen Store*");
        var keiner = () => Projektionslauf.Ziel("P", false, new object());
        keiner.Should().Throw<InvalidOperationException>().WithMessage("*IProjektionsZiel*");
    }

    [Fact]
    public void Ein_Store_mit_zwei_Klassen_wird_abgelehnt()
    {
        var akt = () => Projektionsnaht.Klassenzahl(new ZweiKlassen());
        akt.Should().Throw<InvalidOperationException>().WithMessage("*genau eine*");
    }

    [Fact]
    public async Task Klasse_2_schreibt_erst_die_Wirkungen_dann_den_Fortschritt_in_der_Generation_des_Fortschritts()
    {
        var ziel = new Ziel2();
        var framework = new Framework();
        var naht = new Projektionsnaht(ziel, framework);
        var stand = PartitionsCheckpoint.Neu("p", 0);
        stand.Generation = "g7";

        naht.Beginne(stand.Generation);
        await naht.CommitAsync(stand, new Dictionary<Guid, int>(), Ct);
        ziel.Reihe.Should().Equal("beginne g7", "schreibe");
        framework.Geschrieben.Should().Be(1, "der Fortschritt nach den Wirkungen");

        framework.Scheitert = true;
        naht.Beginne(stand.Generation);
        var akt = () => naht.CommitAsync(stand, new Dictionary<Guid, int>(), Ct);
        await akt.Should().ThrowAsync<PartitionsKonfliktException>();
        ziel.Reihe.Should().EndWith("schreibe", "die Wirkungen sind geschrieben, auch wenn der Fortschritt danach verliert — darum wiederholungsfest");
    }

    [Fact]
    public async Task Klasse_1_schreibt_Wirkungen_und_Fortschritt_in_einem_Schritt_ohne_den_Fortschritt_beim_Framework()
    {
        var db = new SpeicherKonsum();
        var ziel = new SpeicherZiel(db);
        var naht = new Projektionsnaht(ziel, null);
        naht.Ablage.Should().BeSameAs(ziel);
        naht.Beginne(Generationen.Erste);
        (await naht.CommitAsync(PartitionsCheckpoint.Neu("p", 0), new Dictionary<Guid, int>(), Ct)).Should().Be(1);
        var ohneFramework = () => new Projektionsnaht(new Ziel2(), null);
        ohneFramework.Should().Throw<InvalidOperationException>().WithMessage("*Klasse 2*Fortschritt beim Framework*");
    }

    private class Ziel : IProjektionsZiel
    {
        public readonly List<string> Reihe = new();
        public void Beginne(string generation) => Reihe.Add("beginne " + generation);
        public void Stream(Guid stream) { }
        public Task SchreibeAsync(CancellationToken ct) { Reihe.Add("schreibe"); return Task.CompletedTask; }
        public Task NeueGenerationAsync(string generation, CancellationToken ct) => Task.CompletedTask;
        public Task UmschaltenAsync(string generation, CancellationToken ct) => Task.CompletedTask;
    }

    private class Ziel2 : Ziel, IWiederholungsfestesZiel { }

    private sealed class Ziel3 : Ziel, INurMindestensEinmal { }

    private sealed class ZweiKlassen : Ziel2, INurMindestensEinmal { }

    private sealed class Framework : IFortschrittBeimFramework
    {
        public int Geschrieben;
        public bool Scheitert;
        public Task<PartitionsCheckpoint> LadeFortschrittAsync(string projektion, int partition, CancellationToken ct)
            => Task.FromResult(PartitionsCheckpoint.Neu(projektion, partition));
        public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektion, IReadOnlyCollection<Guid> streams, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
        public Task FortschrittZuruecksetzenAsync(string projektion, int partitionen, string generation, CancellationToken ct) => Task.CompletedTask;
        public Task<int> SchreibeFortschrittAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
        {
            if (Scheitert) throw new PartitionsKonfliktException("zweite Aktivierung", new Exception());
            return Task.FromResult(++Geschrieben);
        }
    }
}
