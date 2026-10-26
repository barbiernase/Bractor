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

/// <summary>CQRS058: <c>IDarf&lt;T&gt;</c> nur für Hineingehendes, nur an einem <c>IAkteur</c>; CQRS063/064: Form und Rand eines Client-Vertrags.</summary>
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
public partial class ProbeZiel : IState { public int Version { get; set; } public Guid Id { get; set; } }
public record Buche(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public record Gebucht() : IEvent;
public record Hinweis() : ITransientEvent;
public record Zeige() : IQuery;
public record Antwort() : IQueryResponse;
public record Storniere(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public record Gebucht2() : IEvent;
public sealed record Kassierer : IMensch, IDarf<Buche>, IDarf<IPruefung>;
public sealed record Revisor : IMensch, IDarf<Storniere>;
public record Sperre(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public sealed record Leser : IMensch, IDarf<Zeige>;
public interface IPruefung : IFunktion { Storniere Auf(Gebucht e); }
public interface IFremdPruefung : IFunktion { Storniere Auf(Gebucht2 e); }
";

    [Fact]
    public async Task Command_Query_Transient_sind_erlaubt()
        => (await Ids("public sealed record Kasse : IAkteur, IDarf<Buche>, IDarf<Zeige>, IDarf<Hinweis>;"))
            .Should().BeEmpty();

    [Fact]
    public async Task Eine_Funktion_draussen_darf_man_rechnen_ein_anderes_Interface_nicht()
    {
        (await Ids("public sealed record Rechner : IMaschine, IDarf<IFremdPruefung>;")).Should().BeEmpty();
        (await Ids("public interface IIrgendwas { } public sealed record Rechner : IMaschine, IDarf<IIrgendwas>;")).Should().Equal("CQRS058");
    }

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

    // ── CQRS063/064: Client-Vertrag (docs/konzept-akteure.md §4, Plan §2.7) ──

    [Fact]
    public async Task Ein_Client_traegt_sendet_fragt_und_hoert()
        => (await Ids("public interface IKassenplatz : IClientVertrag, ITraegt<IPruefung>, ISendet<Buche>, ISendet<Storniere>, IFragt<Zeige>, "
                      + "IHoert<Gebucht2>, IHoert<Hinweis> { }"))
            .Should().BeEmpty("zwei Akteure (Kassierer, Revisor, Leser) in EINEM Client sind erlaubt");

    [Fact]
    public async Task Ein_Akteur_ueber_zwei_Clients_ist_ok()
        => (await Ids(@"public interface IKasseA : IClientVertrag, ITraegt<IPruefung> { }
            public interface IKasseB : IClientVertrag, ITraegt<IPruefung>, ISendet<Buche> { }")).Should().BeEmpty();

    [Fact]
    public async Task Ein_Client_setzt_sich_aus_Clients_zusammen()
        => (await Ids(@"public interface IKasseA : IClientVertrag, ITraegt<IPruefung> { }
            public interface IKasse : IKasseA, ISendet<Buche> { }")).Should().BeEmpty();

    [Fact]
    public async Task Ein_Client_ist_ein_Interface()
        => (await Ids("public sealed class Kassenplatz : IClientVertrag { }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Ein_Client_erbt_nichts_Fremdes()
        => (await Ids("public interface IKassenplatz : IClientVertrag, IDisposable { }")).Should().Equal("CQRS063");

    [Fact]
    public async Task Ein_Client_deklariert_nichts_selbst()
    {
        (await Ids("public interface IKassenplatz : IClientVertrag { void Auf(Gebucht e); }")).Should().Equal("CQRS063");
        (await Ids("public interface IKassenplatz : IClientVertrag { Buche Auf(Gebucht e); }")).Should().Equal("CQRS063");
    }

    [Fact]
    public async Task Senden_nur_was_ein_Akteur_darf()
        => (await Ids("public interface IKassenplatz : IClientVertrag, ISendet<Sperre> { }")).Should().Equal("CQRS064");

    [Fact]
    public async Task Tragen_nur_was_ein_Akteur_darf_und_nur_Funktionen_als_Schnittstelle()
    {
        (await Ids("public interface IKassenplatz : IClientVertrag, ITraegt<IFremdPruefung> { }")).Should().Equal("CQRS064");
        (await Ids("public sealed class Pruefung : IFunktion { public Storniere Auf(Gebucht e) => null!; } "
                   + "public interface IKassenplatz : IClientVertrag, ITraegt<Pruefung> { }")).Should().Equal("CQRS064");
    }

    [Fact]
    public async Task Fragen_nur_Queries_senden_keine_Queries()
        => (await Ids("public interface IKassenplatz : IClientVertrag, ISendet<Zeige>, IFragt<Buche> { }")).Should().Equal("CQRS064", "CQRS064");

    [Fact]
    public async Task Hoeren_nur_Events_und_Hinweise()
        => (await Ids("public interface IKassenplatz : IClientVertrag, IHoert<Buche>, IHoert<Gebucht>, IHoert<Hinweis> { }")).Should().Equal("CQRS064");
}
