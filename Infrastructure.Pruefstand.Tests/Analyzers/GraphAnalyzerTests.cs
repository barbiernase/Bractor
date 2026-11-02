using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// CQRS115 (GR-GRAPH, GR-JOIN): die Form eines Graphen aus der einen Lesart (<c>Graphform</c>) — Knoten, Kanten aus den Typen, ein Kopf je Eingangstyp,
/// kreisfrei; ein Graph im Graphen als Schachtel mit Signatur; die Ports eines Joins auf einer Ebene und mit der Vielfachheit aus den Typen
/// (Entscheidung 5), ein falscher Port mit Vorschlag im Befund.
/// </summary>
public class GraphAnalyzerTests
{
    private static async Task<string[]> Befunde(string code)
    {
        _ = typeof(IGraph);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Pipeline", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GraphAnalyzer())).GetAnalyzerDiagnosticsAsync();
        ds.Should().OnlyContain(d => d.Id == GraphAnalyzer.Id);
        return ds.Select(d => d.GetMessage()).ToArray();
    }

    private const string Vorspann = """
        using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions;
        namespace Probe {
        public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
        public record Buche(Id<Konto> AggregateId) : ICommand<Konto>;
        public record Start(int N) : ICommand;
        public record Bild(int N) : ICommand;
        public record Klein(int N) : ICommand;
        public record Kontrast(int N) : ICommand;
        public record Kachel(int N) : ICommand;
        public record Befund(int N) : ICommand;
        public record Unlesbar(string Grund) : ITransientEvent;
        public sealed class Lies : IFunktion { public OneOf<Bild, Unlesbar> Auf(Start s) => default!; }
        public sealed class Verkleinere : IFunktion { public Klein Auf(Bild b) => default!; }
        public sealed class Gleiche : IFunktion { public Kontrast Auf(Bild b) => default!; }
        public sealed class Kachle : IFunktion { public IEnumerable<Kachel> Auf(Bild b) => []; }
        public sealed class Pruefe : IFunktion { public Befund Auf(Kachel k) => default!; }
        """;

    [Fact]
    public async Task Fork_Join_hinter_derselben_Wahl_ist_still()
        => (await Befunde("""
            public sealed class Melde : IFunktion { public Buche Auf(Klein k, Kontrast c, Bild b) => default!; }
            public sealed record Aufbereitung(Lies L, Verkleinere V, Gleiche G, Melde M) : IGraph;
            """)).Should().BeEmpty("Klein, Kontrast und Bild hängen alle am selben Fall BildGelesen — nach der Gabelung liegt keine Wahl");

    [Fact]
    public async Task Ein_T_Port_hinter_einer_Wahl_ist_ein_Fehler_mit_Vorschlag()
    {
        var befunde = await Befunde("""
            public record Links(int N) : ICommand;
            public record Rechts(int N) : ICommand;
            public sealed class Waehle : IFunktion { public OneOf<Links, Rechts> Auf(Bild b) => default!; }
            public sealed class AusLinks : IFunktion { public Klein Auf(Links l) => default!; }
            public sealed class Zusammen : IFunktion { public Buche Auf(Klein k, Kontrast c) => default!; }
            public sealed record Wahl(Lies L, Waehle W, AusLinks A, Gleiche G, Zusammen Z) : IGraph;
            """);
        befunde.Should().ContainSingle().Which.Should().Contain("'Klein k' in Z").And.Contain("hinter einer Wahl").And.Contain("nimm 'Klein? k'");
        (await Befunde("""
            public record Links(int N) : ICommand;
            public record Rechts(int N) : ICommand;
            public sealed class Waehle : IFunktion { public OneOf<Links, Rechts> Auf(Bild b) => default!; }
            public sealed class AusLinks : IFunktion { public Klein Auf(Links l) => default!; }
            public sealed class Zusammen : IFunktion { public Buche Auf(Klein? k, Kontrast c) => default!; }
            public sealed record Wahl(Lies L, Waehle W, AusLinks A, Gleiche G, Zusammen Z) : IGraph;
            """)).Should().BeEmpty("T? wartet, bis feststeht, dass keiner kommt");
    }

