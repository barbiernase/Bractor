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
/// CQRS119/CQRS121 (GR-UEBERSETZUNG) und die Graph-Regeln einer Übersetzung (CQRS115): die Zuordnung nach der einen Lesart (<c>Uebersetzungsform</c>)
/// — <c>[Feld]</c>, gleicher Name mit implizit zuweisbarem Typ, Standardwert, sonst ein Fehler; <c>Guid</c> ⇄ <c>Id&lt;T&gt;</c> nur ausdrücklich; nie zwei
/// Übersetzungen hintereinander; eine Übersetzung am Kopf gibt an Knoten oder ein Aggregat.
/// </summary>
public class UebersetzungAnalyzerTests
{
    private static async Task<(string Id, string Text)[]> Befunde(string code)
    {
        _ = typeof(Uebersetze<,>);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Pipeline", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
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
        public record Anfrage(Id<Konto> Konto, int Betrag) : ICommand;
        public record Rechne(Guid Bezug, int Betrag, int Faktor = 2) : ICommand;
        public record Gerechnet(Guid Bezug, int Betrag) : ICommand;
        public record Fremd(string Betrag) : ICommand;
        public sealed class Rechner : IFunktion { public Gerechnet Auf(Rechne r) => default!; }
        """;

    [Fact]
    public async Task Feld_Name_und_Standardwert_ergeben_eine_vollstaendige_Zuordnung_in_beide_Richtungen()
        => (await Befunde("""
            public sealed record Ablauf(
                [Kopf, Feld(nameof(Rechne.Bezug), nameof(Anfrage.Konto))] Uebersetze<Anfrage, Rechne> Hinein,
                [Von(nameof(Hinein))] Rechner R,
                [Von(nameof(R)), Feld(nameof(Buche.AggregateId), nameof(Gerechnet.Bezug))] Uebersetze<Gerechnet, Buche> Hinaus) : IGraph;
            """)).Should().BeEmpty("Bezug ⇄ Konto ausdrücklich (Id ⇄ Guid), Betrag nach Namen (int → int bzw. int → decimal erweitert), Faktor und Text aus dem Standard");

    [Fact]
    public async Task Ein_Parameter_ohne_Quelle_ist_CQRS119()
    {
        var befunde = await Befunde("""
            public sealed record Ablauf([Kopf] Rechner R, [Von(nameof(R))] Uebersetze<Gerechnet, Buche> Hinaus) : IGraph;
            """);
        befunde.Should().ContainSingle().Which.Id.Should().Be("CQRS119");
        befunde[0].Text.Should().Contain("Buche.AggregateId").And.Contain("Gerechnet hat kein Feld AggregateId");
    }

    [Fact]
    public async Task Guid_wird_nie_still_zu_einer_Id_und_ein_fremder_Typ_nie_zugeordnet()
    {
        var befunde = await Befunde("""
            public record MitId(Guid AggregateId, decimal Betrag) : ICommand;
            public record ZielText(Guid Bezug, int Betrag) : ICommand;
            public sealed class Gibt : IFunktion { public MitId Auf(Rechne r) => default!; }
            public sealed class Leser : IFunktion { public Fremd Auf(Rechne r) => default!; }
            public sealed record Ablauf([Kopf] Gibt G, [Von(nameof(G))] Uebersetze<MitId, Buche> Hinaus) : IGraph;
            public sealed record Zweiter([Kopf] Leser L, [Von(nameof(L))] Uebersetze<Fremd, ZielText> Hinaus) : IGraph;
            """);
        befunde.Select(b => b.Id).Should().Equal("CQRS119", "CQRS119", "CQRS119");
        befunde.Should().Contain(b => b.Text.Contains("MitId.AggregateId (System.Guid) ist nicht implizit zuweisbar"));
        befunde.Should().Contain(b => b.Text.Contains("Fremd.Betrag (string) ist nicht implizit zuweisbar"));
    }

    [Fact]
    public async Task Feld_ohne_Ziel_oder_Quelle_und_Feld_an_einer_Funktion_sind_CQRS119()
    {
        var befunde = await Befunde("""
            public sealed record Ablauf([Kopf, Feld("Betrag", "Betrag")] Rechner R,
                [Von(nameof(R)), Feld("Konto", "Bezug"), Feld("AggregateId", "Unbekannt")] Uebersetze<Gerechnet, Buche> Hinaus) : IGraph;
            """);
        befunde.Should().OnlyContain(b => b.Id == "CQRS119");
        befunde.Should().Contain(b => b.Text.Contains("[Feld] steht nur an einer Übersetzung"));
        befunde.Should().Contain(b => b.Text.Contains("nennt das Ziel 'Konto'"));
        befunde.Should().Contain(b => b.Text.Contains("nennt die Quelle 'Unbekannt'"));
    }

    [Fact]
    public async Task Eine_Uebersetzung_nimmt_einen_Command_ohne_Ziel_und_erzeugt_einen_Command_sonst_CQRS121()
    {
        var befunde = await Befunde("""
            public record Notiz(int Betrag) : ITransientEvent;
            public sealed class Gibt : IFunktion { public Buche Auf(Rechne r) => default!; }
            public sealed record Ablauf([Kopf] Uebersetze<Buche, Rechne> Hinein) : IGraph;
            public sealed record Zweiter([Kopf] Rechner R, [Von(nameof(R))] Uebersetze<Gerechnet, Notiz> Hinaus) : IGraph;
            """);
        befunde.Where(b => b.Id == "CQRS121").Should().HaveCount(2);
    }

    [Fact]
    public async Task Zwei_Uebersetzungen_hintereinander_und_eine_Kopf_Uebersetzung_ohne_Ziel_im_Graphen_sind_Formfehler()
    {
        var befunde = await Befunde("""
            public record Weiter(Guid Bezug, int Betrag) : ICommand;
            public sealed record Kette([Kopf] Rechner R, [Von(nameof(R))] Uebersetze<Gerechnet, Weiter> Eins,
                [Von(nameof(Eins)), Feld(nameof(Buche.AggregateId), nameof(Weiter.Bezug))] Uebersetze<Weiter, Buche> Zwei) : IGraph;
            public sealed record Umbenennung([Kopf] Uebersetze<Gerechnet, Weiter> Nur) : IGraph;
            """);
        befunde.Should().OnlyContain(b => b.Id == GraphAnalyzer.Id);
        befunde.Should().Contain(b => b.Text.Contains("zwei Übersetzungen hintereinander"));
        befunde.Should().Contain(b => b.Text.Contains("ist Kopf und gibt Weiter hinaus"));
    }
}
