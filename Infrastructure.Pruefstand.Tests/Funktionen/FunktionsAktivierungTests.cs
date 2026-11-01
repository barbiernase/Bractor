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
/// beim Stoppen zuerst die Umsetzung, dann ihre gerufenen Funktionen freigegeben; die nächste Aktivierung baut beides neu. Ihr Zustand und der Zustand
/// jeder gerufenen Funktion darin werden als EIN Dokument geladen und gesichert (je Funktion ihr Eintrag).
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
        var a = new FunktionsAktivierung(wurzel, (sp, z) => f = new Lernend((Abstractions.IZustand<Netz>)z!, p), Form(), "Lernend");
        await a.BeginneAsync();
        a.Lade(System.Text.Encoding.UTF8.GetBytes("""{"Lernend":7}"""));   // der Actor lädt den gesicherten Zustand …
        await a.AktiviereAsync(default);                                     // … dann der Lebenszyklus: er sieht ihn
        f!.Gpu.Should().Be(7);

        f.Gpu = 9;                                                           // gerechnet wird auf der Ressource
        System.Text.Encoding.UTF8.GetString(await a.SichereAsync(default)).Should().Be("""{"Lernend":9}""", "vor dem Sichern schreibt die Funktion zurück");
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

    // ── Zustand einer gerufenen Funktion: gehört der Aktivierung des Halters (AktivierungsZustaende), gesichert im selben Dokument ──

    private sealed class Zaehler { public int N { get; set; } }

    private sealed class Zaehlwerk(Abstractions.IZustand<Zaehler> z) { public void Zaehle() => z.Wert.N++; public int N => z.Wert.N; }

    private sealed class Halter(Abstractions.IZustand<Netz> netz, Zaehlwerk werk) { public Abstractions.IZustand<Netz> Netz => netz; public Zaehlwerk Werk => werk; }

    private static FunktionsZustandsform ZaehlerForm() => new(typeof(Zaehler), () => new Zustand<Zaehler>(),
        z => System.Text.Encoding.UTF8.GetBytes(((Zustand<Zaehler>)z).Wert.N.ToString()),
        (z, d) => ((Zustand<Zaehler>)z).Wert = new Zaehler { N = int.Parse(System.Text.Encoding.UTF8.GetString(d)) },
        z => ((Zustand<Zaehler>)z).Wert = new Zaehler());

    [Fact]
    public async Task Der_Zustand_einer_gerufenen_Funktion_wird_mit_dem_des_Halters_geladen_und_gesichert()
    {
        var s = new ServiceCollection();
        s.AddScoped<AktivierungsZustaende>();
        s.AddScoped(sp => new Zaehlwerk((Abstractions.IZustand<Zaehler>)AktivierungsZustaende.Von(sp).Hole("IZaehlwerk", ZaehlerForm())));
        var wurzel = s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Halter? h = null;
        var a = new FunktionsAktivierung(wurzel, (sp, z) => h = new Halter((Abstractions.IZustand<Netz>)z!, sp.GetRequiredService<Zaehlwerk>()), Form(),
            "Halter", gerufenerZustand: true);
        a.MitZustand.Should().BeTrue();
        await a.BeginneAsync();

        a.Lade(System.Text.Encoding.UTF8.GetBytes("""{"Halter":3,"IZaehlwerk":5}"""));
        h!.Netz.Wert.Gewicht.Should().Be(3);
        h.Werk.N.Should().Be(5, "die gerufene Funktion lädt ihren Eintrag aus demselben Dokument");
        h.Werk.Zaehle();
        System.Text.Encoding.UTF8.GetString(a.Sichere()).Should().Be("""{"Halter":3,"IZaehlwerk":6}""");

        a.Lade(System.Text.Encoding.UTF8.GetBytes("""{"Halter":4}"""));
        h.Werk.N.Should().Be(0, "fehlt ihr Eintrag, beginnt sie mit dem Anfangszustand");
        await a.DisposeAsync();
    }

    [Fact]
    public async Task Ein_Halter_ohne_eigenen_Zustand_sichert_nur_die_gerufenen()
    {
        var s = new ServiceCollection();
        s.AddScoped<AktivierungsZustaende>();
        s.AddScoped(sp => new Zaehlwerk((Abstractions.IZustand<Zaehler>)AktivierungsZustaende.Von(sp).Hole("IZaehlwerk", ZaehlerForm())));
        var wurzel = s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Zaehlwerk? w = null;
        var a = new FunktionsAktivierung(wurzel, (sp, _) => w = sp.GetRequiredService<Zaehlwerk>(), null, "Halter", gerufenerZustand: true);
        await a.BeginneAsync();
        a.Lade(null);
        w!.Zaehle();
        System.Text.Encoding.UTF8.GetString(a.Sichere()).Should().Be("""{"IZaehlwerk":1}""");
        await a.DisposeAsync();
    }
}
