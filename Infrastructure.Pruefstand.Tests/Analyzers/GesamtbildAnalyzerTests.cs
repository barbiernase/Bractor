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
        _ = typeof(Abstractions.IReader);
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
public sealed class Uebersicht : IProjektion { public Task Auf(Eroeffnet e, Herkunft h) => Task.CompletedTask; }
public record Hole() : IQuery;
public record Teil(int Wert) : IQueryResponse;
public record Antwort(IReadOnlyList<Teil> Teile) : IQueryResponse;
public sealed class Leser : IReader { public OneOf<Antwort> Frage(Hole q) => default!; }
public sealed record Kunde : IMensch, IDarf<Eroeffne>, IDarf<Hole>;
";

    private static async Task<string[]> Ids(string domaene) => (await Befunde(domaene)).Select(d => d.Id).ToArray();

    [Fact]
    public async Task Eine_geschlossene_Domaene_ist_still()
        => (await Ids("")).Should().BeEmpty("jede Nachricht hat Erzeuger und Konsument; Teil lebt als Feld der Antwort");

    [Fact]
    public async Task Eine_Query_mit_zwei_Readern_ist_ein_Fehler_ohne_Reader_ein_offener_Eingang()
    {
        var befunde = await Befunde("public sealed class Zweiter : IReader { public OneOf<Antwort> Frage(Hole q) => default!; }"
                                    + "public record Vergessen() : IQuery;");
        befunde.Select(d => d.Id).Should().BeEquivalentTo([GesamtbildAnalyzer.QueryId, GesamtbildAnalyzer.EingangOffenId]);
        befunde.Single(d => d.Id == GesamtbildAnalyzer.QueryId).GetMessage().Should().StartWith("'Hole'").And.Contain("Leser").And.Contain("Zweiter");
        befunde.Single(d => d.Id == GesamtbildAnalyzer.EingangOffenId).GetMessage().Should().StartWith("'Vergessen'");
        befunde.Should().OnlyContain(d => d.Location == Location.None, "die Typen liegen in der Domänen-Referenz — der Name führt zur Karte");
    }

    // R4 (Plan Pub/Sub-Neustrukturierung §2.6, §3.6): eine Query hat genau ein Ziel — einen Reader oder ein Aggregat (Frage im Decider).
    private const string Kasse = @"
public record Zaehle(Id<Kasse> AggregateId) : ICommand<Kasse>;
public record Gezaehlt() : IEvent;
public record HoleKasse(Id<Kasse> Kasse) : IQuery;
public record KassenStand(int Bestand) : IQueryResponse;
public partial class Kasse : IState
{
    public int Version { get; set; } public Guid Id { get; set; }
    public class Decider : IDecider<Kasse> { public IEnumerable<Gezaehlt> Decide(Zaehle c) => default!; public KassenStand Frage(HoleKasse q) => default!; }
    public class Applier : IApplier<Kasse> { public void Apply(Gezaehlt e) { } }
}
";

    [Fact]
    public async Task R4_eine_Query_an_ein_Aggregat_hat_ihr_Ziel_und_ihre_Antwort_einen_Erzeuger()
        => (await Ids(Kasse)).Should().BeEmpty("das Aggregat beantwortet HoleKasse (kein offener Eingang) und erzeugt KassenStand (kein offener Ausgang)");

    [Fact]
    public async Task R4_eine_Query_an_Reader_und_Aggregat_zugleich_ist_CQRS084()
    {
        var befunde = await Befunde(Kasse + "public sealed class Kassenbuch : IReader { public KassenStand Frage(HoleKasse q) => default!; }");
        befunde.Should().ContainSingle().Which.Id.Should().Be(GesamtbildAnalyzer.QueryId);
        befunde[0].GetMessage().Should().StartWith("'HoleKasse'").And.Contain("Aggregat Kasse").And.Contain("Reader Kassenbuch");
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

    [Fact]
    public async Task R20_ein_gehoerter_Hinweis_ohne_Erzeuger_im_Netz_ist_CQRS106()
    {
        // Ein Hinweis, den ein Decide (SchonOffen) oder eine Funktion ausgibt, hat einen Erzeuger — einer, den nur ein Client sendet, nicht.
        (await Ids("public sealed class Mahner : IFunktion { public Eroeffne? Auf(SchonOffen h) => null; }")).Should().BeEmpty();
        var befunde = await Befunde("public record Merkmal(double Wert) : ITransientEvent; "
                                    + "public sealed class Schaetzer : IFunktion { public Eroeffne? Auf(Merkmal m) => null; }");
        befunde.Select(d => d.Id).Should().Contain(GesamtbildAnalyzer.HinweisOhneErzeugerId);
        befunde.Single(d => d.Id == GesamtbildAnalyzer.HinweisOhneErzeugerId).GetMessage().Should().Contain("'Schaetzer'").And.Contain("'Merkmal'");
        (await Ids("public record Merkmal(double Wert) : ITransientEvent; "
                   + "public sealed class Merkmalsbildung : IFunktion { public Merkmal Auf(Eroeffnet e) => new(1); } "
                   + "public sealed class Schaetzer : IFunktion { public Eroeffne? Auf(Merkmal m) => null; }")).Should().BeEmpty("eine Funktion erzeugt ihn");
    }
}
