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

    private const string Fluss = """
        public record Gebucht(Guid Id) : IEvent;
        public sealed class Buchungslauf : IPipeline { public PipelineFluss Fluss => PipelineFluss.Definiere(p => { var g = p.Auf<Gebucht>(); }); }
        """;

    private const string Funktion = """
        public sealed record Rechne(int Wert) : IAuftrag<IRechnen>;
        public record Gerechnet(int Wert) : IEvent;
        public interface IRechnen : IFunktion { Task<OneOf<Gerechnet>> RufeAsync(Rechne auftrag, IAusfuehrung x); }
        """;

    [Fact]
    public async Task Ein_Fluss_gehoert_in_die_Ablaeufe()
    {
        (await Befunde("Leihwesen.Pipeline", Fluss)).Should().ContainSingle()
            .Which.Should().Contain("'Gebucht' ist ein Event").And.Contain("Leihwesen, nicht nach Leihwesen.Pipeline");
        (await Befunde("Leihwesen", Fluss)).Should().ContainSingle()
            .Which.Should().Contain("'Buchungslauf' ist ein Fluss").And.Contain("gehört nach Leihwesen.Pipeline");
    }

    [Fact]
    public async Task Ergebnis_Events_einer_Funktion_gehoeren_zur_Funktion()
    {
        (await Befunde("Leihwesen.Pipeline", Funktion)).Should().BeEmpty();
        (await Befunde("Leihwesen", Funktion)).Should().HaveCount(3, "Auftrag, Ergebnis-Event und Schnittstelle gehören nach Leihwesen.Pipeline");
    }

    [Fact]
    public async Task Leseseite_und_Akteure_haben_ihre_Projekte()
    {
        const string code = """
            public record Zeige() : IQuery;
            public record Antwort() : IQueryResponse;
            public sealed record Leser : IMensch, IDarf<Zeige>;
            """;
        var befunde = await Befunde("Leihwesen.Projections", code);
        befunde.Should().ContainSingle().Which.Should().Contain("'Leser' ist ein Akteur").And.Contain("Leihwesen.Akteure");
        (await Befunde("Leihwesen.Akteure", "public sealed record Leser : IMensch, IDarf<IDisposable>;")).Should().BeEmpty();
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
        Projektstruktur.Datei(Projektstruktur.Primitive.Auftrag, "Rechne").Should().Be("Auftraege.cs");
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
        Projektstruktur.PruefeOrt("Domain.Akteure", "Clients.cs", "Domain.Clients", Projektstruktur.Primitive.ClientVertrag, "IX", null)
            .Should().ContainSingle().Which.Should().Contain("Domain.Akteure");
    }
}
