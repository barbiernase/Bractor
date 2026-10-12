using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// CQRS084–088 — Regeln über das Gesamtbild der Domänen-Projekte, geprüft in der Laufzeit-Compilation (Werkbank P7.4): die Domäne liegt
/// dort nur als Metadaten-Referenz vor — genau wie hier (die Probe wird übersetzt und als Referenz einer leeren „Laufzeit“ gelesen).
/// </summary>
public class GesamtbildAnalyzerTests
{
    private static async Task<Diagnostic[]> Befunde(string domaene)
    {
        _ = typeof(Abstractions.IReader<>);
        // Nur BCL + Vertrag: die echten Domänen-Assemblies im Test-Prozess gehören nicht ins Bild der Probe.
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => a.GetName().Name is { } n && (n == "Abstractions" || n == "netstandard" || n == "mscorlib" || n.StartsWith("System")))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var dom = CSharpCompilation.Create("Probe.Domain", new[] { CSharpSyntaxTree.ParseText(Vorspann + domaene + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var bild = new MemoryStream();
        var emit = dom.Emit(bild);
        emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Domänen-Probe muss kompilieren");
        var laufzeit = CSharpCompilation.Create("Probe.Laufzeit", new[] { CSharpSyntaxTree.ParseText("namespace Laufzeit { public static class Leer { } }") },
            refs.Add(MetadataReference.CreateFromImage(bild.ToArray())), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return (await laufzeit.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GesamtbildAnalyzer())).GetAnalyzerDiagnosticsAsync()).ToArray();
    }

    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public record Eroeffne(Guid AggregateId) : ICommand;
public record Eroeffnet() : IEvent;
public record SchonOffen() : ITransientEvent;
public partial class Konto : IState
{
    public int Version { get; set; } public Guid Id { get; set; }
    public class Decider : IDecider<Konto> { public OneOf<Eroeffnet, SchonOffen> Decide(Eroeffne c) => default!; }
    public class Applier : IApplier<Konto> { public void Apply(Eroeffnet e) { } }
}
public sealed class Uebersicht : ISubscriber { public string SubscriberId => ""u""; public Task Handle(Eroeffnet e, IAggregateEnvelope env) => Task.CompletedTask; }
public record Frage() : IQuery;
public record Teil(int Wert) : IQueryResponse;
public record Antwort(IReadOnlyList<Teil> Teile) : IQueryResponse;
public sealed class Leser : IReader<Uebersicht> { public OneOf<Antwort> Handle(Frage q) => default!; }
public sealed record Kunde : IMensch, IDarf<Eroeffne>, IDarf<Frage>;
";

    private static async Task<string[]> Ids(string domaene) => (await Befunde(domaene)).Select(d => d.Id).ToArray();

    [Fact]
    public async Task Eine_geschlossene_Domaene_ist_still()
        => (await Ids("")).Should().BeEmpty("jede Nachricht hat Erzeuger und Konsument; Teil lebt als Feld der Antwort");

    [Fact]
    public async Task Eine_Query_mit_zwei_Readern_ist_ein_Fehler_ohne_Reader_ein_offener_Eingang()
    {
        var befunde = await Befunde("public sealed class Zweiter : IReader<Uebersicht> { public OneOf<Antwort> Handle(Frage q) => default!; }"
                                    + "public record Vergessen() : IQuery;");
        befunde.Select(d => d.Id).Should().BeEquivalentTo([GesamtbildAnalyzer.QueryId, GesamtbildAnalyzer.EingangOffenId]);
        befunde.Single(d => d.Id == GesamtbildAnalyzer.QueryId).GetMessage().Should().StartWith("'Frage'").And.Contain("Leser").And.Contain("Zweiter");
        befunde.Single(d => d.Id == GesamtbildAnalyzer.EingangOffenId).GetMessage().Should().StartWith("'Vergessen'");
        befunde.Should().OnlyContain(d => d.Location == Location.None, "die Typen liegen in der Domänen-Referenz — der Name führt zur Karte");
    }

    [Fact]
    public async Task Ein_Command_ohne_Decider_ist_ein_offener_Eingang()
        => (await Befunde("public record Schliesse(Guid AggregateId) : ICommand;")).Should().ContainSingle()
            .Which.GetMessage().Should().StartWith("'Schliesse' (Command) nimmt niemand an");

    [Fact]
    public async Task Ein_Event_oder_eine_Response_ohne_Erzeuger_ist_ein_offener_Ausgang()
    {
        var befunde = await Befunde("public record Tot() : IEvent; public record Lose() : IQueryResponse; "
                                    + "public sealed record Quelle(string K) : IQuellNachricht { public string Kennung => K; }");
        befunde.Select(d => d.Id).Should().Equal(GesamtbildAnalyzer.AusgangOffenId, GesamtbildAnalyzer.AusgangOffenId);
        befunde.Select(d => d.GetMessage()).Should().Contain(m => m.StartsWith("'Tot' (Event)")).And.Contain(m => m.StartsWith("'Lose' (Response)"));
    }

    [Fact]
    public async Task Eine_Zusage_ohne_Client_ist_eine_Warnung_sobald_es_Clients_gibt()
    {
        const string vertrag = "public sealed record Kasse : IMaschine; public interface IKasse : IAkteurVertrag<Kasse> { OneOf<Eroeffne> Auf(Eroeffnet e); }";
        (await Ids(vertrag)).Should().BeEmpty("ohne Clients ist es ein Entwurf ohne Software-Sicht");
        var befunde = await Befunde(vertrag + "public interface IAnzeige : IClientVertrag { void Auf(Eroeffnet e); }");
        befunde.Should().ContainSingle().Which.GetMessage().Should().StartWith("'IKasse': die Zusage Auf(Eroeffnet) → Eroeffne");
        (await Ids(vertrag + "public interface ILaden : IClientVertrag, IKasse { }")).Should().BeEmpty();
    }

    [Fact]
    public async Task Projektionen_die_sich_ueber_transiente_Events_wecken_sind_ein_Kreis()
    {
        var befunde = await Befunde(@"
public record Ping() : ITransientEvent; public record Pong() : ITransientEvent;
public sealed class A : ISubscriber { public string SubscriberId => ""a""; public OneOf<Pong> Handle(Ping e, IAggregateEnvelope env) => default!; }
public sealed class B : ISubscriber { public string SubscriberId => ""b""; public OneOf<Ping> Handle(Pong e, IAggregateEnvelope env) => default!; }");
        befunde.Should().ContainSingle().Which.Id.Should().Be(GesamtbildAnalyzer.ZyklusId);
        befunde[0].GetMessage().Should().Contain("A → B");
    }
}
