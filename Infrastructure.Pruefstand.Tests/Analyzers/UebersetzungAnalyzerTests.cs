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
/// CQRS119/CQRS121 (GR-UEBERSETZUNG): eine Übersetzung ist ein Record der Domäne (<c>IUebersetzung&lt;TEin, TAus&gt;</c>, docs/plan-ohne-pipeline.md)
/// und ordnet nach der einen Lesart (<c>Uebersetzungsform</c>) zu — <c>[Feld]</c>, gleicher Name mit implizit zuweisbarem Typ, Standardwert, sonst ein
/// Fehler; <c>Guid</c> ⇄ <c>Id&lt;T&gt;</c> nur ausdrücklich. Sie nimmt einen Command ohne Ziel oder ein Event (eine Antwort) und erzeugt einen Command;
/// ein Graph nennt sie nie als Knoten (CQRS115).
/// </summary>
public class UebersetzungAnalyzerTests
{
    private static async Task<(string Id, string Text)[]> Befunde(string code)
    {
        _ = typeof(IUebersetzung<,>);
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
        public sealed class Rechner : IFunktion { public Gerechnet Auf(Rechne r) => default!; }
        """;

    [Fact]
    public async Task Feld_Name_und_Standardwert_ergeben_eine_vollstaendige_Zuordnung_in_beide_Richtungen()
        => (await Befunde("""
            [Feld(nameof(Rechne.Bezug), nameof(Anfrage.Konto))] public sealed record Hinein : IUebersetzung<Anfrage, Rechne>;
            [Feld(nameof(Buche.AggregateId), nameof(Gerechnet.Bezug))] public sealed record Hinaus : IUebersetzung<Gerechnet, Buche>;
            public sealed record Antwort : IUebersetzung<Gebucht, Bestaetigt>;
            """)).Should().BeEmpty("Bezug ⇄ Konto ausdrücklich (Id ⇄ Guid), Betrag nach Namen (int → int bzw. int → decimal erweitert), Faktor und Text aus "
            + "dem Standard; eine Antwort-Übersetzung nimmt ein Event");

    [Fact]
    public async Task Ein_Parameter_ohne_Quelle_ist_CQRS119()
    {
        var befunde = await Befunde("""
            public sealed record Hinaus : IUebersetzung<Gerechnet, Buche>;
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
            public sealed record Hinaus : IUebersetzung<MitId, Buche>;
            public sealed record Zweite : IUebersetzung<Fremd, ZielText>;
            """);
        befunde.Select(b => b.Id).Should().Equal("CQRS119", "CQRS119", "CQRS119");
        befunde.Should().Contain(b => b.Text.Contains("MitId.AggregateId (System.Guid) ist nicht implizit zuweisbar"));
        befunde.Should().Contain(b => b.Text.Contains("Fremd.Betrag (string) ist nicht implizit zuweisbar"));
    }

    [Fact]
    public async Task Feld_ohne_Ziel_oder_Quelle_ist_CQRS119()
    {
        var befunde = await Befunde("""
            [Feld("Konto", "Bezug"), Feld("AggregateId", "Unbekannt")] public sealed record Hinaus : IUebersetzung<Gerechnet, Buche>;
            """);
        befunde.Should().OnlyContain(b => b.Id == "CQRS119");
        befunde.Should().Contain(b => b.Text.Contains("nennt das Ziel 'Konto'"));
        befunde.Should().Contain(b => b.Text.Contains("nennt die Quelle 'Unbekannt'"));
    }

    [Fact]
    public async Task Eine_Uebersetzung_nimmt_einen_Command_ohne_Ziel_oder_ein_Event_und_erzeugt_einen_Command_sonst_CQRS121()
    {
        var befunde = await Befunde("""
            public record Notiz(int Betrag) : ITransientEvent;
            public sealed record Hinein : IUebersetzung<Buche, Rechne>;
            public sealed record Hinaus : IUebersetzung<Gerechnet, Notiz>;
            public sealed record Vom : IUebersetzung<Notiz, Rechne>;
            """);
        befunde.Where(b => b.Id == "CQRS121").Should().HaveCount(3, "ein Command an ein Aggregat, ein Hinweis als Ergebnis, ein Hinweis als Eingang");
    }

    [Fact]
    public async Task Ein_Graph_nennt_keine_Uebersetzung_als_Knoten()
    {
        var befunde = await Befunde("""
            [Feld(nameof(Buche.AggregateId), nameof(Gerechnet.Bezug))] public sealed record Hinaus : IUebersetzung<Gerechnet, Buche>;
            public sealed record Ablauf([Kopf] Rechner R, [Von(nameof(R))] Hinaus H) : IGraph;
            """);
        befunde.Should().Contain(b => b.Id == GraphAnalyzer.Id && b.Text.Contains("ist eine Übersetzung") && b.Text.Contains("lebt in der Domäne"));
    }
}
