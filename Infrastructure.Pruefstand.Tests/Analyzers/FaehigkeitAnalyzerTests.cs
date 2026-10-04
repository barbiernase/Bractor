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
/// Fähigkeiten statt Rumpf-Analyse: welche Store-Funktionen ein Handle rufen DARF, steht als Parameter in der Signatur.
/// CQRS051 = Fähigkeit trägt genau eine Funktion; CQRS054 = kein Store/Fristplan im Zustand eines Konsumenten;
/// CQRS055 = kein selbstgebauter Store. Andere Dienste im Konstruktor bleiben erlaubt.
/// </summary>
public class FaehigkeitAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        _ = typeof(Abstractions.ICommand);
        _ = typeof(Core.ProjectionWriter);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("FaehigkeitProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FaehigkeitAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions; using Core;
namespace Probe {
public record Gemacht() : IEvent;
public class Zeile : IReadModel { public Guid Id { get; set; } }
public interface ISchreibe : IWriteStore { Task SchreibeAsync(Zeile z); }
public interface IFinde : IReadStore { Task<Zeile?> FindeAsync(Guid id); }
public interface IBuch : IStore, ISchreibe, IFinde { }
public sealed class Buch : IBuch
{
    public Task SchreibeAsync(Zeile z) => Task.CompletedTask;
    public Task<Zeile?> FindeAsync(Guid id) => Task.FromResult<Zeile?>(null);
}
public record Konfig(int Wert);
}
";

    [Fact]
    public async Task Faehigkeit_als_Handle_Parameter_ist_erlaubt()
    {
        (await Ids(@"namespace Probe { public partial class P : ISubscriber, IPullSubscriber {
            public P(Konfig k) { }
            public string SubscriberId => ""p"";
            public Task Handle(Gemacht e, IAggregateEnvelope env, ProjectionWriter w, ISchreibe s) => s.SchreibeAsync(new Zeile()); } }"))
            .Should().BeEmpty("Fähigkeit als Parameter + ein anderer Dienst (Konfig) im Konstruktor sind der vorgesehene Weg");
    }

    [Fact]
    public async Task Store_im_Konstruktor_oder_Feld_eines_Konsumenten_ist_CQRS054()
    {
        (await Ids(@"namespace Probe { public partial class P : ISubscriber, IPullSubscriber {
            private readonly IBuch _buch;
            public P(IBuch buch) { _buch = buch; }
            public string SubscriberId => ""p"";
            public Task Handle(Gemacht e, IAggregateEnvelope env, ProjectionWriter w) => _buch.SchreibeAsync(new Zeile()); } }"))
            .Should().Equal(FaehigkeitAnalyzer.ZustandId, FaehigkeitAnalyzer.ZustandId);
    }

    [Fact]
    public async Task Statischer_Store_und_Fristplan_in_einer_Pipeline_sind_CQRS054()
    {
        (await Ids(@"namespace Probe { public class L : IPipelineHandler {
            public static IFinde? Finde;
            public IFristplan? Plan { get; set; }
            public string PipelineId => ""l""; } }"))
            .Should().Equal(FaehigkeitAnalyzer.ZustandId, FaehigkeitAnalyzer.ZustandId);
    }

    [Fact]
    public async Task Selbstgebauter_Store_ist_CQRS055()
    {
        (await Ids(@"namespace Probe { public static class Irgendwo { public static object Baue() => new Buch(); } }"))
            .Should().Equal(FaehigkeitAnalyzer.NeuId);
    }

    [Fact]
    public async Task Faehigkeit_mit_zwei_Funktionen_oder_geerbter_Faehigkeit_ist_CQRS051()
    {
        (await Ids(@"namespace Probe {
            public interface IZwei : IWriteStore { Task A(); Task B(); }
            public interface IVerkettet : IReadStore, IFinde { Task<int> ZaehleAsync(); } }"))
            .Should().Equal(FaehigkeitAnalyzer.FormId, FaehigkeitAnalyzer.FormId);
    }
}

/// <summary>
/// CQRS057 — ein Handler, den der Editor an seinen Typen erkennt, muss auch der sein, den der Dispatch ruft:
/// Name <c>Handle</c>, Kontext an fester Stelle, dahinter nur Fähigkeiten. Sonst wäre er im Editor sichtbar, zur Laufzeit tot.
/// </summary>
public class HandlerFormAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        _ = typeof(Abstractions.ICommand);
        _ = typeof(Core.ProjectionWriter);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("FormProbe", new[] { CSharpSyntaxTree.ParseText(@"
using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions; using Core;
namespace Probe {
public record Gemacht() : IEvent;
public record Frage() : IQuery;
public record Antwort() : IQueryResponse;
public record Los() : IPipelineTrigger;
public interface IFinde : IReadStore { Task<int> FindeAsync(Guid id); }
public record Konfig(int Wert);
public interface IKi : IAkteur { int Rate(); }
" + code + " }") }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HandlerFormAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    [Fact]
    public async Task Korrekte_Handler_mit_Faehigkeiten_sind_erlaubt()
    {
        (await Ids(@"
public partial class P : ISubscriber { public string SubscriberId => ""p"";
    public Task Handle(Gemacht e, IAggregateEnvelope env, ProjectionWriter w) => Task.CompletedTask; }
public partial class R : IReader<P> {
    public Task<Antwort> Handle(Frage q, IMessageEnvelope env, ReadContext ctx, IFinde f) => Task.FromResult(new Antwort()); }
public partial class L : IPipelineHandler { public string PipelineId => ""l"";
    public Task OnInitializeAsync(PipelineContext ctx) => Task.CompletedTask;
    public Task Handle(Los t, PipelineContext ctx, IFinde f) => Task.CompletedTask; }")).Should().BeEmpty();
    }

    [Fact]
    public async Task Anderer_Name_falsche_Stelle_und_Nicht_Faehigkeit_sind_CQRS057()
    {
        (await Ids(@"
public partial class P : ISubscriber { public string SubscriberId => ""p"";
    public Task Verarbeite(Gemacht e, IAggregateEnvelope env, ProjectionWriter w) => Task.CompletedTask;
    public Task Handle(Gemacht e, ProjectionWriter w, IAggregateEnvelope env) => Task.CompletedTask; }
public partial class L : IPipelineHandler { public string PipelineId => ""l"";
    public Task Handle(Los t, PipelineContext ctx, Konfig k) => Task.CompletedTask; }"))
            .Should().Equal(HandlerFormAnalyzer.FormId, HandlerFormAnalyzer.FormId, HandlerFormAnalyzer.FormId);
    }

    [Fact]
    public async Task Akteur_Dienst_als_Parameter_nur_am_Pipeline_Handle()
    {
        (await Ids(@"
public partial class L : IPipelineHandler { public string PipelineId => ""l"";
    public Task Handle(Los t, PipelineContext ctx, IKi ki) => Task.CompletedTask; }")).Should().BeEmpty();
        (await Ids(@"
public partial class P : ISubscriber { public string SubscriberId => ""p"";
    public Task Handle(Gemacht e, IAggregateEnvelope env, ProjectionWriter w, IKi ki) => Task.CompletedTask; }"))
            .Should().Equal(HandlerFormAnalyzer.FormId);
    }
}
