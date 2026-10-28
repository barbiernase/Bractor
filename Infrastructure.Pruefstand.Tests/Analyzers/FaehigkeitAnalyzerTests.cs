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
[Trait("Kategorie", "Garantie")]
public class FaehigkeitAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        _ = typeof(Abstractions.ICommand);
        _ = typeof(Core.MartenStore);
        // Ausdrücklich angehängt: ein „_ = typeof(…)" darf der JIT verwerfen — ob Core geladen ist, hinge sonst an der Testreihenfolge.
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(Abstractions.ICommand).Assembly, typeof(Core.MartenStore).Assembly }).Distinct()
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
        (await Ids(@"namespace Probe { public sealed class P : IProjektion {
            public P(Konfig k) { }
            public Task Auf(Gemacht e, Herkunft h, ISchreibe s) => s.SchreibeAsync(new Zeile()); } }"))
            .Should().BeEmpty("Fähigkeit als Parameter + ein anderer Dienst (Konfig) im Konstruktor sind der vorgesehene Weg");
    }

    [Fact]
    public async Task Store_im_Konstruktor_oder_Feld_eines_Konsumenten_ist_CQRS054()
    {
        (await Ids(@"namespace Probe { public sealed class P : IProjektion {
            private readonly IBuch _buch;
            public P(IBuch buch) { _buch = buch; }
            public Task Auf(Gemacht e, Herkunft h) => _buch.SchreibeAsync(new Zeile()); } }"))
            .Should().Equal(FaehigkeitAnalyzer.ZustandId, FaehigkeitAnalyzer.ZustandId);
    }

    [Fact]
    public async Task Statischer_Store_und_Store_als_Eigenschaft_in_einer_Funktion_sind_CQRS054()
    {
        (await Ids(@"namespace Probe { public class L : IFunktion {
            public static IFinde? Finde;
            public IFinde? Auch { get; set; } } }"))
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
/// CQRS057 — Projektion und Reader in der Form, die Generatoren, Weckplan und Extractor über dieselbe Lesart rufen: <c>Task Auf(TEvent e[, Herkunft h],
/// Schreib-Fähigkeiten…)</c> bzw. <c>Frage(TQuery q, Lese-Fähigkeiten…)</c>. Eine Methode, die einen Eingang nimmt und anders heißt, ein Parameter, der
/// keine Fähigkeit ist, oder eine Projektion, die etwas zurückgibt, wäre im Editor sichtbar, zur Laufzeit tot.
/// </summary>
public class HandlerFormAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        _ = typeof(Abstractions.ICommand);
        _ = typeof(Core.MartenStore);
        // Ausdrücklich angehängt: ein „_ = typeof(…)" darf der JIT verwerfen — ob Core geladen ist, hinge sonst an der Testreihenfolge.
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(Abstractions.ICommand).Assembly, typeof(Core.MartenStore).Assembly }).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("FormProbe", new[] { CSharpSyntaxTree.ParseText(@"
using System; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks; using Abstractions; using Core;
namespace Probe {
public record Gemacht() : IEvent;
public record Hole() : IQuery;
public record Antwort() : IQueryResponse;
public interface IFinde : IReadStore { Task<int> FindeAsync(Guid id); }
public interface ISchreibe : IWriteStore { Task SchreibeAsync(Guid id); }
public record Konfig(int Wert);
" + code + " }") }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HandlerFormAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    [Fact]
    public async Task Auf_und_Frage_mit_Faehigkeiten_Herkunft_und_Abbruch_sind_erlaubt()
    {
        (await Ids(@"
public sealed class P : IProjektion { public Task Auf(Gemacht e, ISchreibe s, Herkunft h, CancellationToken ct) => s.SchreibeAsync(h.Stream); }
public sealed class R : IReader { public Task<Antwort> Frage(Hole q, IFinde f, CancellationToken ct) => Task.FromResult(new Antwort()); }
")).Should().BeEmpty();
    }

    [Fact]
    public async Task Anderer_Name_und_ein_Parameter_der_keine_Faehigkeit_ist_sind_CQRS057()
    {
        (await Ids(@"
public sealed class P : IProjektion {
    public Task Verarbeite(Gemacht e, Herkunft h) => Task.CompletedTask;
    public Task Auf(Gemacht e, Konfig k) => Task.CompletedTask; }"))
            .Should().Equal(HandlerFormAnalyzer.FormId, HandlerFormAnalyzer.FormId);
        (await Ids(@"
public sealed class R : IReader {
    public Task<Antwort> Handle(Hole q, IFinde f) => Task.FromResult(new Antwort());
    public Task<Antwort> Frage(Hole q, ISchreibe s) => Task.FromResult(new Antwort()); }"))
            .Should().Equal(HandlerFormAnalyzer.FormId, HandlerFormAnalyzer.FormId);
    }

    [Theory]
    [InlineData("public interface IEreignis : IEvent { }", "IEreignis")]
    [InlineData("public abstract record Basis : IEvent;", "Basis")]
    [InlineData("public record Messwert(double Wert) : ITransientEvent;", "Messwert")]
    public async Task Eine_Projektion_hoert_nur_konkrete_persistente_Events_sonst_CQRS057(string typ, string name)
        => (await Ids(typ + @"
public sealed class P : IProjektion { public Task Auf(" + name + @" e) => Task.CompletedTask; }"))
            .Should().Equal(HandlerFormAnalyzer.FormId);

    [Fact]
    public async Task Eine_Projektion_die_etwas_ausgibt_ist_CQRS057()
        => (await Ids(@"
public sealed class P : IProjektion {
    public Task<Gemacht> Auf(Gemacht e) => Task.FromResult(e); }"))
            .Should().Equal(HandlerFormAnalyzer.FormId);
}
