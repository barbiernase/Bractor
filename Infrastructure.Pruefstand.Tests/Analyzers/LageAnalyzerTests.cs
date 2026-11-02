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

/// <summary>CQRS098: jede Primitive im Projekt ihrer Rolle (docs/07 §7.2) — die Rolle folgt aus dem Projektnamen (Wurzel + Suffix).</summary>
public class LageAnalyzerTests
{
    private static async Task<string[]> Befunde(string projekt, string code)
    {
        _ = typeof(IState);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create(projekt, new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new LageAnalyzer())).GetAnalyzerDiagnosticsAsync();
        ds.Should().OnlyContain(d => d.Id == LageAnalyzer.Id);
        return ds.Select(d => d.GetMessage()).ToArray();
    }

    private const string Vorspann = """
        using System; using System.Threading.Tasks; using Abstractions;
        namespace Probe {
        """;

    private const string Bausteine = """
        public record Ueberfaellig(int Tage) : ICommand;
        public record Gemahnt(int Stufe) : ICommand;
        public sealed class Mahnwesen : IFunktion { public Gemahnt Auf(Ueberfaellig e) => default!; }
        public sealed record Mahnablauf([Kopf] Mahnwesen M) : IGraph;
        """;

    [Fact]
    public async Task Funktionen_Graphen_und_Commands_zwischen_Knoten_gehoeren_in_die_Ablaeufe()
    {
        (await Befunde("Leihwesen", Bausteine)).Should().HaveCount(4, "Funktion, Graph und die Commands, die nur Knoten einander geben")
            .And.Contain(b => b.Contains("'Mahnwesen' ist eine Funktion"))
            .And.Contain(b => b.Contains("'Mahnablauf' ist ein Graph"))
            .And.Contain(b => b.Contains("'Gemahnt' ist ein Command zwischen Knoten"));
        (await Befunde("Leihwesen.Pipeline", Bausteine)).Should().BeEmpty();
    }

    [Fact]
    public async Task Leseseite_und_Akteure_haben_ihre_Projekte()
    {
        const string code = """
            public record Zeige() : IQuery;
            public record Antwort() : IQueryResponse;
            public sealed record Leser : IAkteur, IDarf<Zeige>;
            public sealed record Theke : IClient, IVerkoerpert<Leser>;
            """;
        var befunde = await Befunde("Leihwesen.Projections", code);
        befunde.Should().HaveCount(2);
        befunde.Should().Contain(b => b.Contains("'Leser' ist ein Akteur") && b.Contains("Leihwesen.Akteure"));
        befunde.Should().Contain(b => b.Contains("'Theke' ist ein Client") && b.Contains("Leihwesen.Akteure"));
        (await Befunde("Leihwesen.Akteure", "public sealed record Leser : IAkteur, IDarf<IDisposable>; public sealed record Theke : IClient, IVerkoerpert<Leser>;"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Die_Tabelle_kennt_Wurzel_und_Rolle()
    {
        Projektstruktur.Zerlege("Domain.Pipeline").Should().Be(("Domain", Projektstruktur.Rolle.Ablaeufe));
        Projektstruktur.Zerlege("Domain").Should().Be(("Domain", Projektstruktur.Rolle.Schreibseite));
        Projektstruktur.Zerlege("Pruefdomaene.Projections").Should().Be(("Pruefdomaene", Projektstruktur.Rolle.Leseseite));
        Projektstruktur.Projekte("Domain").Should().Equal("Domain", "Domain.Projections", "Domain.Pipeline", "Domain.Akteure", "Domain.Infrastructure",
            "Domain.Leitung", "Domain.Laufzeit");
        Projektstruktur.CodegenProjekte("Domain").Should().Equal("Domain", "Domain.Projections", "Domain.Pipeline", "Domain.Akteure");
        Projektstruktur.HandgeschriebeneProjekte("Domain").Should().Equal("Domain", "Domain.Projections", "Domain.Pipeline", "Domain.Akteure",
            "Domain.Infrastructure");   // der Werkbank-Stand: ohne Leitung und Komposition (nur Generate)
        Projektstruktur.Datei(Projektstruktur.Primitive.Decider, "Konto").Should().Be("Konto.Decider.cs");
        Projektstruktur.Datei(Projektstruktur.Primitive.Funktion, "Rechnen").Should().Be("Rechnen.cs", "je Funktion eine eigene Datei");
        Projektstruktur.Datei(Projektstruktur.Primitive.Client, "Theke").Should().Be("Clients.cs");
        Projektstruktur.BereichAus("Domain.Pipeline", "Domain.Pipeline.ImagePair").Should().Be("ImagePair");
        Projektstruktur.BereichAus("Domain.Pipeline", "Domain.ImagePair").Should().BeNull();
    }

    [Fact]
    public void Ort_Datei_und_Namespace_folgen_der_Tabelle()
    {
        Projektstruktur.PruefeOrt("Domain", "ImagePair/Events.cs", "Domain.ImagePair", Projektstruktur.Primitive.Event, "X", null).Should().BeEmpty();
        Projektstruktur.PruefeOrt("Domain", "ImagePair/ImagePair.Decider.cs", "Domain.ImagePair", Projektstruktur.Primitive.Decider, "Decider", "ImagePair")
            .Should().BeEmpty();
        Projektstruktur.PruefeOrt("Domain", "ImagePair/Decider.cs", "Domain.ImagePair", Projektstruktur.Primitive.Decider, "Decider", "ImagePair")
            .Should().ContainSingle().Which.Should().Contain("ImagePair.Decider.cs");
        Projektstruktur.PruefeOrt("Domain.Infrastructure", "ImagePair/ImagePairStore.Lesen.cs", "Domain.Infrastructure.ImagePair",
            Projektstruktur.Primitive.StoreImplementierung, "ImagePairStore", null).Should().BeEmpty("Partial-Teile <Typ>.<Teil>.cs");
        Projektstruktur.PruefeOrt("Domain", "Basis.cs", "Domain", Projektstruktur.Primitive.Hilfscode, "Basis", null)
            .Should().ContainSingle().Which.Should().Contain("<Bereich>");
        Projektstruktur.PruefeOrt("Domain.Akteure", "Clients.cs", "Domain.Clients", Projektstruktur.Primitive.Client, "X", null)
            .Should().ContainSingle().Which.Should().Contain("Domain.Akteure");
    }
}
