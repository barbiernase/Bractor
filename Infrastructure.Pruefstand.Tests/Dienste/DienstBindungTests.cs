using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.SourceGeneration;
using FluentAssertions;
using Infrastructure.Dienste;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Dienste;

/// <summary>
/// Die Dienst-Bindung (konzept-werkbank §12.3 E11): der <c>DienstGenerator</c> liest Dienste, Implementierungen und Konfig-Records nur aus Code-Fakten; die
/// Bindung beim Start wählt nach der Umgebung und bricht laut bei fehlender, mehrdeutiger oder zyklischer Implementierung bzw. fehlender
/// Konfig; der Analyzer (CQRS089–CQRS092) hält die Form im Build.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class DienstBindungTests
{
    // ── Generator ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly ImmutableArray<MetadataReference> Refs = Referenzen();
    private static ImmutableArray<MetadataReference> Referenzen()
    {
        var dazu = new[] { typeof(IDienst).Assembly.Location, typeof(Microsoft.Extensions.Logging.ILogger<>).Assembly.Location };
        return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => a.Location)
            .Concat(dazu).Distinct().Select(l => (MetadataReference)MetadataReference.CreateFromFile(l)).ToImmutableArray();
    }

    private const string Vorspann = "using System; using System.Threading.Tasks; using Abstractions; using Microsoft.Extensions.Logging;\n";

    private static CSharpCompilation Probe(string code)
    {
        var comp = CSharpCompilation.Create("DienstProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code) }, Refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return comp;
    }

    private static string Generiert(string code)
    {
        CSharpGeneratorDriver.Create(new Infrastructure.SourceGeneration.DienstGenerator()).RunGeneratorsAndUpdateCompilation(Probe(code), out var neu, out _);
        return string.Join("\n", neu.SyntaxTrees.Skip(1).Select(t => t.ToString()));
    }

    private const string Bild = @"namespace Probe {
        public sealed record Gemacht(string X) : IEvent;
        public sealed record Angefordert(string X) : IEvent;
        public interface IMach : IFunktion { Gemacht Auf(Angefordert e); }
        public interface ISkalierer : IDienst { int Skaliere(int x); }
        public sealed class Skalierer(ILogger<Skalierer> log) : ISkalierer { public int Skaliere(int x) => x; }
        public enum Modus { Schnell = 1, Gruendlich = 2 }
        public record BildKonfig(string Ausgabe, int Hoehe, Modus Modus, TimeSpan Frist, bool Laut = false, string[]? Endungen = null);
        public sealed class Mach(ISkalierer s, BildKonfig k) : IMach { public Gemacht Auf(Angefordert e) => default!; }
    }";

    [Fact]
    public void Dienst_Implementierung_Bedarf_und_Konfig_Record_werden_aus_dem_Code_gelesen()
    {
        var q = Generiert(Bild);
        q.Should().Contain("[\"ISkalierer\"] = typeof(global::Probe.ISkalierer)");
        q.Should().Contain("new DienstImplementierung(typeof(global::Probe.ISkalierer), \"Skalierer\", typeof(global::Probe.Skalierer), "
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
    public void Eine_nicht_oeffentliche_Implementierung_fehlt_in_der_Tabelle()
        => Generiert(Bild.Replace("public sealed class Skalierer", "internal sealed class Skalierer"))
            .Should().NotContain("typeof(global::Probe.Skalierer)", "die generierte Fabrik kann sie nicht bauen (CQRS092 meldet es im Build)");

    // ── Bindung beim Start ─────────────────────────────────────────────────────────────────────────────────────────────

    private interface ID1 : IDienst;
    private interface ID2 : IDienst;
    private sealed record K(string Wert);
    private sealed class FImpl;
    private sealed class D1a;
    private sealed class D1b;
    private sealed class D2a;

    private sealed record Lauf(IServiceCollection S, List<string> Gebunden);

    private static Lauf Binde(Dictionary<string, DienstKonfig> wahl, Dictionary<string, string?> konfig, Dictionary<Type, Type[]> braucht,
        bool zweiImpls = false)
    {
        var gebunden = new List<string>();
        var s = new ServiceCollection();
        var namen = new Dictionary<string, Type> { ["ID1"] = typeof(ID1), ["ID2"] = typeof(ID2) };
        var impls = new List<DienstImplementierung>
        {
            new(typeof(ID1), "D1a", typeof(D1a), _ => gebunden.Add("ID1 → D1a")),
            new(typeof(ID2), "D2a", typeof(D2a), _ => gebunden.Add("ID2 → D2a")),
        };
        if (zweiImpls) impls.Add(new(typeof(ID1), "D1b", typeof(D1b), _ => gebunden.Add("ID1 → D1b")));
        var konfigs = new[] { new KonfigRecord(typeof(K), "K", sec => { KonfigLeser.NurBekannte(sec, "Wert"); return new K(KonfigLeser.Text(sec, "Wert")); }) };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(konfig).Build();
        s.BindeDienste(wahl, cfg.GetSection("Konfig"), [typeof(FImpl)], namen, impls, braucht, konfigs);
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
            .Should().Throw<BootFehler>().Where(e => e.Karte == "dienst:ID1").WithMessage("*mehrere Implementierungen (D1a, D1b)*Dienste:ID1:Implementierung*");
        Binde(new() { ["ID1"] = new() { Implementierung = "D1b" } }, new(), new() { [typeof(FImpl)] = [typeof(ID1)] }, zweiImpls: true)
            .Gebunden.Should().Equal("ID1 → D1b");
    }

    [Fact]
    public void Fehlende_Implementierung_unbekannter_Name_und_unbekannter_Dienst_brechen_den_Start()
    {
        var ohne = new Dictionary<Type, Type[]> { [typeof(FImpl)] = [typeof(ID1)] };
        FluentActions.Invoking(() => Binde(new() { ["ID1"] = new() { Implementierung = "Gibts" } }, new(), ohne))
            .Should().Throw<BootFehler>().WithMessage("*Implementierung = 'Gibts' gibt es nicht (gefunden: D1a)*");
        FluentActions.Invoking(() => Binde(new() { ["IUnbekannt"] = new() }, new(), ohne))
            .Should().Throw<BootFehler>().Where(e => e.Karte == "host:Dienste").WithMessage("*Dienste:IUnbekannt*");
    }

    [Fact]
    public void Ein_Kreis_ueber_Dienste_bricht_den_Start()
        => FluentActions.Invoking(() => Binde(new(), new(),
                new() { [typeof(FImpl)] = [typeof(ID1)], [typeof(D1a)] = [typeof(ID2)], [typeof(D2a)] = [typeof(ID1)] }))
            .Should().Throw<BootFehler>().WithMessage("Dienste im Kreis: ID1 → ID2 → ID1*");

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
    public void Der_Bestand_hat_zwei_Bild_Dienste_und_den_Konfig_Record_BildaufbereitungKonfig()
    {
        GeneratedDienste.Namen.Keys.Should().BeEquivalentTo("IImageResizer", "IHistogramEqualizer");
        GeneratedDienste.Implementierungen.Select(i => i.Name).Should().BeEquivalentTo("ImageResizer", "HistogramEqualizer");
        GeneratedDienste.Konfigs.Select(k => k.Name).Should().Equal("BildaufbereitungKonfig");
    }

    // ── Analyzer CQRS089–CQRS092 ──────────────────────────────────────────────────────────────────────────────────────

    private static async Task<string[]> Befunde(string code)
        => (await Probe(code).WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DienstAnalyzer())).GetAnalyzerDiagnosticsAsync())
            .Select(d => d.Id).ToArray();

    [Fact]
    public async Task Der_Dienst_im_Konstruktor_einer_Implementierung_ist_still()
        => (await Befunde(Bild)).Should().BeEmpty();

    [Theory]
    [InlineData("public sealed class Reaktion { public Task Handle(Gemacht e, ISkalierer s) => Task.CompletedTask; }")]   // Handle-Parameter
    [InlineData("public sealed class Projektion(ISkalierer s) { public int X => s.Skaliere(1); }")]                       // Konstruktor im Fachcode
    [InlineData("public sealed class Halter { public ISkalierer? S; }")]                                                   // Feld
    [InlineData("public sealed class Halter2 { public System.Func<ISkalierer>? Fabrik { get; set; } }")]                              // im Typ-Argument
    public async Task Ein_Dienst_ausserhalb_einer_Implementierung_ist_CQRS089(string code)
        => (await Befunde(Bild + " namespace Probe { " + code + " }")).Should().Contain(DienstAnalyzer.OrtId);

    [Fact]
    public async Task Ein_Konstruktor_Parameter_den_die_Umgebung_nicht_bindet_ist_CQRS090()
        => (await Befunde(Bild.Replace("Mach(ISkalierer s, BildKonfig k)", "Mach(ISkalierer s, BildKonfig k, System.Net.Http.HttpClient h)")))
            .Should().Equal(DienstAnalyzer.CtorId);

    [Fact]
    public async Task Ein_Konfig_Feld_das_die_Konfig_nicht_lesen_kann_ist_CQRS091()
        => (await Befunde(Bild.Replace("bool Laut = false", "System.Uri? Laut = null"))).Should().Equal(DienstAnalyzer.KonfigFeldId);

    [Fact]
    public async Task Eine_nicht_oeffentliche_Implementierung_ist_CQRS092()
        => (await Befunde(Bild.Replace("public sealed class Skalierer", "internal sealed class Skalierer"))).Should().Equal(DienstAnalyzer.BaubarId);
}
