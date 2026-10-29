using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Die Umsetzung einer Funktion lebt je Aktivierung (docs/04 §4.4): gebaut beim Aktivieren in einem eigenen Bereich,
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

    private static object Baue(IServiceProvider sp, object? _) => new Umsetzung(sp.GetRequiredService<Verbindung>(), sp.GetRequiredService<Protokoll>());

    [Fact]
    public async Task Beim_Stoppen_werden_erst_die_Umsetzung_dann_ihre_Dienste_geschlossen()
    {
        var (wurzel, p) = Container();
        var a = new FunktionsAktivierung(wurzel, Baue);
        await a.BeginneAsync();
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
        await erste.BeginneAsync();
        var v1 = ((Umsetzung)erste.Umsetzung).Verbindung;
        await erste.DisposeAsync();

        var zweite = new FunktionsAktivierung(wurzel, Baue);
        await zweite.BeginneAsync();
        ((Umsetzung)zweite.Umsetzung).Verbindung.Nr.Should().NotBe(v1.Nr, "die Ressource gehört der Aktivierung, nicht dem Knoten");
        await zweite.DisposeAsync();
        p.Should().Equal("umsetzung zu", "verbindung zu", "umsetzung zu", "verbindung zu");
    }

    [Fact]
    public async Task Scheitert_der_Bau_wird_der_Bereich_trotzdem_geschlossen()
    {
        var (wurzel, p) = Container();
        var a = new FunktionsAktivierung(wurzel, (sp, _) => { sp.GetRequiredService<Verbindung>(); throw new InvalidOperationException("Modell fehlt"); });
        await a.Invoking(x => x.BeginneAsync().AsTask()).Should().ThrowAsync<InvalidOperationException>();
        p.Should().Equal("verbindung zu");
        await a.DisposeAsync();   // nichts mehr offen
        p.Should().HaveCount(1);
    }

    // ── Lebenszyklus (ILebenszyklus, docs/04 §4.4, docs/10 E10) ──

    private sealed class Netz { public int Gewicht { get; set; } }

    private sealed class Lernend(Abstractions.IZustand<Netz> netz, Protokoll p) : Abstractions.ILebenszyklus
    {
        public int Gpu;   // nur im Test: die Ressource der Aktivierung
        public Task BeimAktivierenAsync(System.Threading.CancellationToken ct) { Gpu = netz.Wert.Gewicht; p.Add($"aktiviert mit {Gpu}"); return Task.CompletedTask; }
        public Task VorDemSichernAsync(System.Threading.CancellationToken ct) { netz.Wert.Gewicht = Gpu; p.Add("zurückgeschrieben"); return Task.CompletedTask; }
    }

    private static FunktionsZustandsform Form() => new(typeof(Netz), () => new Zustand<Netz>(),
        z => System.Text.Encoding.UTF8.GetBytes(((Zustand<Netz>)z).Wert.Gewicht.ToString()),
        (z, d) => ((Zustand<Netz>)z).Wert = new Netz { Gewicht = int.Parse(System.Text.Encoding.UTF8.GetString(d)) },
        z => ((Zustand<Netz>)z).Wert = new Netz());

    [Fact]
    public async Task Der_Lebenszyklus_laeuft_nach_dem_Laden_des_Zustands_und_vor_jeder_Sicherung()
    {
        var (wurzel, p) = Container();
        Lernend? f = null;
        var a = new FunktionsAktivierung(wurzel, (sp, z) => f = new Lernend((Abstractions.IZustand<Netz>)z!, p), Form());
        await a.BeginneAsync();
        a.Lade(System.Text.Encoding.UTF8.GetBytes("7"));          // der Actor lädt den gesicherten Zustand …
        await a.AktiviereAsync(default);                           // … dann der Lebenszyklus: er sieht ihn
        f!.Gpu.Should().Be(7);

        f.Gpu = 9;                                                 // gerechnet wird auf der Ressource
        System.Text.Encoding.UTF8.GetString(await a.SichereAsync(default)).Should().Be("9", "vor dem Sichern schreibt die Funktion zurück");
        p.Should().Equal("aktiviert mit 7", "zurückgeschrieben");
        await a.DisposeAsync();
    }

    [Fact]
    public async Task Ohne_Lebenszyklus_ist_Aktivieren_nichts()
    {
        var (wurzel, _) = Container();
        var a = new FunktionsAktivierung(wurzel, Baue);
        await a.BeginneAsync();
        await a.AktiviereAsync(default);
        await a.DisposeAsync();
    }
}
