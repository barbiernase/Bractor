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
/// CQRS119/CQRS121 (GR-UEBERSETZUNG): eine Übersetzung ist eine Klasse <c>{TSource}_{TTarget}_{n}(Parameter der Quelle) : ITranslate&lt;TSource, TTarget&gt;</c>
/// (docs/04 §4.4) — ihr Konstruktor nimmt genau die Quelle, <c>Translate()</c> baut das Ziel aus ihren Feldern, der Compiler prüft die Zuordnung.
/// Sie nimmt einen Command ohne Ziel oder ein Event (eine Antwort) und erzeugt einen Command; ein Graph nennt sie nie als Knoten (CQRS115).
/// </summary>
public class UebersetzungAnalyzerTests
{
    private static async Task<(string Id, string Text)[]> Befunde(string code)
    {
        _ = typeof(ITranslate<,>);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GraphAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => (d.Id, d.GetMessage())).ToArray();
    }

    private const string Vorspann = """
        using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions;
        namespace Probe {
        public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
        public record Buche(Id<Konto> AggregateId, decimal Betrag, string Text = "") : ICommand<Konto>;
        public record Gebucht(decimal Betrag) : IEvent;
        public record Anfrage(Id<Konto> Konto, int Betrag) : ICommand;
        public record Rechne(Guid Bezug, int Betrag, int Faktor = 2) : ICommand;
        public record Gerechnet(Guid Bezug, int Betrag) : ICommand;
        public record Bestaetigt(decimal Betrag) : ICommand;
        public record Fremd(string Betrag) : ICommand;
        public sealed class Rechner : IFunktion { public Task<Gerechnet> Auf(Rechne r) => null!; }
        """;

    [Fact]
    public async Task Konstruktor_mit_der_Quelle_und_Translate_ergeben_eine_vollstaendige_Zuordnung_in_beide_Richtungen()
        => (await Befunde("""
            public sealed class Anfrage_Rechne_1(Anfrage source) : ITranslate<Anfrage, Rechne> { public Rechne Translate() => new(Bezug: source.Konto.Wert, Betrag: source.Betrag); }
            public sealed class Gerechnet_Buche_1(Gerechnet source) : ITranslate<Gerechnet, Buche> { public Buche Translate() => new(AggregateId: new(source.Bezug), Betrag: source.Betrag); }
            public sealed class Gebucht_Bestaetigt_1(Gebucht source) : ITranslate<Gebucht, Bestaetigt> { public Bestaetigt Translate() => new(source.Betrag); }
            """)).Should().BeEmpty("der Konstruktor nimmt die Quelle, Translate baut das Ziel (Faktor und Text aus dem Standard); eine "
            + "Antwort-Übersetzung nimmt ein Event");

    [Fact]
    public async Task Ein_Konstruktor_ohne_genau_die_Quelle_ist_CQRS119()
    {
        var befunde = await Befunde("""
            public sealed class Gerechnet_Buche_1(Guid Bezug, int Betrag) : ITranslate<Gerechnet, Buche> { public Buche Translate() => new(new(Bezug), Betrag); }
            public sealed class Gebucht_Bestaetigt_1(Gerechnet source) : ITranslate<Gebucht, Bestaetigt> { public Bestaetigt Translate() => new(source.Betrag); }
            """);
        befunde.Select(b => b.Id).Should().Equal("CQRS119", "CQRS119");
        befunde.Should().Contain(b => b.Text.Contains("'Gerechnet_Buche_1': der Konstruktor nimmt genau die Quelle — (Gerechnet source), nicht (System.Guid Bezug, int Betrag)"));
        befunde.Should().Contain(b => b.Text.Contains("'Gebucht_Bestaetigt_1': der Konstruktor nimmt genau die Quelle — (Gebucht source)"));
    }

    [Fact]
    public async Task Eine_Uebersetzung_nimmt_einen_Command_ohne_Ziel_oder_ein_Event_und_erzeugt_einen_Command_sonst_CQRS121()
    {
        var befunde = await Befunde("""
            public record Notiz(int Betrag) : ITransientEvent;
            public sealed class Buche_Rechne_1(Buche source) : ITranslate<Buche, Rechne> { public Rechne Translate() => new(Guid.Empty, 0); }
            public sealed class Gerechnet_Notiz_1(Gerechnet source) : ITranslate<Gerechnet, Notiz> { public Notiz Translate() => new(source.Betrag); }
            public sealed class Notiz_Rechne_1(Notiz source) : ITranslate<Notiz, Rechne> { public Rechne Translate() => new(Guid.Empty, source.Betrag); }
            """);
        befunde.Where(b => b.Id == "CQRS121").Should().HaveCount(3, "ein Command an ein Aggregat, ein Hinweis als Ergebnis, ein Hinweis als Eingang");
    }

    [Fact]
    public async Task Ein_Graph_nennt_keine_Uebersetzung_als_Knoten()
    {
        var befunde = await Befunde("""
            public sealed class Gerechnet_Buche_1(Gerechnet source) : ITranslate<Gerechnet, Buche> { public Buche Translate() => new(new(source.Bezug), source.Betrag); }
            public sealed record Ablauf([Kopf] Rechner R, [Von(nameof(R))] Gerechnet_Buche_1 H) : IGraph;
            """);
        befunde.Should().Contain(b => b.Id == GraphAnalyzer.Id && b.Text.Contains("ist eine Übersetzung") && b.Text.Contains("lebt in der Domäne"));
    }
}
