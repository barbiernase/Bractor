using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// CQRS070 — ein Quellen-Typ ist <c>public sealed record X : DateiGemeldet;</c> ohne eigene Member und ohne Parameterliste
/// (docs/04 §4.7). Ein IQuellNachricht ohne Treiber-Basis (Webhook-/Test-Quelle) prüft er nicht.
/// </summary>
public class QuellenTypAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(Abstractions.DateiGemeldet).Assembly }).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var comp = CSharpCompilation.Create("QuellenProbe",
            new[] { CSharpSyntaxTree.ParseText("using System; using Abstractions; namespace Probe {\n" + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new QuellenTypAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    [Fact]
    public async Task Die_feste_Form_ist_erlaubt()
        => (await Ids("public sealed record DateiErkannt : DateiGemeldet;")).Should().BeEmpty();

    [Fact]
    public async Task Nicht_sealed_ist_ein_Fehler()
        => (await Ids("public record DateiErkannt : DateiGemeldet;")).Should().Equal(QuellenTypAnalyzer.FormId);

    [Fact]
    public async Task Eigene_Member_sind_ein_Fehler()
        => (await Ids("public sealed record DateiErkannt : DateiGemeldet { public string Extra { get; init; } = \"\"; }"))
            .Should().Equal(QuellenTypAnalyzer.FormId);

    [Fact]
    public async Task Eine_Parameterliste_ist_ein_Fehler()
        => (await Ids("public sealed record DateiErkannt(int Extra) : DateiGemeldet;")).Should().Equal(QuellenTypAnalyzer.FormId);

    [Fact]
    public async Task Der_Takt_Treiber_hat_dieselbe_feste_Form()
    {
        (await Ids("public sealed record Stundentakt : TaktGemeldet;")).Should().BeEmpty();
        (await Ids("public sealed record Stundentakt : TaktGemeldet { public int Extra { get; init; } }")).Should().Equal(QuellenTypAnalyzer.FormId);
    }

    [Fact]
    public void Der_Quellen_Generator_baut_je_Treiber_seine_Fabrik()
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(Abstractions.DateiGemeldet).Assembly, typeof(Infrastructure.Quellen.QuellEingang).Assembly })
            .Distinct().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Laufzeit", new[] { CSharpSyntaxTree.ParseText(
                "using Abstractions; namespace Probe { public sealed record DateiErkannt : DateiGemeldet; public sealed record Stundentakt : TaktGemeldet; }") },
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        CSharpGeneratorDriver.Create(new Infrastructure.SourceGeneration.QuellenGenerator()).RunGeneratorsAndUpdateCompilation(comp, out var neu, out _);
        neu.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var quelle = string.Join("\n", neu.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        quelle.Should().Contain("new QuellTyp(typeof(global::Probe.Stundentakt), \"Takt\", \"Stundentakt\", \"quelle:Stundentakt\", null,");
        quelle.Should().Contain("t => new global::Probe.Stundentakt { Zeitpunkt = t.Zeitpunkt, Kennung = t.Kennung }");
        quelle.Should().Contain("new QuellTyp(typeof(global::Probe.DateiErkannt), \"Datei\"");
    }

    [Fact]
    public async Task Eine_Quelle_ohne_Treiber_Basis_prueft_er_nicht()
        => (await Ids("public sealed record Ping(int Seq) : IQuellNachricht { public string Kennung => Seq.ToString(); }")).Should().BeEmpty();
}
