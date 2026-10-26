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
/// CQRS084–087 — Regeln über das Gesamtbild der Domänen-Projekte, geprüft in der Laufzeit-Compilation (Werkbank P7.4): die Domäne liegt
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
public record Eroeffne(Id<Konto> AggregateId) : ICommand<Konto>;
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
        => (await Befunde("public record Schliesse(Id<Konto> AggregateId) : ICommand<Konto>;")).Should().ContainSingle()
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
    public async Task Eine_Funktion_ohne_Umsetzung_und_ohne_Traeger_ist_eine_Warnung()
    {
        const string funktion = "public interface IRechnen : IFunktion { OneOf<Eroeffne> Auf(Eroeffnet e); }";
        var befunde = await Befunde(funktion);
        befunde.Should().ContainSingle().Which.Id.Should().Be(GesamtbildAnalyzer.GetragenId);
        befunde[0].GetMessage().Should().StartWith("'IRechnen'");
        (await Ids(funktion + "public interface IKasse : IClientVertrag, ITraegt<IRechnen> { }")).Should().BeEmpty("ein Client trägt sie");
        (await Ids(funktion + "public sealed class Rechnen : IRechnen { public OneOf<Eroeffne> Auf(Eroeffnet e) => default!; }"))
            .Should().BeEmpty("sie hat eine Umsetzung im Host");
    }
}
