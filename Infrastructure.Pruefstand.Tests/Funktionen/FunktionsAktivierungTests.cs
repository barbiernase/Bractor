using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Die Umsetzung einer Funktion lebt je Aktivierung (Plan Pub/Sub-Neustrukturierung §2.9, Phase 4b): gebaut beim Aktivieren in einem eigenen Bereich,
/// beim Stoppen zuerst die Umsetzung, dann ihre Dienste freigegeben; die nächste Aktivierung baut beides neu.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsAktivierungTests
{
    private sealed class Protokoll : List<string> { }

    private sealed class Verbindung(Protokoll p) : IAsyncDisposable
    {
        public readonly Guid Nr = Guid.NewGuid();
        public ValueTask DisposeAsync() { p.Add("verbindung zu"); return ValueTask.CompletedTask; }
    }

    private sealed class Umsetzung(Verbindung v, Protokoll p) : IDisposable
    {
        public Verbindung Verbindung => v;
        public void Dispose() => p.Add("umsetzung zu");
    }

    private static (IServiceProvider Wurzel, Protokoll P) Container()
    {
        var p = new Protokoll();
        var s = new ServiceCollection();
        s.AddSingleton(p);
        s.AddScoped(sp => new Verbindung(sp.GetRequiredService<Protokoll>()));   // ein Dienst: im Bereich der Aktivierung gebunden
        return (s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), p);
    }

    private static object Baue(IServiceProvider sp) => new Umsetzung(sp.GetRequiredService<Verbindung>(), sp.GetRequiredService<Protokoll>());

    [Fact]
    public async Task Beim_Stoppen_werden_erst_die_Umsetzung_dann_ihre_Dienste_geschlossen()
    {
        var (wurzel, p) = Container();
        var a = new FunktionsAktivierung(wurzel, Baue);
        a.Beginne();
        a.Aktiv.Should().BeTrue();
        p.Should().BeEmpty();

        await a.DisposeAsync();
        p.Should().Equal("umsetzung zu", "verbindung zu");
        a.Aktiv.Should().BeFalse();
        a.Invoking(x => x.Umsetzung).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Jede_Aktivierung_baut_Umsetzung_und_Dienste_neu()
    {
        var (wurzel, p) = Container();
        var erste = new FunktionsAktivierung(wurzel, Baue);
        erste.Beginne();
        var v1 = ((Umsetzung)erste.Umsetzung).Verbindung;
        await erste.DisposeAsync();

        var zweite = new FunktionsAktivierung(wurzel, Baue);
        zweite.Beginne();
        ((Umsetzung)zweite.Umsetzung).Verbindung.Nr.Should().NotBe(v1.Nr, "die Ressource gehört der Aktivierung, nicht dem Knoten");
        await zweite.DisposeAsync();
        p.Should().Equal("umsetzung zu", "verbindung zu", "umsetzung zu", "verbindung zu");
    }

    [Fact]
    public async Task Scheitert_der_Bau_wird_der_Bereich_trotzdem_geschlossen()
    {
        var (wurzel, p) = Container();
        var a = new FunktionsAktivierung(wurzel, sp => { sp.GetRequiredService<Verbindung>(); throw new InvalidOperationException("Modell fehlt"); });
        a.Invoking(x => x.Beginne()).Should().Throw<InvalidOperationException>();
        p.Should().Equal("verbindung zu");
        await a.DisposeAsync();   // nichts mehr offen
        p.Should().HaveCount(1);
    }
}
