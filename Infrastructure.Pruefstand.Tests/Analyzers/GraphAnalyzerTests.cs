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
/// CQRS115 (GR-GRAPH, GR-KANTE, GR-KOPF, GR-JOIN): die Form eines Graphen aus der einen Lesart (<c>Graphform</c>) — jede innere Kante als
/// <c>[Von(nameof(…))]</c> am nehmenden Knoten, jeder Kopf als <c>[Kopf]</c> (je Eingangstyp genau einer), eine Funktion darf mehrfach stehen,
/// kreisfrei über Knoten; ein Graph im Graphen als Schachtel mit Signatur; die Ports eines Joins auf einer Ebene und mit der Vielfachheit aus den Typen
/// (Entscheidung 5), ein Typ höchstens einmal je Auf; ein falscher Port mit Vorschlag im Befund.
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
    public async Task Fork_Join_hinter_derselben_Wahl_ist_still()
        => (await Befunde("""
            public sealed class Zusammen : IFunktion { public Buche Auf(Klein k, Kontrast c, Bild b) => default!; }
            public sealed record Aufbereitung([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(L))] Gleiche G,
                [Von(nameof(V), nameof(G), nameof(L))] Zusammen M) : IGraph;
            """)).Should().BeEmpty("Klein, Kontrast und Bild hängen alle am selben Fall BildGelesen — nach der Gabelung liegt keine Wahl");

    [Fact]
    public async Task Ein_T_Port_hinter_einer_Wahl_ist_ein_Fehler_mit_Vorschlag()
    {
        const string wahl = """
            public record Links(int N) : ICommand;
            public record Rechts(int N) : ICommand;
            public sealed class Waehle : IFunktion { public OneOf<Links, Rechts> Auf(Bild b) => default!; }
            public sealed class AusLinks : IFunktion { public Klein Auf(Links l) => default!; }
            public sealed record Wahl([Kopf] Lies L, [Von(nameof(L))] Waehle W, [Von(nameof(W))] AusLinks A, [Von(nameof(L))] Gleiche G,
                [Von(nameof(A), nameof(G))] Zusammen Z) : IGraph;
            """;
        var befunde = await Befunde(wahl + "public sealed class Zusammen : IFunktion { public Buche Auf(Klein k, Kontrast c) => default!; }");
        befunde.Should().ContainSingle().Which.Should().Contain("'Klein k' in Z").And.Contain("hinter einer Wahl").And.Contain("nimm 'Klein? k'");
        (await Befunde(wahl + "public sealed class Zusammen : IFunktion { public Buche Auf(Klein? k, Kontrast c) => default!; }"))
            .Should().BeEmpty("T? wartet, bis feststeht, dass keiner kommt");
    }

    [Fact]
    public async Task Je_und_Sammeln_auf_den_richtigen_Ebenen_ist_still_sonst_ein_Fehler_mit_Vorschlag()
    {
        const string kacheln = """
            public sealed record Kacheln([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(L))] Kachle K, [Von(nameof(K))] Pruefe P,
                [Von(nameof(P), nameof(V))] Befunde B) : IGraph;
            """;
        (await Befunde(kacheln + "public sealed class Befunde : IFunktion { public Buche Auf(IReadOnlyList<Befund> alle, Klein k) => default!; }"))
            .Should().BeEmpty("die Liste sammelt die Unterläufe der Kacheln eine Ebene hinauf — dort liegt auch Klein");
        (await Befunde(kacheln + "public sealed class Befunde : IFunktion { public Buche Auf(Befund b, Klein k) => default!; }"))
            .Should().ContainSingle().Which.Should().Contain("verschiedenen Ebenen").And.Contain("'IReadOnlyList<Befund> b'");
        (await Befunde("""
            public sealed class Befunde : IFunktion { public Buche Auf(IReadOnlyList<Klein> alle) => default!; }
            public sealed record Kacheln([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(V))] Befunde B) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("kommt aus keinem Strom").And.Contain("nimm 'Klein alle'");
    }

    [Fact]
    public async Task Ein_Rennen_nimmt_den_ersten_von_mehreren_Faellen()
        => (await Befunde("""
            public sealed class Erster : IFunktion { public Buche Auf(OneOf<Klein, Kontrast> wer) => default!; }
            public sealed record Rennen([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(L))] Gleiche G, [Von(nameof(V), nameof(G))] Erster E) : IGraph;
            """)).Should().BeEmpty();

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
        // Ein Join halb von außen paart nie.
        (await Befunde("""
            public sealed class Halb : IFunktion { public Buche Auf(Klein k, Kachel x) => default!; }
            public sealed record Halbjoin([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Kopf, Von(nameof(V))] Halb H) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("'x' in H kommt von außen ([Kopf])").And.Contain("ein Join paart nur");
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
    public async Task Eine_genannte_Quelle_die_keinen_Port_speist_ist_ein_Fehler()
        => (await Befunde("public sealed record Leer([Kopf] Lies L, [Von(nameof(L))] Verkleinere V, [Von(nameof(L), nameof(V))] Gleiche G) : IGraph;"))
            .Should().ContainSingle().Which.Should().Contain("'G' nennt in [Von] 'V', aber V gibt nichts aus, was G nimmt (V: Klein; G: Bild)");

    [Fact]
    public async Task Ein_Port_ohne_Quelle_an_einem_Knoten_ohne_Kopf_ist_ein_Fehler()
        => (await Befunde("""
            public sealed class Zusammen : IFunktion { public Buche Auf(Klein k, Kontrast c) => default!; }
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

    // ── GR-JOIN: ein Typ höchstens einmal je Auf; zwei Eingänge von außen treffen sich nie ──

    [Fact]
    public async Task Ein_Typ_zweimal_in_einem_Auf_ist_ein_Fehler_mit_Vorschlag()
        => (await Befunde("""
            public sealed class Vergleiche : IFunktion { public Buche Auf(Bild links, Bild rechts) => default!; }
            public sealed record Vergleich([Kopf] Lies L, [Von(nameof(L))] Vergleiche C) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("nimmt 'Bild' zweimal in einem Auf ('links', 'rechts')").And.Contain("'BildLinks'");

    [Fact]
    public async Task Zwei_Erzeuger_an_einem_T_Port_ohne_Wahl_dazwischen_sind_ein_Fehler()
    {
        const string zwei = """
            public sealed record Doppelt([Kopf] Lies L, [Von(nameof(L))] Verkleinere Vorschau, [Von(nameof(L))] Verkleinere Daumen,
                [Von(nameof(Vorschau), nameof(Daumen))] Nimm N) : IGraph;
            """;
        (await Befunde(zwei + "public sealed class Nimm : IFunktion { public Buche Auf(Klein k) => default!; }"))
            .Should().ContainSingle().Which.Should().Contain("'Klein k' in N bekommt in einem Lauf zwei Werte (von Vorschau und Daumen")
            .And.Contain("nimm einen Knoten je Quelle: 'NVorschau' mit [Von(nameof(Vorschau))] und 'NDaumen' mit [Von(nameof(Daumen))]")
            .And.Contain("zwei Ports (Join)").And.Contain("OneOf<…> (Rennen)");
        (await Befunde("""
            public sealed class Nimm : IFunktion { public Buche Auf(Klein k) => default!; }
            public sealed record Getrennt([Kopf] Lies L, [Von(nameof(L))] Verkleinere Vorschau, [Von(nameof(L))] Verkleinere Daumen,
                [Von(nameof(Vorschau))] Nimm NVorschau, [Von(nameof(Daumen))] Nimm NDaumen) : IGraph;
            """)).Should().BeEmpty("der Vorschlag aus dem Befund baut: je Quelle ein eigener Knoten derselben Funktion");
        (await Befunde(zwei + "public sealed class Nimm : IFunktion { public Buche Auf(Klein? k) => default!; }"))
            .Should().ContainSingle("auch T? nimmt höchstens einen").Which.Should().Contain("'Klein? k' in N bekommt in einem Lauf zwei Werte");
    }

    [Fact]
    public async Task Zusammenfliessen_hinter_einer_Wahl_ist_still_ein_Join_ohne_Fall_trennt_nicht()
    {
        const string wahl = """
            public record Links(int N) : ICommand;
            public record Rechts(int N) : ICommand;
            public sealed class Waehle : IFunktion { public OneOf<Links, Rechts> Auf(Bild b) => default!; }
            public sealed class AusLinks : IFunktion { public Klein Auf(Links l) => default!; }
            public sealed class AusRechts : IFunktion { public Klein Auf(Rechts r) => default!; }
            public sealed class AusBeiden : IFunktion { public Klein Auf(Links? l, Rechts? r) => default!; }
            """;
        (await Befunde(wahl + """
            public sealed record Zusammen([Kopf] Lies L, [Von(nameof(L))] Waehle W, [Von(nameof(W))] AusLinks A, [Von(nameof(W))] AusRechts R,
                [Von(nameof(A), nameof(R))] Melde M) : IGraph;
            """)).Should().BeEmpty("A braucht den Fall Links, R den Fall Rechts — im selben Lauf kommt nur einer an");
        (await Befunde(wahl + """
            public sealed record Zusammen([Kopf] Lies L, [Von(nameof(L))] Waehle W, [Von(nameof(W))] AusLinks A, [Von(nameof(W))] AusBeiden B,
                [Von(nameof(A), nameof(B))] Melde M) : IGraph;
            """)).Should().ContainSingle("B läuft in jedem Fall der Wahl, auch neben A").Which.Should().Contain("zwei Werte (von A und B");
    }

    [Fact]
    public async Task Zwei_Eingaenge_von_aussen_treffen_sich_nie()
        => (await Befunde("""
            public sealed class Beide : IFunktion { public Buche Auf(Klein k, Kachel x) => default!; }
            public sealed record Draussen([Kopf] Beide B) : IGraph;
            """)).Should().ContainSingle().Which.Should().Contain("kommen alle von außen").And.Contain("Vorgangs-Aggregat");
}
