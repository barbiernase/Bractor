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
/// CQRS115 (GR-GRAPH, GR-KANTE, GR-KOPF): die Form eines Graphen aus der einen Lesart (<c>Graphform</c>) — jede innere Kante als
/// <c>[Von(nameof(…))]</c> am nehmenden Knoten, jeder Kopf als <c>[Kopf]</c> (je Eingangstyp genau einer), eine Funktion darf mehrfach stehen,
/// kreisfrei über Knoten; ein Graph im Graphen als Schachtel mit Signatur. Dass ein Auf genau eine Nachricht nimmt, prüft die Funktionsform (CQRS100).
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
        public sealed class Melde : IFunktion { public Buche Auf(Klein k) => default!; }
        public sealed class Waechter : IFunktion
        {
            public async IAsyncEnumerable<Start> Lausche([System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)
            { await Task.CompletedTask; yield break; }
        }
        """;

    [Fact]
    public async Task Dieselbe_Funktion_darf_zweimal_im_Graphen_stehen_Vorschau_und_Thumbnail()
        => (await Befunde("""
            public sealed record Bildpaar([Kopf] Lies L, [Von(nameof(L))] Verkleinere Vorschau, [Von(nameof(L))] Verkleinere Thumbnail,
                [Von(nameof(Vorschau))] Melde MeldeVorschau, [Von(nameof(Thumbnail))] Melde MeldeThumbnail) : IGraph;
            """)).Should().BeEmpty("die Kanten stehen am nehmenden Knoten — wer von welchem Knoten bekommt, sagt Von, nicht der Typ");

    [Fact]
    public async Task Eine_Kette_mit_gleichem_Typ_hinein_und_hinaus_ist_kein_Kreis()
        => (await Befunde("""
            public sealed class Normalisiere : IFunktion { public Bild Auf(Bild b) => default!; }
            public sealed class Schaerfe : IFunktion { public Bild Auf(Bild b) => default!; }
            public sealed record Filter([Kopf] Lies L, [Von(nameof(L))] Normalisiere Vorher, [Von(nameof(Vorher))] Schaerfe S,
                [Von(nameof(S))] Normalisiere Nachher, [Von(nameof(Nachher))] Verkleinere V) : IGraph;
            """)).Should().BeEmpty("Bild → Bild → Bild: die Kreis-Prüfung läuft über die Knoten, nicht über die Typen");

    [Fact]
    public async Task Ein_Knoten_mit_Kopf_und_Von_nimmt_einen_Port_von_aussen_und_einen_von_innen()
    {
        const string sammle = "public sealed class Sammle : IFunktion { public Buche Auf(Klein k) => default!; public Buche Auf(Kachel k) => default!; }";
        (await Befunde(sammle + "public sealed record Mix([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Kopf, Von(nameof(V))] Sammle S) : IGraph;"))
            .Should().BeEmpty("Klein kommt von V, Kachel von außen — zwei Köpfe: Start an L, Kachel an S");
    }

    [Fact]
    public async Task Ein_Graph_im_Graphen_ist_eine_Schachtel_mit_Signatur()
    {
        (await Befunde("""
            public sealed record Vorschau([Kopf] Verkleinere V) : IGraph;
            public sealed record Linie([Kopf] Lies L, [Von(nameof(L))] Vorschau Vorschau, [Von(nameof(Vorschau))] Melde M) : IGraph;
            """)).Should().BeEmpty("Von(L) speist den Kopf der Schachtel (Bild), Von(Vorschau) nimmt ihren Ausgang (Klein)");
        (await Befunde("""
            public sealed record Vorschau([Kopf] Verkleinere V) : IGraph;
            public sealed record Linie([Kopf] Vorschau Vorschau, [Von(nameof(Vorschau))] Melde M) : IGraph;
            """)).Should().BeEmpty("[Kopf] an der Schachtel macht ihren Kopf (Bild) zum Kopf des äußeren Graphen");
        // Was innen erzeugt UND genommen wird, ist kein Ausgang der Schachtel: Von(Vorschau) speist M nicht.
        var befunde = await Befunde("""
            public sealed record Vorschau([Kopf] Verkleinere V, [Von(nameof(V))] Melde Innen) : IGraph;
            public sealed record Linie([Kopf] Lies L, [Von(nameof(L))] Vorschau Vorschau, [Von(nameof(Vorschau))] Melde M) : IGraph;
            """);
        befunde.Should().Contain(b => b.Contains("Graph 'Linie'") && b.Contains("nennt in [Von] 'Vorschau'") && b.Contains("speist mindestens einen Port"));
    }

    [Fact]
    public async Task Ein_Kreis_und_ein_Graph_der_sich_selbst_enthaelt_sind_Fehler()
    {
        (await Befunde("""
            public sealed class Zurueck : IFunktion { public Bild Auf(Klein k) => default!; }
            public sealed record Kreis([Kopf] Lies L, [Von(nameof(L), nameof(Z))] Verkleinere V, [Von(nameof(V))] Zurueck Z) : IGraph;
            """)).Should().Contain(b => b.Contains("bilden einen Kreis"));
        (await Befunde("public sealed record Selbst([Kopf] Lies L, [Von(nameof(L))] Selbst S) : IGraph;")).Should().Contain(b => b.Contains("enthält sich selbst"));
    }

    [Fact]
    public async Task Eine_Quelle_ist_der_Kopf_ihres_Graphen_und_einzige()
    {
        (await Befunde("""
            public sealed record Vorschau([Kopf] Lies L, [Von(nameof(L))] Verkleinere V) : IGraph;
            public sealed record Linie(Waechter Eingang, [Von(nameof(Eingang))] Vorschau Vorschau, [Von(nameof(Vorschau))] Melde M) : IGraph;
            """)).Should().BeEmpty("der Wächter speist den Kopf der Schachtel; ein Graph mit Quelle braucht keinen Kopf");
        (await Befunde("""
            public sealed class Fremd : IFunktion { public Buche Auf(Kachel k) => default!; }
            public sealed record Gemischt(Waechter Eingang, [Von(nameof(Eingang))] Lies L, [Kopf] Fremd F) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("Quellknoten Eingang").And.Contain("Kachel");
        (await Befunde("public sealed record Laut([Kopf] Waechter Eingang, [Von(nameof(Eingang))] Lies L) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("'Eingang' ist eine Quelle").And.Contain("weder [Kopf] noch [Von]");
    }

    // ── GR-GRAPH: jeder Knoten trägt [Kopf] oder [Von]; Von nennt per nameof andere Knoten desselben Records ──

    [Fact]
    public async Task Ein_Knoten_ohne_Kopf_und_ohne_Von_ist_ein_Fehler()
        => (await Befunde("public sealed record Lose([Kopf] Lies L, Verkleinere V) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("der Knoten 'V' trägt weder [Kopf] noch [Von]");

    [Fact]
    public async Task Von_nennt_per_nameof_einen_anderen_Knoten_desselben_Records_genau_einmal()
    {
        (await Befunde("""public sealed record Literal([Kopf] Lies L, [Von("L")] Verkleinere V) : IGraph;"""))
            .Should().ContainSingle().Which.Should().Contain("nennt \"L\"").And.Contain("nie ein String-Literal");
        (await Befunde("public sealed record Fremd([Kopf] Lies L, [Von(nameof(Unlesbar))] Verkleinere V) : IGraph;"))
            .Should().Contain(b => b.Contains("nennt 'Unlesbar' — kein Knoten des Graphen Fremd"));
        (await Befunde("public sealed record Selbst2([Kopf] Lies L, [Von(nameof(L), nameof(V))] Verkleinere V) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("nennt den Knoten selbst");
        (await Befunde("public sealed record Doppelt([Kopf] Lies L, [Von(nameof(L), nameof(L))] Verkleinere V) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("nennt 'L' doppelt");
    }

    // ── GR-KANTE: jede genannte Quelle speist einen Port, jeder Port hat eine Quelle oder kommt von außen ──

    [Fact]
    public async Task Eine_Kante_ist_gesichert_oder_mit_Verlierbar_im_eigenen_Von_verlierbar_eine_Quelle_nur_einmal()
    {
        (await Befunde("""
            public sealed record Gemischt([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(L))] Gleiche G,
                [Von(nameof(V))] [Von(nameof(G), Verlierbar = true)] Sammle S) : IGraph;
            public sealed class Sammle : IFunktion { public Buche Auf(Klein k) => default!; public Buche Auf(Kontrast c) => default!; }
            """)).Should().BeEmpty("Verlierbar ist ein benannter Wert, kein Knoten — und je Zustellung ein eigenes [Von]");
        (await Befunde("""
            public sealed record Zweimal([Kopf] Lies L, [Von(nameof(L))] [Von(nameof(L), Verlierbar = true)] Verkleinere V) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("nennt 'L' doppelt");
    }

    [Fact]
    public async Task Eine_genannte_Quelle_die_keinen_Port_speist_ist_ein_Fehler()
        => (await Befunde("public sealed record Leer([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(L), nameof(V))] Gleiche G) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("'G' nennt in [Von] 'V', aber V gibt nichts aus, was G nimmt (V: Klein; G: Bild)");

    [Fact]
    public async Task Ein_Port_ohne_Quelle_an_einem_Knoten_ohne_Kopf_ist_ein_Fehler()
        => (await Befunde("""
            public sealed class Zusammen : IFunktion { public Buche Auf(Klein k) => default!; public Buche Auf(Kontrast c) => default!; }
            public sealed record Halb([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(V))] Zusammen Z) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("'c' in Z (Kontrast) bekommt von keiner genannten Quelle (V) etwas");

    // ── GR-KOPF: je Eingangstyp genau ein [Kopf]; ein [Kopf] nimmt etwas von außen ──

    [Fact]
    public async Task Zwei_Koepfe_fuer_denselben_Typ_sind_ein_Fehler()
        => (await Befunde("public sealed record Zwei([Kopf] Verkleinere V, [Kopf] Gleiche G) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("'Bild' nehmen 2 Köpfe des Graphen Zwei (V, G)").And.Contain("in [Von]");

    [Fact]
    public async Task Ein_Kopf_an_dem_nichts_von_aussen_ankommt_ist_ein_Fehler()
        => (await Befunde("public sealed record Innen([Kopf] Lies L, [Kopf, Von(nameof(L))] Verkleinere V) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("'V' trägt [Kopf], aber an ihm kommt nichts von außen an");
}
