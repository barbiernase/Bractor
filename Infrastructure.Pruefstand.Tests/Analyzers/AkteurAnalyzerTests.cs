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
public record Sperre(Guid AggregateId) : ICommand;
public sealed record Leser : IMensch, IDarf<Zeige>;
";

    /// <summary>Zwei Akteur-Verträge für die Client-Proben (CQRS063–065).</summary>
    private const string Teile = @"
public interface IKasse1 : IAkteurVertrag<Kassierer> { OneOf<Buche> Auf(Gebucht e); void Auf(Hinweis e); }
public interface IRevision : IAkteurVertrag<Revisor> { IAsyncEnumerable<OneOf<Storniere>> Auf(Gebucht2 e); }
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

    // ── CQRS061/062: Akteur-Vertrag (docs/konzept-akteure.md §3) ──

    [Fact]
    public async Task Vertrag_mit_Zusage_Kenntnis_und_Strom_ist_ok()
        => (await Ids(@"public interface IKassierer : IAkteurVertrag<Kassierer>
            { OneOf<Buche> Auf(Gebucht e); void Auf(Hinweis e); IAsyncEnumerable<OneOf<Buche, Storniere>> Auf(Gebucht2 e); }"))
            .Should().BeEmpty();

    [Fact]
    public async Task Ein_konkreter_Command_als_Rueckgabe_ist_ok()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { Buche Auf(Gebucht e); }")).Should().BeEmpty();

    [Fact]
    public async Task Ein_Vertrag_ist_ein_Interface()
        => (await Ids("public sealed class Kassierer2 : IAkteurVertrag<Kassierer> { }")).Should().Equal("CQRS061");

    [Fact]
    public async Task Ein_Akteur_darf_seinen_Vertrag_in_Teile_schneiden()
        => (await Ids(@"public interface IKassiererA : IAkteurVertrag<Kassierer> { OneOf<Buche> Auf(Gebucht e); }
            public interface IKassiererB : IAkteurVertrag<Kassierer> { void Auf(Hinweis e); }
            public interface IKassierer : IKassiererA, IKassiererB { void Auf(Gebucht2 e); }")).Should().BeEmpty();

    [Fact]
    public async Task Ueber_alle_Teile_hoechstens_eine_Zusage_je_Event()
        => (await Ids(@"public interface IKassiererA : IAkteurVertrag<Kassierer> { OneOf<Buche> Auf(Gebucht e); }
            public interface IKassiererB : IAkteurVertrag<Kassierer> { void Auf(Gebucht e); }")).Should().Equal("CQRS061", "CQRS061");

    [Fact]
    public async Task Zusagen_heissen_Auf()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { void Bei(Gebucht e); }")).Should().Equal("CQRS061");

    [Fact]
    public async Task Reagieren_kann_man_nur_auf_Events()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { void Auf(Buche c); }")).Should().Equal("CQRS061");

    [Fact]
    public async Task Je_Event_hoechstens_eine_Zusage()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { void Auf(Gebucht e); Buche Auf(Gebucht e, int x); }"))
            .Should().Equal("CQRS061");

    [Fact]
    public async Task Kein_Property_im_Vertrag()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { int Zaehler { get; } }")).Should().Equal("CQRS061");

    [Fact]
    public async Task Ausgabe_ICommand_ist_ein_Fehler()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { OneOf<ICommand> Auf(Gebucht e); }")).Should().Equal("CQRS062");

    [Fact]
    public async Task Ausgabe_als_Task_ist_ein_Fehler()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { System.Threading.Tasks.Task<Buche> Auf(Gebucht e); }"))
            .Should().Equal("CQRS062");

    [Fact]
    public async Task Ausgabe_Event_ist_ein_Fehler()
        => (await Ids("public interface IKassierer : IAkteurVertrag<Kassierer> { OneOf<Gebucht2> Auf(Gebucht e); }")).Should().Equal("CQRS062");

    [Fact]
    public async Task Vertrags_Ausgabe_darf_der_Akteur_auch_drinnen()
        => (await Ids(@"public interface IKassierer : IAkteurVertrag<Kassierer> { OneOf<Storniere> Auf(Gebucht e); }" + Pipeline
            + @"public IEnumerable<OneOf<Storniere>> Handle(Gebucht2 e, PipelineContext ctx, IKasse ki) { yield break; } }"))
            .Should().BeEmpty("Befugt = IDarf ∪ Ausgaben(Vertrag)");

    [Fact]
    public async Task Dieselbe_Entscheidung_drinnen_und_draussen_ist_ein_Fehler()
        => (await Ids(@"public interface IKassierer : IAkteurVertrag<Kassierer> { OneOf<Buche> Auf(Gebucht e); }" + Pipeline
            + @"public IEnumerable<OneOf<Buche>> Handle(Gebucht e, PipelineContext ctx, IKasse ki) { yield break; } }"))
            .Should().Equal("CQRS060");

    // ── CQRS063–065: Client-Vertrag (docs/konzept-akteure.md §4) ──

    [Fact]
    public async Task Ein_Client_mit_zwei_Akteuren_ist_ok()
        => (await Ids(Teile + @"public interface IKassenplatz : IClientVertrag, IKasse1, IRevision, ISendet<Buche>, ISendet<Storniere>, IFragt<Zeige>
            { void Auf(Gebucht2b e); }
            public record Gebucht2b() : IEvent;")).Should().BeEmpty("zwei Teile verschiedener Akteure in EINEM Client sind kein zweiter Vertrag je Akteur");

    [Fact]
    public async Task Ein_Akteur_ueber_zwei_Clients_ist_ok()
        => (await Ids(Teile + @"public interface IKasseA : IClientVertrag, IKasse1 { }
            public interface IKasseB : IClientVertrag, IKasse1, ISendet<Buche> { }")).Should().BeEmpty();

    [Fact]
    public async Task Ein_Client_ist_ein_Interface()
        => (await Ids("public sealed class Kassenplatz : IClientVertrag { }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Ein_Client_ist_selbst_kein_Vertrags_Teil()
        => (await Ids("public interface IKassenplatz : IClientVertrag, IAkteurVertrag<Kassierer> { }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Ein_Client_erbt_nichts_Fremdes()
        => (await Ids("public interface IKassenplatz : IClientVertrag, IDisposable { }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Eine_Ausgabe_gehoert_in_einen_Akteur_Vertrag()
        => (await Ids("public interface IKassenplatz : IClientVertrag { OneOf<Buche> Auf(Gebucht e); }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Kenntnis_nur_von_Events()
        => (await Ids("public interface IKassenplatz : IClientVertrag { void Auf(Buche c); }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Senden_nur_was_ein_Akteur_darf()
        => (await Ids("public interface IKassenplatz : IClientVertrag, ISendet<Sperre> { }")).Should().Equal("CQRS064");

    [Fact]
    public async Task Fragen_nur_Queries_senden_keine_Queries()
        => (await Ids("public interface IKassenplatz : IClientVertrag, ISendet<Zeige>, IFragt<Buche> { }")).Should().Equal("CQRS064", "CQRS064");

    [Fact]
    public async Task Je_Event_hoechstens_eine_Methode_im_Client()
        => (await Ids(Teile + "public interface IKassenplatz : IClientVertrag, IKasse1 { void Auf(Gebucht e); }")).Should().Equal("CQRS065");
}
