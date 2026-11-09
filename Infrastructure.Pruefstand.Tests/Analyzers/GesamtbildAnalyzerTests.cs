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
/// CQRS084–086, CQRS108, CQRS116/117 — Regeln über das Gesamtbild der Domänen-Projekte, geprüft in der Laufzeit-Compilation (Werkbank P7.4): die Domäne liegt
/// dort nur als Metadaten-Referenz vor — genau wie hier (die Probe wird übersetzt und als Referenz einer leeren „Laufzeit“ gelesen).
/// </summary>
public class GesamtbildAnalyzerTests
{
    private static async Task<Diagnostic[]> Befunde(string domaene, string? katalog = null)
    {
        _ = typeof(Abstractions.IReader);
        // Nur BCL + Vertrag: die echten Domänen-Assemblies im Test-Prozess gehören nicht ins Bild der Probe.
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => a.GetName().Name is { } n && (n == "Abstractions" || n == "netstandard" || n == "mscorlib" || n.StartsWith("System")))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        if (katalog is not null)
        {
            // Ein Funktionskatalog als eigene Assembly (<Name>.Katalog): die Domänen-Probe nutzt ihn wie die Abläufe.
            var kat = CSharpCompilation.Create("Rechen.Katalog", new[] { CSharpSyntaxTree.ParseText("using System; using Abstractions;\n" + katalog) }, refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var katBild = new MemoryStream();
            kat.Emit(katBild).Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("der Katalog muss kompilieren");
            refs = refs.Add(MetadataReference.CreateFromImage(katBild.ToArray()));
        }
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
public sealed record Kunde : IAkteur, IDarf<Eroeffne>, IDarf<Hole>;
";

    private static async Task<string[]> Ids(string domaene) => (await Befunde(domaene)).Select(d => d.Id).ToArray();

    // Ein Graph liegt im Katalog und ist domänenneutral (docs/04 §4.4): bindet die Domäne ihn an (hier gibt ein Akteur an seinen Kopf),
    //   braucht jeder Command ohne Ziel, der ihn verlässt, einen Empfänger — eine Übersetzung der Domäne, die seinen Typ nimmt, oder den Kopf eines Graphen.
    private const string Rechenkatalog = """
        namespace Rechen.Katalog {
        public record Rechne(int N) : ICommand;
        public record Gerechnet(Guid Bezug, int N) : ICommand;
        public sealed class Rechner : IFunktion { public Gerechnet Auf(Rechne r) => default!; }
        public sealed record Kette([Kopf] Rechner R) : IGraph;
        }
        """;

    [Fact]
    public async Task Ein_angebundener_Katalog_Graph_ohne_Uebersetzung_seines_Ausgangs_ist_CQRS120()
    {
        var befunde = await Befunde("public sealed record Nutzer : IAkteur, IDarf<Rechen.Katalog.Rechne>;", katalog: Rechenkatalog);
        befunde.Where(d => d.Id == GesamtbildAnalyzer.VerlaesstId).Select(d => d.GetMessage()).Should().ContainSingle()
            .Which.Should().StartWith("'Kette'").And.Contain("Gerechnet").And.Contain("IUebersetzung<Gerechnet, …>");
    }

    [Fact]
    public async Task Eine_Uebersetzung_der_Domaene_nimmt_den_Ausgang_eines_Katalog_Graphen_nach_seinem_Typ()
    {
        var befunde = await Befunde("""
            public sealed record Nutzer : IAkteur, IDarf<Rechen.Katalog.Rechne>;
            [Feld(nameof(Eroeffne.AggregateId), nameof(Rechen.Katalog.Gerechnet.Bezug))] public sealed record Vermerken : IUebersetzung<Rechen.Katalog.Gerechnet, Eroeffne>;
            """, katalog: Rechenkatalog);
        befunde.Where(d => d.Id is GesamtbildAnalyzer.EmpfaengerId or GesamtbildAnalyzer.VerlaesstId).Should().BeEmpty(
            "Gerechnet nimmt die Übersetzung Vermerken — der Graph bleibt neutral, die Domäne macht ihn spezifisch");
    }

    [Fact]
    public async Task Ein_Graph_im_Katalog_ohne_Anbindung_ist_eine_Blaupause()
        => (await Befunde("", katalog: Rechenkatalog)).Where(d => d.Id is GesamtbildAnalyzer.EmpfaengerId or GesamtbildAnalyzer.VerlaesstId)
            .Should().BeEmpty("niemand gibt an seinen Kopf, keine Übersetzung nimmt seinen Ausgang — er läuft nicht");

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

    // GR-ANTWORT (docs/04 §4.4): ein Event erreicht eine Funktion nur als Antwort auf einen eigenen Command.
    [Fact]
    public async Task Eine_Antwort_die_kein_eigener_Command_entscheiden_kann_ist_CQRS108()
    {
        (await Ids("public record Anstoss(Id<Konto> Konto) : ICommand; public sealed class Kette : IFunktion { public Eroeffne Auf(Anstoss a) => default!; "
                   + "public IEnumerable<Eroeffne> Auf(Eroeffnet e) => []; } public sealed record G([Kopf] Kette K) : IGraph;"))
            .Should().NotContain(GesamtbildAnalyzer.AntwortOhneCommandId, "Eroeffne kann Eroeffnet entscheiden");
        var befunde = await Befunde("public record Anstoss(Id<Konto> Konto) : ICommand; public record Anderes() : IEvent; "
                                    + "public sealed class Lauscher : IFunktion { public Eroeffne Auf(Anstoss a) => default!; public IEnumerable<Eroeffne> Auf(Anderes e) => []; } "
                                    + "public sealed record G([Kopf] Lauscher L) : IGraph;");
        befunde.Should().Contain(d => d.Id == GesamtbildAnalyzer.AntwortOhneCommandId)
            .Which.GetMessage().Should().StartWith("Die Funktion 'Lauscher' hört 'Anderes'").And.Contain("Eroeffne");
    }

    // R4 (docs/03 §3.2, docs/10 §10.6): eine Query hat genau ein Ziel — einen Reader oder ein Aggregat (Frage im Decider).
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
        var befunde = await Befunde("public record Tot() : IEvent; public record Lose() : IQueryResponse;");
        befunde.Select(d => d.Id).Should().Equal(GesamtbildAnalyzer.AusgangOffenId, GesamtbildAnalyzer.AusgangOffenId);
        befunde.Select(d => d.GetMessage()).Should().Contain(m => m.StartsWith("'Tot' (Event)")).And.Contain(m => m.StartsWith("'Lose' (Response)"));
    }