    [Fact]
    public async Task Je_und_Sammeln_auf_den_richtigen_Ebenen_ist_still_sonst_ein_Fehler_mit_Vorschlag()
    {
        (await Befunde("""
            public sealed class Befunde : IFunktion { public Buche Auf(IReadOnlyList<Befund> alle, Klein k) => default!; }
            public sealed record Kacheln(Lies L, Verkleinere V, Kachle K, Pruefe P, Befunde B) : IGraph;
            """)).Should().BeEmpty("die Liste sammelt die Unterläufe der Kacheln eine Ebene hinauf — dort liegt auch Klein");
        (await Befunde("""
            public sealed class Befunde : IFunktion { public Buche Auf(Befund b, Klein k) => default!; }
            public sealed record Kacheln(Lies L, Verkleinere V, Kachle K, Pruefe P, Befunde B) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("verschiedenen Ebenen").And.Contain("'IReadOnlyList<Befund> b'");
        (await Befunde("""
            public sealed class Befunde : IFunktion { public Buche Auf(IReadOnlyList<Klein> alle) => default!; }
            public sealed record Kacheln(Lies L, Verkleinere V, Befunde B) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("kommt aus keinem Strom").And.Contain("nimm 'Klein alle'");
    }

    [Fact]
    public async Task Ein_Rennen_nimmt_den_ersten_von_mehreren_Faellen()
        => (await Befunde("""
            public sealed class Erster : IFunktion { public Buche Auf(OneOf<Klein, Kontrast> wer) => default!; }
            public sealed record Rennen(Lies L, Verkleinere V, Gleiche G, Erster E) : IGraph;
            """)).Should().BeEmpty();

    [Fact]
    public async Task Ein_Graph_im_Graphen_ist_eine_Schachtel_mit_Signatur()
    {
        (await Befunde("""
            public sealed class Melde : IFunktion { public Buche Auf(Klein k) => default!; }
            public sealed record Vorschau(Verkleinere V) : IGraph;
            public sealed record Linie(Lies L, Vorschau Vorschau, Melde M) : IGraph;
            """)).Should().BeEmpty("Bild geht in die Schachtel hinein (ihr Eingang), Klein kommt heraus (ihr Ausgang)");
        // Was innen erzeugt UND genommen wird, sieht außen niemand: Melde bekommt Klein nicht und ist ein zweiter Kopf ohne Erzeuger.
        var befunde = await Befunde("""
            public sealed class Innen : IFunktion { public Buche Auf(Klein k) => default!; }
            public sealed class Melde : IFunktion { public Buche Auf(Klein k, Start s) => default!; }
            public sealed record Vorschau(Verkleinere V, Innen I) : IGraph;
            public sealed record Linie(Lies L, Vorschau Vorschau, Melde M) : IGraph;
            """);
        befunde.Should().Contain(b => b.Contains("Graph 'Linie'") && b.Contains("Eingang des Graphen"));
    }

    [Fact]
    public async Task Ein_Kreis_und_ein_Graph_der_sich_selbst_enthaelt_sind_Fehler()
    {
        (await Befunde("""
            public sealed class Zurueck : IFunktion { public Bild Auf(Klein k) => default!; }
            public sealed record Kreis(Lies L, Verkleinere V, Zurueck Z) : IGraph;
            """)).Should().Contain(b => b.Contains("bilden einen Kreis"));
        (await Befunde("public sealed record Selbst(Lies L, Selbst S) : IGraph;")).Should().Contain(b => b.Contains("enthält sich selbst"));
    }

    [Fact]
    public async Task Eine_Quelle_ist_der_Kopf_ihres_Graphen_und_einzige()
    {
        const string quelle = """
            public sealed class Waechter : IFunktion
            {
                public async IAsyncEnumerable<Start> Lausche([System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)
                { await Task.CompletedTask; yield break; }
            }
            """;
        (await Befunde(quelle + """
            public sealed class Melde : IFunktion { public Buche Auf(Klein k) => default!; }
            public sealed record Vorschau(Lies L, Verkleinere V) : IGraph;
            public sealed record Linie(Waechter Eingang, Vorschau Vorschau, Melde M) : IGraph;
            """)).Should().BeEmpty("der Wächter erzeugt Start, die Schachtel nimmt es; ein Graph mit Quelle braucht keinen Eingang");
        (await Befunde(quelle + """
            public sealed class Fremd : IFunktion { public Buche Auf(Kachel k) => default!; }
            public sealed record Gemischt(Waechter Eingang, Lies L, Fremd F) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("Quellknoten Eingang").And.Contain("Kachel");
    }
}
