using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// R19 (CQRS105, docs/04 §4.4): Zustand einer Funktion nur über <c>IZustand&lt;T&gt;</c> im Konstruktor einer Funktions-Klasse —
/// kein veränderliches Feld, keine Eigenschaft mit Setter, keine Sammlung hinter <c>readonly</c>; <c>IZustand&lt;T&gt;</c> nirgends sonst. Schließt
/// die Lücke „gleichbleibende Funktion mit Feldern“ (sie rechnete beim Wiederholen anders).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ZustandAnalyzerTests
{
    private const string Vorspann = """
        using System; using System.Collections.Generic; using System.Text; using System.Threading.Tasks; using Abstractions;
        namespace Probe {
        public sealed record Gemessen(double Wert) : IEvent;
        public sealed record Auffaellig(double Wert) : ITransientEvent;
        public sealed class Modell { public double Summe { get; set; } public int Anzahl { get; set; } }
        public interface IVerbindung : IDienst { Task<int> ZaehleAsync(); }
        """;

    private static async Task<(string Id, string Text)[]> Befunde(string code)
    {
        _ = typeof(IZustand<>);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Pipeline", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ZustandAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => (d.Id, d.GetMessage())).ToArray();
    }

    [Fact]
    public async Task Zustand_ueber_IZustand_und_Dienste_im_Konstruktor_sind_erlaubt()
    {
        (await Befunde("""
            public sealed class Lerner(IZustand<Modell> modell, IVerbindung db) : IFunktion
            {
                private readonly IVerbindung _db = db;
                private const int Grenze = 3;
                public void Auf(Gemessen e) { modell.Wert.Summe += e.Wert; modell.Wert.Anzahl++; }
                public Auffaellig? Auf(Auffaellig a) => modell.Wert.Anzahl > Grenze ? a : null;
            }
            """)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("private int _zaehler;", "veränderlich")]
    [InlineData("private static int _alle;", "statisch")]
    [InlineData("private readonly List<double> _werte = new();", "Sammlung")]
    [InlineData("private readonly double[] _puffer = new double[8];", "Sammlung")]
    [InlineData("private readonly StringBuilder _text = new();", "Sammlung")]
    [InlineData("public int Gesehen { get; set; }", "Setter")]
    public async Task Zustand_in_Feldern_oder_Eigenschaften_einer_Funktion_ist_CQRS105(string member, string worum)
    {
        var befunde = await Befunde("public sealed class Zaehler : IFunktion { " + member + " public Auffaellig? Auf(Gemessen e) => null; }");
        befunde.Should().ContainSingle(b => b.Id == ZustandAnalyzer.Id).Which.Text.Should().Contain(worum);
    }

    [Fact]
    public async Task Auch_die_Umsetzung_einer_Funktion_draussen_haelt_keinen_Zustand_und_nimmt_kein_IZustand()
    {
        var befunde = await Befunde("""
            public interface IPruefe : IFunktion { Auffaellig? Auf(Gemessen e); }
            public sealed class Pruefe(IZustand<Modell> m) : IPruefe { private int _n; public Auffaellig? Auf(Gemessen e) => null; }
            """);
        befunde.Count(b => b.Id == ZustandAnalyzer.Id).Should().Be(2, "das Feld und der Zustand in der Umsetzung (draußen lebt er im Worker)");
    }

    [Fact]
    public async Task IZustand_ausserhalb_einer_Funktion_ist_CQRS105()
    {
        var befunde = await Befunde("""
            public sealed class Verbindung(IZustand<Modell> m) : IVerbindung { public Task<int> ZaehleAsync() => Task.FromResult(0); }
            """);
        befunde.Should().ContainSingle(b => b.Id == ZustandAnalyzer.Id).Which.Text.Should().Contain("keine Funktion");
    }
}