    // GR-EMPFAENGER (Entscheidung 2): ein Command ohne Ziel, der seinen Erzeuger verlässt, nimmt genau ein Graph am Kopf.
    private const string Verteilt = @"
public record Pruefe(Id<Konto> Konto) : ICommand;
public record Los() : ICommand;
public sealed class Pruefung : IFunktion { public Eroeffne Auf(Pruefe p) => default!; }
public sealed record Pruefgraph([Kopf] Pruefung P) : IGraph;
public sealed class Anstoss : IFunktion { public Pruefe Auf(Los l) => default!; }
public sealed record Anstossgraph([Kopf] Anstoss A) : IGraph;
";

    [Fact]
    public async Task Ein_Command_ohne_Ziel_hat_genau_einen_Empfaenger_den_Kopf_eines_Graphen_CQRS116()
    {
        (await Ids(Verteilt)).Should().BeEmpty("Pruefe verlässt den Anstoßgraphen und nimmt genau der Prüfgraph am Kopf");
        var zwei = await Befunde(Verteilt + "public sealed class Zweite : IFunktion { public Eroeffne Auf(Pruefe p) => default!; } public sealed record Zweitgraph([Kopf] Zweite Z) : IGraph;");
        zwei.Should().ContainSingle(d => d.Id == GesamtbildAnalyzer.EmpfaengerId).Which.GetMessage().Should().StartWith("'Pruefe'").And.Contain("Anstossgraph.A")
            .And.Contain("mehrere nehmen ihn").And.Contain("Pruefgraph").And.Contain("Zweitgraph");
        var keiner = await Befunde("public record Weiter() : ICommand; public record Los() : ICommand; "
                                   + "public sealed class Start : IFunktion { public Weiter Auf(Los l) => default!; } public sealed record Losgraph([Kopf] Start L) : IGraph;");
        keiner.Where(d => d.Id == GesamtbildAnalyzer.EmpfaengerId).Select(d => d.GetMessage()).Should().Contain(m => m.Contains("kein Graph nimmt ihn am Kopf"));
    }

    // Arbeit (Entscheidung 13): ein Akteur, der einen Command ohne Ziel hört, ist sein Empfänger.
    private const string Melder = @"
public record Pruefe(Id<Konto> Konto) : ICommand;
public sealed record Melder : IAkteur, IDarf<Pruefe>;
";

    [Fact]
    public async Task Ein_Command_ohne_Ziel_eines_Akteurs_nimmt_genau_ein_Graph_am_Kopf_CQRS116()
    {
        var ohne = await Befunde(Melder);
        ohne.Where(d => d.Id == GesamtbildAnalyzer.EmpfaengerId).Select(d => d.GetMessage())
            .Should().ContainSingle().Which.Should().StartWith("'Pruefe'").And.Contain("Akteur Melder").And.Contain("kein Graph nimmt ihn am Kopf");
        (await Ids(Melder + "public sealed class Pruefung : IFunktion { public Eroeffne Auf(Pruefe p) => default!; } "
                          + "public sealed record Pruefgraph([Kopf] Pruefung P) : IGraph;"))
            .Should().NotContain(GesamtbildAnalyzer.EmpfaengerId, "der Kopf des Pruefgraphs ist sein Empfänger");
    }

    [Fact]
    public async Task Innerhalb_eines_Graphen_ist_die_Kante_der_Empfaenger_eine_Funktion_ohne_Graph_eine_Warnung_CQRS117()
    {
        (await Ids("public record Weiter() : ICommand; public record Los() : ICommand; public sealed class Start : IFunktion { public Weiter Auf(Los l) => default!; } "
                   + "public sealed class Dann : IFunktion { public Eroeffne Auf(Weiter w) => default!; } public sealed record Kette([Kopf] Start L, [Von(nameof(L))] Dann D) : IGraph;"))
            .Should().BeEmpty("Weiter nimmt ein Knoten desselben Graphen");
        (await Befunde("public record Los() : ICommand; public sealed class Allein : IFunktion { public Eroeffne Auf(Los l) => default!; }"))
            .Should().ContainSingle().Which.Id.Should().Be(GesamtbildAnalyzer.OhneGraphId);
    }
}
