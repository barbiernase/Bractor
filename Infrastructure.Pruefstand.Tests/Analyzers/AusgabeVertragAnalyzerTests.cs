using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// CQRS050 — geschlossener Ausgabe-Vertrag. Was ein Decider/Handler erzeugen kann, steht in der Signatur (konkreter Typ
/// oder OneOf konkreter Typen); eine offene Signatur (Interface, Typ-Parameter, object, abstrakte Klasse) ist ein
/// Build-Fehler. Reine Effekt-Handler (Task/void) bleiben erlaubt.
/// </summary>
public class AusgabeVertragAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> Analysiere(string code)
    {
        _ = typeof(Abstractions.ICommand);
        _ = typeof(Core.ProjectionWriter);
        _ = typeof(object);
        // Ausdrücklich angehängt: ein „_ = typeof(…)" darf der JIT verwerfen — ob Core geladen ist, hinge sonst an der Testreihenfolge.
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(Abstractions.ICommand).Assembly, typeof(Core.ProjectionWriter).Assembly }).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("VertragProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new AusgabeVertragAnalyzer())).GetAnalyzerDiagnosticsAsync();
    }

    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions; using Core;
namespace Probe {
public record Mach(Guid AggregateId) : ICommand;
public record Anderes(Guid AggregateId) : ICommand;
public record Gemacht() : IEvent;
public record Abgelehnt() : ITransientEvent;
public record Frage() : IQuery;
public record Antwort() : IQueryResponse;
public record Fehlt() : IQueryResponse;
public record Los() : IPipelineTrigger;
public partial class Ding : IState { public int Version { get; set; } public Guid Id { get; set; } }
}
";

    private static async Task<string[]> Ids(string code) => (await Analysiere(code)).Select(d => d.Id).ToArray();

    [Fact]
    public async Task Decide_mit_OneOf_oder_konkretem_Typ_ist_geschlossen()
    {
        (await Ids(@"namespace Probe { public class D : IDecider<Ding> {
            public IEnumerable<OneOf<Gemacht, Abgelehnt>> Decide(Mach c) { yield return new Gemacht(); }
            public IEnumerable<Gemacht> Decide(Anderes c) { yield return new Gemacht(); } } }")).Should().BeEmpty();
    }

    [Fact]
    public async Task Decide_mit_IEvent_ist_offen()
    {
        (await Ids(@"namespace Probe { public class D : IDecider<Ding> {
            public IEnumerable<IEvent> Decide(Mach c) { yield return new Gemacht(); } } }"))
            .Should().Equal(AusgabeVertragAnalyzer.OffenId);
    }

    [Fact]
    public async Task Pipeline_mit_ICommand_ist_offen_mit_OneOf_geschlossen_und_Task_erlaubt()
    {
        (await Ids(@"namespace Probe { public class P : IPipelineHandler { public string PipelineId => ""p"";
            public async IAsyncEnumerable<ICommand> Handle(Los t, PipelineContext ctx) { await Task.CompletedTask; yield return new Mach(Guid.Empty); } } }"))
            .Should().Equal(AusgabeVertragAnalyzer.OffenId);
        (await Ids(@"namespace Probe { public class P : IPipelineHandler { public string PipelineId => ""p"";
            public async IAsyncEnumerable<OneOf<Mach, Los>> Handle(Los t, PipelineContext ctx) { await Task.CompletedTask; yield return new Mach(Guid.Empty); }
            public Task Handle(Gemacht e, PipelineContext ctx) => Task.CompletedTask; } }")).Should().BeEmpty();
    }

    [Fact]
    public async Task OneOf_mit_Interface_Argument_ist_offen()
    {
        (await Ids(@"namespace Probe { public class R : ISubscriber { public string SubscriberId => ""r"";
            public async IAsyncEnumerable<OneOf<Mach, ICommand>> Handle(Gemacht e, IAggregateEnvelope env) { await Task.CompletedTask; yield return new Mach(Guid.Empty); } } }"))
            .Should().Equal(AusgabeVertragAnalyzer.OffenId);
    }

    [Fact]
    public async Task Projektion_mit_Task_ist_erlaubt()
    {
        (await Ids(@"namespace Probe { public class Pj : ISubscriber { public string SubscriberId => ""pj"";
            public Task Handle(Gemacht e, IAggregateEnvelope env, ProjectionWriter w) => Task.CompletedTask; } }")).Should().BeEmpty();
    }

    [Fact]
    public async Task Reader_mit_IQueryResponse_ist_offen_mit_OneOf_geschlossen()
    {
        (await Ids(@"namespace Probe { public class Pj : ISubscriber { public string SubscriberId => ""pj""; }
            public class Rd : IReader<Pj> { public Task<IQueryResponse> Handle(Frage q, IMessageEnvelope env, ReadContext ctx) => Task.FromResult<IQueryResponse>(new Antwort()); } }"))
            .Should().Equal(AusgabeVertragAnalyzer.OffenId);
        (await Ids(@"namespace Probe { public class Pj : ISubscriber { public string SubscriberId => ""pj""; }
            public class Rd : IReader<Pj> { public Task<OneOf<Antwort, Fehlt>> Handle(Frage q, IMessageEnvelope env, ReadContext ctx) => Task.FromResult<OneOf<Antwort, Fehlt>>(new Antwort()); } }"))
            .Should().BeEmpty();
    }
}
