using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.SourceGeneration;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Die Bindung der Technik (docs/06 §6.5): der <c>TechnikGenerator</c> liest die Technik (ohne Marker: was der Konstruktor einer Funktion nimmt),
/// Umsetzungen und Konfig-Records nur aus Code-Fakten; die Bindung beim Start wählt nach der Umgebung (<c>Funktionen:&lt;Name&gt;:Implementierung</c>)
/// und bricht laut bei fehlender, mehrdeutiger oder zyklischer Umsetzung bzw. fehlender Konfig; der Analyzer (CQRS089–CQRS092) hält die Form im Build.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class TechnikBindungTests
{
    // ── Generator ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly ImmutableArray<MetadataReference> Refs = Referenzen();
    private static ImmutableArray<MetadataReference> Referenzen()
    {
        var dazu = new[] { typeof(IFunktion).Assembly.Location, typeof(Microsoft.Extensions.Logging.ILogger<>).Assembly.Location };
        return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => a.Location)
            .Concat(dazu).Distinct().Select(l => (MetadataReference)MetadataReference.CreateFromFile(l)).ToImmutableArray();
    }

    private const string Vorspann = "using System; using System.Threading.Tasks; using Abstractions; using Microsoft.Extensions.Logging;\n";

    private static CSharpCompilation Probe(string code)
    {
        var comp = CSharpCompilation.Create("TechnikProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code) }, Refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return comp;
    }

    private static string Generiert(string code)
    {
        CSharpGeneratorDriver.Create(new Infrastructure.SourceGeneration.TechnikGenerator()).RunGeneratorsAndUpdateCompilation(Probe(code), out var neu, out _);
        return string.Join("\n", neu.SyntaxTrees.Skip(1).Select(t => t.ToString()));
    }

    private const string Bild = @"namespace Probe {
        public sealed record Gemacht(string X) : IEvent;
        public sealed record Angefordert(string X) : IEvent;
        public interface IMach : IFunktion { Gemacht Auf(Angefordert e); }
        public interface ISkalierer { int Skaliere(int x); }
        public sealed class Skalierer(ILogger<Skalierer> log) : ISkalierer { public int Skaliere(int x) => x; }
        public enum Modus { Schnell = 1, Gruendlich = 2 }
        public record BildKonfig(string Ausgabe, int Hoehe, Modus Modus, TimeSpan Frist, bool Laut = false, string[]? Endungen = null);
        public sealed class Mach(ISkalierer s, BildKonfig k) : IMach { public Gemacht Auf(Angefordert e) => default!; }
    }";

    [Fact]
    public void Technik_Umsetzung_Bedarf_und_Konfig_Record_werden_aus_dem_Code_gelesen()
    {
        var q = Generiert(Bild);
        q.Should().Contain("[\"ISkalierer\"] = typeof(global::Probe.ISkalierer)");
        q.Should().Contain("new TechnikUmsetzung(typeof(global::Probe.ISkalierer), \"Skalierer\", typeof(global::Probe.Skalierer), "
                           + "s => s.AddScoped<global::Probe.ISkalierer>(sp => new global::Probe.Skalierer(");
        q.Should().Contain("GetRequiredService<global::Microsoft.Extensions.Logging.ILogger<global::Probe.Skalierer>>(sp)", "die Fabrik ruft den Konstruktor direkt");
        q.Should().Contain("[typeof(global::Probe.Mach)] = new Type[] { typeof(global::Probe.ISkalierer), typeof(global::Probe.BildKonfig) }");
        q.Should().MatchRegex(@"new KonfigRecord\(typeof\(global::Probe\.BildKonfig\), ""BildKonfig"", Konfig\d+\)");
        q.Should().Contain("KonfigLeser.NurBekannte(s, \"Ausgabe\", \"Hoehe\", \"Modus\", \"Frist\", \"Laut\", \"Endungen\")");
        q.Should().Contain("KonfigLeser.Text(s, \"Ausgabe\"), KonfigLeser.Zahl(s, \"Hoehe\"), KonfigLeser.Wahl<global::Probe.Modus>(s, \"Modus\"), KonfigLeser.Dauer(s, \"Frist\")");
        q.Should().Contain("(KonfigLeser.Hat(s, \"Laut\") ? KonfigLeser.JaNein(s, \"Laut\") : false)", "ein Default gilt, wenn das Feld fehlt");
        q.Should().Contain("(KonfigLeser.Hat(s, \"Endungen\") ? KonfigLeser.Texte(s, \"Endungen\") : default!)");
    }

    [Fact]
    public void Eine_Funktion_ist_keine_Technik()
        => Generiert(Bild).Should().NotContain("[\"IMach\"]", "sie ruft das Framework — ihre Bindung steht in GeneratedFunktionsBausteine");

    [Fact]
    public void Was_keine_Funktion_im_Konstruktor_nimmt_ist_keine_Technik()
        => Generiert(Bild + " namespace Probe { public interface IFrei { int X(); } public sealed class Frei : IFrei { public int X() => 1; } }")
            .Should().NotContain("IFrei", "Technik ist allein, was der Konstruktor einer Funktion (oder einer Technik-Umsetzung) nimmt");

    [Fact]
    public void Technik_im_Konstruktor_einer_Technik_Umsetzung_ist_Technik()
    {
        var q = Generiert(Bild.Replace("Skalierer(ILogger<Skalierer> log)", "Skalierer(ILogger<Skalierer> log, IMesser m)")
            + " namespace Probe { public interface IMesser { int Miss(); } public sealed class Messer : IMesser { public int Miss() => 1; } }");
        q.Should().Contain("[\"IMesser\"] = typeof(global::Probe.IMesser)");
        q.Should().Contain("[typeof(global::Probe.Skalierer)] = new Type[] { typeof(global::Probe.IMesser) },");
    }

    [Fact]
    public void Eine_Technik_Klasse_ist_ihre_eigene_Umsetzung()
    {
        var q = Generiert(Bild.Replace("Mach(ISkalierer s, BildKonfig k)", "Mach(ISkalierer s, BildKonfig k, Zaehler z)")
            + " namespace Probe { public sealed class Zaehler { public int Plus(int x) => x + 1; } }");
        q.Should().Contain("[\"Zaehler\"] = typeof(global::Probe.Zaehler)");
        q.Should().Contain("new TechnikUmsetzung(typeof(global::Probe.Zaehler), \"Zaehler\", typeof(global::Probe.Zaehler), "
                           + "s => s.AddScoped<global::Probe.Zaehler>(sp => new global::Probe.Zaehler()))");
    }

    [Fact]
    public void Eine_Umsetzung_mit_Zustand_holt_ihn_aus_der_Aktivierung_unter_dem_Namen_ihrer_Funktion()
    {
        var q = Generiert(Bild.Replace("Mach(ISkalierer s, BildKonfig k)", "Mach(ISkalierer s, BildKonfig k, IZaehlwerk z)") + @" namespace Probe {
            public sealed class Zaehlstand { public int N { get; set; } }
            public interface IZaehlwerk { void Zaehle(); }
            public sealed class Zaehlwerk(IZustand<Zaehlstand> stand) : IZaehlwerk { public void Zaehle() => stand.Wert.N++; } }");
        q.Should().MatchRegex(@"private static readonly FunktionsZustandsform Zustand0 = new global::Infrastructure\.Funktionen\.FunktionsZustandsform\(typeof\(global::Probe\.Zaehlstand\)");
        q.Should().Contain("new global::Probe.Zaehlwerk((global::Abstractions.IZustand<global::Probe.Zaehlstand>)AktivierungsZustaende.Von(sp).Hole(\"IZaehlwerk\", Zustand0)!)),"
                           + " Zustand0),", "der Zustand gehört der Aktivierung des Halters, nie dem Container");
        q.IndexOf("Zustand0 =", StringComparison.Ordinal).Should().BeLessThan(q.IndexOf("Umsetzungen =", StringComparison.Ordinal),
            "die Form steht vor der Tabelle (statische Felder in Reihenfolge)");
    }

    [Fact]
    public void Gewaehlt_mit_Zustand_folgt_der_Konstruktor_Kette()
    {
        var mit = new TechnikUmsetzung(typeof(ID2), "D2a", typeof(D2a), _ => { }, new FunktionsZustandsform(typeof(K), () => new object(), _ => [], (_, _) => { }, _ => { }));
        var ohne = new TechnikUmsetzung(typeof(ID1), "D1a", typeof(D1a), _ => { });
        var wahl = new TechnikWahl(new Dictionary<Type, TechnikUmsetzung> { [typeof(ID1)] = ohne, [typeof(ID2)] = mit },
            new Dictionary<Type, Type[]> { [typeof(FImpl)] = [typeof(ID1)], [typeof(D1a)] = [typeof(ID2)] });
        wahl.MitZustand(typeof(FImpl)).Should().BeTrue("FImpl → ID1 (D1a) → ID2 (D2a mit Zustand)");
        wahl.MitZustand(typeof(D2a)).Should().BeFalse();
    }

    [Fact]
    public void Eine_nicht_oeffentliche_Implementierung_fehlt_in_der_Tabelle()
        => Generiert(Bild.Replace("public sealed class Skalierer", "internal sealed class Skalierer"))
            .Should().NotContain("typeof(global::Probe.Skalierer)", "die generierte Fabrik kann sie nicht bauen (CQRS092 meldet es im Build)");

    // ── Bindung beim Start ─────────────────────────────────────────────────────────────────────────────────────────────

    // Nur Schlüssel der Bindung — keine IFunktion: die Test-Assembly ist selbst eine Vertrags-Assembly, eine leere Funktion wäre ein Formfehler.
    private interface ID1;
    private interface ID2;
    private sealed record K(string Wert);
    private sealed class FImpl;
    private sealed class D1a;
    private sealed class D1b;
    private sealed class D2a;

    private sealed record Lauf(IServiceCollection S, List<string> Gebunden);

    private static Lauf Binde(Dictionary<string, string?> wahl, Dictionary<string, string?> konfig, Dictionary<Type, Type[]> braucht,
        bool zweiImpls = false)
    {
        var gebunden = new List<string>();
        var s = new ServiceCollection();
        var impls = new List<TechnikUmsetzung>
        {
            new(typeof(ID1), "D1a", typeof(D1a), _ => gebunden.Add("ID1 → D1a")),
            new(typeof(ID2), "D2a", typeof(D2a), _ => gebunden.Add("ID2 → D2a")),
        };
        if (zweiImpls) impls.Add(new(typeof(ID1), "D1b", typeof(D1b), _ => gebunden.Add("ID1 → D1b")));
        var konfigs = new[] { new KonfigRecord(typeof(K), "K", sec => { KonfigLeser.NurBekannte(sec, "Wert"); return new K(KonfigLeser.Text(sec, "Wert")); }) };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(konfig).Build();
        s.BindeTechnik(wahl, cfg.GetSection("Konfig"), [typeof(FImpl)], impls, braucht, konfigs);
        return new Lauf(s, gebunden);
    }

    [Fact]
    public void Gebunden_wird_transitiv_was_die_Funktion_braucht_samt_Konfig()
    {
        var l = Binde(new(), new() { ["Konfig:K:Wert"] = "x" },
            new() { [typeof(FImpl)] = [typeof(ID1), typeof(K)], [typeof(D1a)] = [typeof(ID2)] });
        l.Gebunden.Should().Equal("ID2 → D2a", "ID1 → D1a");
        l.S.Single(d => d.ServiceType == typeof(K)).ImplementationInstance.Should().Be(new K("x"));
    }

    [Fact]
    public void Nicht_Gebrauchtes_wird_nicht_gebunden()
        => Binde(new(), new(), new()).Gebunden.Should().BeEmpty();

    [Fact]
    public void Mehrdeutig_braucht_die_Wahl_der_Umgebung()
    {
        FluentActions.Invoking(() => Binde(new(), new(), new() { [typeof(FImpl)] = [typeof(ID1)] }, zweiImpls: true))
            .Should().Throw<BootFehler>().Where(e => e.Karte == "funktion:ID1").WithMessage("*mehrere Umsetzungen (D1a, D1b)*Funktionen:ID1:Implementierung*");
        Binde(new() { ["ID1"] = "D1b" }, new(), new() { [typeof(FImpl)] = [typeof(ID1)] }, zweiImpls: true)
            .Gebunden.Should().Equal("ID1 → D1b");
    }

    [Fact]
    public void Ein_unbekannter_Umsetzungsname_bricht_den_Start()
        => FluentActions.Invoking(() => Binde(new() { ["ID1"] = "Gibts" }, new(), new() { [typeof(FImpl)] = [typeof(ID1)] }))
            .Should().Throw<BootFehler>().WithMessage("Funktionen:ID1:Implementierung = 'Gibts' gibt es nicht (gefunden: D1a)*");

    [Fact]
    public void Ein_Kreis_ueber_Technik_bricht_den_Start()
        => FluentActions.Invoking(() => Binde(new(), new(),
                new() { [typeof(FImpl)] = [typeof(ID1)], [typeof(D1a)] = [typeof(ID2)], [typeof(D2a)] = [typeof(ID1)] }))
            .Should().Throw<BootFehler>().WithMessage("Technik im Kreis: ID1 → ID2 → ID1*");

    [Fact]
    public void Konfig_fehlt_hat_unbekannte_Felder_oder_unbekannte_Records()
    {
        var braucht = new Dictionary<Type, Type[]> { [typeof(FImpl)] = [typeof(K)] };
        FluentActions.Invoking(() => Binde(new(), new(), braucht)).Should().Throw<BootFehler>().WithMessage("Konfig:K fehlt*");
        FluentActions.Invoking(() => Binde(new(), new() { ["Konfig:K:Wert"] = "x", ["Konfig:K:Falsch"] = "y" }, braucht))
            .Should().Throw<BootFehler>().WithMessage("*unbekannte Felder Falsch*");
        FluentActions.Invoking(() => Binde(new(), new() { ["Konfig:Fremd:A"] = "x" }, new()))
            .Should().Throw<BootFehler>().WithMessage("Konfig:Fremd — kein Konfig-Record*");
    }

    [Fact]
    public void KonfigLeser_liest_streng()
    {
        var s = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["R:Zahl"] = "12", ["R:Dauer"] = "00:00:05", ["R:Wahl"] = "Prod", ["R:Liste:0"] = "a", ["R:Liste:1"] = "b", ["R:Kaputt"] = "zwölf",
        }).Build().GetSection("R");
        KonfigLeser.Zahl(s, "Zahl").Should().Be(12);
        KonfigLeser.Dauer(s, "Dauer").Should().Be(TimeSpan.FromSeconds(5));
        KonfigLeser.Wahl<UmgebungsArt>(s, "Wahl").Should().Be(UmgebungsArt.Prod);
        KonfigLeser.Texte(s, "Liste").Should().Equal("a", "b");
        FluentActions.Invoking(() => KonfigLeser.Zahl(s, "Kaputt")).Should().Throw<BootFehler>().WithMessage("R:Kaputt = 'zwölf' ist kein(e) ganze Zahl*");
        FluentActions.Invoking(() => KonfigLeser.Text(s, "Fehlt")).Should().Throw<BootFehler>().WithMessage("R:Fehlt fehlt*");
    }

    [Fact]
    public void Der_Bestand_hat_zwei_Bild_Techniken_und_den_Konfig_Record_BildaufbereitungKonfig()
    {
        GeneratedTechnik.Namen.Keys.Should().BeEquivalentTo("IImageResizer", "IHistogramEqualizer");
        GeneratedTechnik.Umsetzungen.Select(i => i.Name).Should().BeEquivalentTo("ImageResizer", "HistogramEqualizer");
        GeneratedTechnik.Konfigs.Select(k => k.Name).Should().Equal("BildaufbereitungKonfig");
    }

    // ── Analyzer CQRS089–CQRS092 ──────────────────────────────────────────────────────────────────────────────────────

    private static async Task<string[]> Befunde(string code)
        => (await Probe(code).WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TechnikAnalyzer())).GetAnalyzerDiagnosticsAsync())
            .Select(d => d.Id).ToArray();

    [Fact]
    public async Task Technik_im_Konstruktor_einer_Umsetzung_ist_still()
        => (await Befunde(Bild)).Should().BeEmpty();

    [Theory]
    [InlineData("public sealed class Reaktion { public Task Handle(Gemacht e, ISkalierer s) => Task.CompletedTask; }")]   // Handle-Parameter
    [InlineData("public sealed class Projektion(ISkalierer s) { public int X => s.Skaliere(1); }")]                       // Konstruktor im Fachcode
    [InlineData("public sealed class Halter { public ISkalierer? S; }")]                                                   // Feld
    [InlineData("public sealed class Halter2 { public System.Func<ISkalierer>? Fabrik { get; set; } }")]                              // im Typ-Argument
    public async Task Technik_ausserhalb_eines_Konstruktors_ist_CQRS089(string code)
        => (await Befunde(Bild + " namespace Probe { " + code + " }")).Should().Contain(TechnikAnalyzer.OrtId);

    [Fact]
    public async Task Ein_Konstruktor_Parameter_den_die_Umgebung_nicht_bindet_ist_CQRS090()
        => (await Befunde(Bild.Replace("Mach(ISkalierer s, BildKonfig k)", "Mach(ISkalierer s, BildKonfig k, System.Net.Http.HttpClient h)")))
            .Should().Equal(TechnikAnalyzer.CtorId);

    [Fact]
    public async Task Eine_Funktion_mit_Eingaengen_im_Konstruktor_ist_CQRS090()
        => (await Befunde(Bild.Replace("Skalierer(ILogger<Skalierer> log)", "Skalierer(ILogger<Skalierer> log, IMach m)")))
            .Should().Equal(TechnikAnalyzer.CtorId);

    [Fact]
    public async Task Ein_Konfig_Feld_das_die_Konfig_nicht_lesen_kann_ist_CQRS091()
        => (await Befunde(Bild.Replace("bool Laut = false", "System.Uri? Laut = null"))).Should().Equal(TechnikAnalyzer.KonfigFeldId);

    [Fact]
    public async Task Eine_nicht_oeffentliche_Implementierung_ist_CQRS092()
        => (await Befunde(Bild.Replace("public sealed class Skalierer", "internal sealed class Skalierer"))).Should().Equal(TechnikAnalyzer.BaubarId);
}
