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

/// <summary>CQRS058: <c>IDarf&lt;T&gt;</c> nur für Hineingehendes, <c>IHoert&lt;T&gt;</c> nur für Fakten (Event, Hinweis), beide nur an einem
/// <c>IAkteur</c>; CQRS063/064: Form eines Clients (ein Record, der nur nennt, wen er verkörpert).</summary>
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
public record Pruefe(Id<ProbeZiel> Ziel) : ICommand;
public sealed record Kassierer : IAkteur, IDarf<Buche>;
public sealed record Revisor : IAkteur, IHoert<Gebucht>, IDarf<Storniere>;
public record Sperre(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public sealed record Leser : IAkteur, IDarf<Zeige>, IHoert<Gebucht>, IHoert<Hinweis>;
";

    [Fact]
    public async Task Command_Query_Transient_sind_erlaubt()
        => (await Ids("public sealed record Kasse : IAkteur, IDarf<Buche>, IDarf<Zeige>, IDarf<Hinweis>, IDarf<Pruefe>;"))
            .Should().BeEmpty();

    [Fact]
    public async Task Ein_Interface_oder_eine_Funktion_kann_man_nicht_duerfen()
    {
        (await Ids("public interface IIrgendwas { } public sealed record Rechner : IAkteur, IDarf<IIrgendwas>;")).Should().Equal("CQRS058");
        (await Ids("public sealed class Rechnung : IFunktion { public System.Threading.Tasks.Task<Hinweis> Auf(Pruefe p) => null!; } public sealed record Rechner : IAkteur, IDarf<Rechnung>;"))
            .Should().Equal(["CQRS058"], "IDarf<IFunktion> ist entfallen — draußen rechnet ein Client, der auf ein Fakt reagiert");
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
    public async Task Ein_Interface_ist_kein_Akteur()
        => (await Ids("public interface IRobo : IAkteur, IDarf<Buche> { }")).Should().Equal("CQRS058");

    // ── IHoert am Akteur: Fakten (Event, Hinweis) — sein Client reagiert darauf oder nimmt sie zur Kenntnis ──

    [Fact]
    public async Task Ein_Akteur_hoert_Events_und_Hinweise()
        => (await Ids("public sealed record Pruefer : IAkteur, IHoert<Gebucht>, IHoert<Hinweis>, IDarf<Storniere>;")).Should().BeEmpty();

    [Fact]
    public async Task Einen_Command_hoert_kein_Akteur_weder_an_ein_Aggregat_noch_ohne_Ziel()
    {
        (await Ids("public sealed record Pruefer : IAkteur, IHoert<Buche>;")).Should().Equal(["CQRS058"], "sein Empfänger ist das Aggregat");
        (await Ids("public sealed record Pruefer : IAkteur, IHoert<Pruefe>;")).Should().Equal(["CQRS058"], "sein Empfänger ist der Kopf eines Graphen");
    }

    [Fact]
    public async Task Eine_Query_oder_eine_Response_hoert_kein_Akteur()
    {
        (await Ids("public sealed record Pruefer : IAkteur, IHoert<Zeige>;")).Should().Equal("CQRS058");
        (await Ids("public sealed record Pruefer : IAkteur, IHoert<Antwort>;")).Should().Equal("CQRS058");
    }

    [Fact]
    public async Task IHoert_ohne_IAkteur_ist_ein_Fehler()
        => (await Ids("public sealed record Kasse : IHoert<Gebucht>;")).Should().Equal("CQRS058");

    // ── CQRS063/064: Client (docs/konzept-akteure.md §4) ──

    [Fact]
    public async Task Ein_Client_verkoerpert_mehrere_Akteure()
        => (await Ids("public sealed record Kassenplatz : IClient, IVerkoerpert<Kassierer>, IVerkoerpert<Revisor>, IVerkoerpert<Leser>;"))
            .Should().BeEmpty("mehrere Akteure in EINEM Client sind erlaubt");

    [Fact]
    public async Task Ein_Akteur_ueber_zwei_Clients_ist_ok()
        => (await Ids(@"public sealed record KasseA : IClient, IVerkoerpert<Kassierer>;
            public sealed record KasseB : IClient, IVerkoerpert<Kassierer>, IVerkoerpert<Revisor>;")).Should().BeEmpty();

    [Fact]
    public async Task Ein_Client_ist_ein_Record()
    {
        (await Ids("public sealed class Kassenplatz : IClient { }")).Should().Equal("CQRS063");
        (await Ids("public interface IKassenplatz : IClient, IVerkoerpert<Kassierer> { }")).Should().Equal(["CQRS063"], "der Client-Vertrag als Interface ist entfallen");
    }

    [Fact]
    public async Task Ein_Client_erbt_nichts_Fremdes()
        => (await Ids("public sealed record Kassenplatz : IClient, IVerkoerpert<Kassierer>, IComparable { public int CompareTo(object? o) => 0; }"))
            .Should().NotBeEmpty().And.OnlyContain(i => i == "CQRS063");

    [Fact]
    public async Task Ein_Client_deklariert_nichts_selbst()
    {
        (await Ids("public sealed record Kassenplatz : IClient, IVerkoerpert<Kassierer> { public int Kasse => 1; }")).Should().Equal("CQRS063");
        (await Ids("public sealed record Kassenplatz : IClient, IVerkoerpert<Kassierer> { public void Auf(Gebucht e) { } }")).Should().Equal("CQRS063");
    }

    [Fact]
    public async Task Ein_Client_nennt_keine_eigene_Befugnis()
    {
        (await Ids("public sealed record Kassenplatz : IClient, IVerkoerpert<Kassierer>, IDarf<Sperre>;")).Should().Contain("CQRS063",
            "was über die Leitung geht, sind die IDarf der verkörperten Akteure — der Client nennt keine eigene");
        (await Ids("public sealed record Kassenplatz : IClient, IVerkoerpert<Kassierer>, IHoert<Gebucht>;")).Should().Contain("CQRS063",
            "ein eigener Kenntnis-Satz je Software ist entfallen — gehört wird am Akteur");
    }

    [Fact]
    public async Task Verkoerpern_nur_einen_konkreten_Akteur()
        => (await Ids("public interface IRolle : IAkteur { } public sealed record Kassenplatz : IClient, IVerkoerpert<IRolle>;"))
            .Should().Contain("CQRS064");
}
