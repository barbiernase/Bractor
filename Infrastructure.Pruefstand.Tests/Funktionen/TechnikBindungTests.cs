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
        public sealed record Gemacht(string X) : ICommand;
        public sealed record Angefordert(string X) : ICommand;
        public interface ISkalierer { int Skaliere(int x); }
        public sealed class Skalierer(ILogger<Skalierer> log) : ISkalierer { public int Skaliere(int x) => x; }
        public enum Modus { Schnell = 1, Gruendlich = 2 }
        public record BildKonfig(string Ausgabe, int Hoehe, Modus Modus, TimeSpan Frist, bool Laut = false, string[]? Endungen = null);
        public sealed class Mach(ISkalierer s, BildKonfig k) : IFunktion { public Task<Gemacht> Auf(Angefordert e) => null!; }
    }";

    [Fact]
    public void Technik_Umsetzung_Bedarf_und_Konfig_Record_werden_aus_dem_Code_gelesen()
    {
        var q = Generiert(Bild);
        q.Should().Contain("[\"ISkalierer\"] = typeof(global::Probe.ISkalierer)");
        q.Should().Contain("new TechnikUmsetzung(typeof(global::Probe.ISkalierer), \"Skalierer\", typeof(global::Probe.Skalierer), "
                           + "sp => new global::Probe.Skalierer(");
        q.Should().Contain("GetRequiredService<global::Microsoft.Extensions.Logging.ILogger<global::Probe.Skalierer>>(sp)", "die Fabrik ruft den Konstruktor direkt");
        q.Should().Contain("[typeof(global::Probe.Mach)] = new Type[] { typeof(global::Probe.ISkalierer), typeof(global::Probe.BildKonfig) }");
        q.Should().MatchRegex(@"new KonfigRecord\(typeof\(global::Probe\.BildKonfig\), ""BildKonfig"", new\[\] \{ ""Ausgabe"", ""Hoehe"", ""Modus"", ""Frist"", ""Laut"", ""Endungen"" \}, Konfig\d+\)");
        q.Should().Contain("KonfigLeser.NurBekannte(s, \"Ausgabe\", \"Hoehe\", \"Modus\", \"Frist\", \"Laut\", \"Endungen\")");
        q.Should().Contain("KonfigLeser.Text(s, \"Ausgabe\"), KonfigLeser.Zahl(s, \"Hoehe\"), KonfigLeser.Wahl<global::Probe.Modus>(s, \"Modus\"), KonfigLeser.Dauer(s, \"Frist\")");
        q.Should().Contain("(KonfigLeser.Hat(s, \"Laut\") ? KonfigLeser.JaNein(s, \"Laut\") : false)", "ein Default gilt, wenn das Feld fehlt");
        q.Should().Contain("(KonfigLeser.Hat(s, \"Endungen\") ? KonfigLeser.Texte(s, \"Endungen\") : default!)");
    }

    [Fact]
    public void Eine_Funktion_ist_keine_Technik()
        => Generiert(Bild).Should().NotContain("[\"Mach\"]", "sie ruft das Framework — ihre Bindung steht in GeneratedFunktionsBausteine");

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
                           + "sp => new global::Probe.Zaehler()),");
    }

    [Fact]
    public void Eine_Umsetzung_mit_Zustand_holt_ihn_aus_der_Aktivierung_unter_dem_Namen_ihrer_Funktion()
    {
        var q = Generiert(Bild.Replace("Mach(ISkalierer s, BildKonfig k)", "Mach(ISkalierer s, BildKonfig k, IZaehlwerk z)") + @" namespace Probe {
            public sealed class Zaehlstand { public int N { get; set; } }
            public interface IZaehlwerk { void Zaehle(); }
            public sealed class Zaehlwerk(IZustand<Zaehlstand> stand) : IZaehlwerk { public void Zaehle() => stand.Wert.N++; } }");
        q.Should().MatchRegex(@"private static readonly FunktionsZustandsform Zustand0 = new global::Infrastructure\.Funktionen\.FunktionsZustandsform\(typeof\(global::Probe\.Zaehlstand\)");
        q.Should().Contain("new global::Probe.Zaehlwerk((global::Abstractions.IZustand<global::Probe.Zaehlstand>)AktivierungsZustaende.Von(sp).Hole(\"IZaehlwerk\", Zustand0)!),"
                           + " Zustand0),", "der Zustand gehört der Aktivierung des Halters, nie dem Container");
        q.IndexOf("Zustand0 =", StringComparison.Ordinal).Should().BeLessThan(q.IndexOf("Umsetzungen =", StringComparison.Ordinal),
            "die Form steht vor der Tabelle (statische Felder in Reihenfolge)");
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
    private sealed class D1a : ID1;
    private sealed class D1b : ID1;
    private sealed class D2a : ID2;

    private sealed record Lauf(IServiceCollection S, List<string> Gebaut, KnotenKontext Knoten);

    /// <summary>Bindet für einen Knoten (Umsetzung <see cref="FImpl"/>) mit seiner Technik-Wahl und seinen Werten.</summary>
    private static Lauf Binde(Dictionary<string, string> wahl, Dictionary<string, string?> werte, Dictionary<Type, Type[]> braucht,
        bool zweiImpls = false, bool d2MitZustand = false)
    {
        var gebaut = new List<string>();
        var s = new ServiceCollection();
        var zustand = new FunktionsZustandsform(typeof(K), () => new object(), _ => [], (_, _) => { }, _ => { });
        var impls = new List<TechnikUmsetzung>
        {
            new(typeof(ID1), "D1a", typeof(D1a), _ => { gebaut.Add("ID1 → D1a"); return new D1a(); }),
            new(typeof(ID2), "D2a", typeof(D2a), _ => { gebaut.Add("ID2 → D2a"); return new D2a(); }, d2MitZustand ? zustand : null),
        };
        if (zweiImpls) impls.Add(new(typeof(ID1), "D1b", typeof(D1b), _ => { gebaut.Add("ID1 → D1b"); return new D1b(); }));
        var konfigs = new[] { new KonfigRecord(typeof(K), "K", ["Wert"], sec => { KonfigLeser.NurBekannte(sec, "Wert"); return new K(KonfigLeser.Text(sec, "Wert")); }) };
        var knoten = new KnotenKontext("i", "k", new KnotenKonfig { Technik = wahl, Werte = werte });
        s.BindeTechnik([(knoten, typeof(FImpl))], impls, braucht, konfigs);
        return new Lauf(s, gebaut, knoten);
    }

    /// <summary>Baut <typeparamref name="T"/> im Bereich des Knotens (wie eine Aktivierung).</summary>
    private static object Baue<T>(Lauf l)
    {
        using var sp = l.S.BuildServiceProvider();
        using var bereich = sp.CreateScope();
        bereich.ServiceProvider.GetRequiredService<KnotenBereich>().Kontext = l.Knoten;
        return bereich.ServiceProvider.GetRequiredService(typeof(T));
    }

    [Fact]
    public void Gebunden_wird_transitiv_was_die_Funktion_braucht_samt_Konfig_aus_den_Werten_des_Knotens()
    {
        var l = Binde(new(), new() { ["Wert"] = "x" },
            new() { [typeof(FImpl)] = [typeof(ID1), typeof(K)], [typeof(D1a)] = [typeof(ID2)] });
        l.S.Should().Contain(d => d.ServiceType == typeof(ID1)).And.Contain(d => d.ServiceType == typeof(ID2));
        Baue<K>(l).Should().Be(new K("x"), "der Konfig-Record kommt aus den Werten des Knotens");
        Baue<ID1>(l).Should().BeOfType<D1a>();
        l.Gebaut.Should().Equal("ID1 → D1a");
    }

    [Fact]
    public void Nicht_Gebrauchtes_wird_nicht_gebunden()
        => Binde(new(), new(), new()).S.Should().NotContain(d => d.ServiceType == typeof(ID1) || d.ServiceType == typeof(K));

    [Fact]
    public void Mehrdeutig_braucht_die_Wahl_des_Knotens()
    {
        FluentActions.Invoking(() => Binde(new(), new(), new() { [typeof(FImpl)] = [typeof(ID1)] }, zweiImpls: true))
            .Should().Throw<BootFehler>().Where(e => e.Karte == "funktion:ID1").WithMessage("i/k: *mehrere Umsetzungen (D1a, D1b)*Technik:ID1*");
        Baue<ID1>(Binde(new() { ["ID1"] = "D1b" }, new(), new() { [typeof(FImpl)] = [typeof(ID1)] }, zweiImpls: true)).Should().BeOfType<D1b>();
    }

    [Fact]
    public void Eine_unbekannte_Umsetzung_oder_Technik_bricht_den_Start()
    {
        FluentActions.Invoking(() => Binde(new() { ["ID1"] = "Gibts" }, new(), new() { [typeof(FImpl)] = [typeof(ID1)] }))
            .Should().Throw<BootFehler>().WithMessage("i/k: Technik:ID1 = 'Gibts' gibt es nicht (gefunden: D1a)*");
        FluentActions.Invoking(() => Binde(new() { ["IFremd"] = "X" }, new(), new() { [typeof(FImpl)] = [typeof(ID1)] }))
            .Should().Throw<BootFehler>().WithMessage("i/k: Technik:IFremd — keine Technik dieses Namens*");
    }

    [Fact]
    public void Ein_Kreis_ueber_Technik_bricht_den_Start()
        => FluentActions.Invoking(() => Binde(new(), new(),
                new() { [typeof(FImpl)] = [typeof(ID1)], [typeof(D1a)] = [typeof(ID2)], [typeof(D2a)] = [typeof(ID1)] }))
            .Should().Throw<BootFehler>().WithMessage("Technik im Kreis: ID1 → ID2 → ID1*");

    [Fact]
    public void Werte_fehlen_oder_sind_unbekannt()
    {
        var braucht = new Dictionary<Type, Type[]> { [typeof(FImpl)] = [typeof(K)] };
        FluentActions.Invoking(() => Binde(new(), new(), braucht)).Should().Throw<BootFehler>().WithMessage("i/k:Wert fehlt*");
        FluentActions.Invoking(() => Binde(new(), new() { ["Wert"] = "x", ["Falsch"] = "y" }, braucht))
            .Should().Throw<BootFehler>().WithMessage("i/k: unbekannte Felder Falsch*");
    }

    [Fact]
    public void Gewaehlt_mit_Zustand_folgt_der_Konstruktor_Kette_je_Knoten()
    {
        var mit = Binde(new(), new(), new() { [typeof(FImpl)] = [typeof(ID1)], [typeof(D1a)] = [typeof(ID2)] }, d2MitZustand: true);
        using var sp = mit.S.BuildServiceProvider();
        sp.GetRequiredService<TechnikWahl>().MitZustand("i/k").Should().BeTrue("FImpl → ID1 (D1a) → ID2 (D2a mit Zustand)");
        var ohne = Binde(new(), new(), new() { [typeof(FImpl)] = [typeof(ID1)] }, d2MitZustand: true);
        using var sp2 = ohne.S.BuildServiceProvider();
        sp2.GetRequiredService<TechnikWahl>().MitZustand("i/k").Should().BeFalse();
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
    public void Der_Bestand_hat_zwei_Bild_Techniken_und_die_Konfig_Records_der_Bildaufbereitung_und_des_Katalogs()
    {
        // Die Katalogfunktionen (Core/Katalog) sind Bausteine wie die der Domäne: ihre Konfig-Records gehören dazu, ob eine Instanz sie nennt oder nicht.
        GeneratedTechnik.Namen.Keys.Should().BeEquivalentTo("IImageResizer", "IHistogramEqualizer");
        GeneratedTechnik.Umsetzungen.Select(i => i.Name).Should().BeEquivalentTo("ImageResizer", "HistogramEqualizer");
        GeneratedTechnik.Konfigs.Select(k => k.Name).Should().BeEquivalentTo("BildaufbereitungKonfig", "Dateiwaechterkonfig", "Taktkonfig", "Webhookkonfig");
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
        => (await Befunde(Bild.Replace("Skalierer(ILogger<Skalierer> log)", "Skalierer(ILogger<Skalierer> log, Mach m)")))
            .Should().Equal(TechnikAnalyzer.CtorId);

    [Fact]
    public async Task Ein_Konfig_Feld_das_die_Konfig_nicht_lesen_kann_ist_CQRS091()
        => (await Befunde(Bild.Replace("bool Laut = false", "System.Uri? Laut = null"))).Should().Equal(TechnikAnalyzer.KonfigFeldId);

    [Fact]
    public async Task Eine_oeffentliche_generische_Funktion_ist_baubar_eine_nicht_oeffentliche_ist_CQRS092()
    {
        // Eine generische Funktion ist eine Blaupause: der Generator baut sie je Typargument eines Knotens (Entscheidung 4).
        string Mit(string sichtbar) => Bild.Replace("public enum Modus", "public sealed record Los() : ICommand; public sealed record Fertig() : ICommand; "
            + sichtbar + " sealed class Glaettung<T>(ISkalierer s) : IFunktion { public Task<Fertig> Auf(Los l) => null!; } public enum Modus");
        (await Befunde(Mit("public"))).Should().NotContain(TechnikAnalyzer.BaubarId);
        (await Befunde(Mit("internal"))).Should().Contain(TechnikAnalyzer.BaubarId);
    }

    [Fact]
    public async Task Eine_nicht_oeffentliche_Implementierung_ist_CQRS092()
        => (await Befunde(Bild.Replace("public sealed class Skalierer", "internal sealed class Skalierer"))).Should().Equal(TechnikAnalyzer.BaubarId);
}
