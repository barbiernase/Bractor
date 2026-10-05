using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>CQRS058: <c>IDarf&lt;T&gt;</c> nur für Hineingehendes, nur an einem <c>IAkteur</c>.</summary>
public class AkteurAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        _ = typeof(Abstractions.IAkteur);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("AkteurProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new AkteurAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    private const string Vorspann = @"
using System; using System.Collections.Generic; using Abstractions;
namespace Probe {
public record Buche(Guid AggregateId) : ICommand;
public record Gebucht() : IEvent;
public record Hinweis() : ITransientEvent;
public record Zeige() : IQuery;
public record Antwort() : IQueryResponse;
public record Starte() : IPipelineTrigger;
public record Storniere(Guid AggregateId) : ICommand;
public record Gebucht2() : IEvent;
public record Ki(int Wert);
public sealed record Kassierer : IMensch, IDarf<Buche>;
public sealed record Revisor : IMensch, IDarf<Storniere>;
public interface IKasse : IAkteurDienst<Kassierer> { Ki Frage(); }
public interface IPruefer : IAkteurDienst<Revisor> { }
";

    [Fact]
    public async Task Command_Query_Trigger_Transient_sind_erlaubt()
        => (await Ids("public sealed record Kasse : IAkteur, IDarf<Buche>, IDarf<Zeige>, IDarf<Starte>, IDarf<Hinweis>;"))
            .Should().BeEmpty();

    [Fact]
    public async Task Ein_Event_aus_dem_Log_kann_man_nicht_duerfen()
        => (await Ids("public sealed record Kasse : IAkteur, IDarf<Gebucht>;")).Should().Equal("CQRS058");

    [Fact]
    public async Task Eine_Response_kann_man_nicht_duerfen()
        => (await Ids("public sealed record Kasse : IAkteur, IDarf<Antwort>;")).Should().Equal("CQRS058");

    [Fact]
    public async Task IDarf_ohne_IAkteur_ist_ein_Fehler()
        => (await Ids("public sealed record Kasse : IDarf<Buche>;")).Should().Equal("CQRS058");

    [Fact]
    public async Task Ein_Akteur_ohne_IDarf_ist_erlaubt()
        => (await Ids("public sealed record Gast : IAkteur;")).Should().BeEmpty();

    [Fact]
    public async Task Ein_Dienst_ist_kein_Akteur()
        => (await Ids("public interface IRobo : IKi, IDarf<Buche> { }")).Should().Equal("CQRS058");

    // ── CQRS060: der Dienst eines Akteurs — im Auftrag steht in der Signatur, nur was der Akteur darf ──

    private const string Pipeline = @"
public partial class P : IPipelineHandler
{
    public string PipelineId => ""p"";
    ";

    [Fact]
    public async Task Handle_im_Auftrag_mit_erlaubtem_Command_ist_ok()
        => (await Ids(Pipeline + @"public IEnumerable<OneOf<Buche>> Handle(Gebucht2 e, PipelineContext ctx, IKasse ki) { yield break; } }"))
            .Should().BeEmpty();

    [Fact]
    public async Task Handle_im_Auftrag_mit_nicht_erlaubtem_Command_ist_ein_Fehler()
        => (await Ids(Pipeline + @"public IEnumerable<OneOf<Buche, Storniere>> Handle(Gebucht2 e, PipelineContext ctx, IKasse ki) { yield break; } }"))
            .Should().Equal("CQRS060");

    [Fact]
    public async Task Hoechstens_ein_Akteur_je_Handle()
        => (await Ids(Pipeline + @"public IEnumerable<OneOf<Buche>> Handle(Gebucht2 e, PipelineContext ctx, IKasse a, IPruefer b) { yield break; } }"))
            .Should().Equal("CQRS060");

    [Fact]
    public async Task Akteur_Dienst_im_Konstruktor_irgendeiner_Klasse_ist_ein_Fehler()
        => (await Ids("public sealed class Hilfe { public Hilfe(IKasse k) { } }")).Should().Equal("CQRS060");

    [Fact]
    public async Task Akteur_Dienst_im_Konstruktor_ist_ein_Fehler()
        => (await Ids(Pipeline + @"private readonly IKasse _ki; public P(IKasse ki) { _ki = ki; }
            public IEnumerable<OneOf<Buche>> Handle(Gebucht2 e, PipelineContext ctx) { yield break; } }"))
            .Should().Equal("CQRS060", "CQRS060");
}
