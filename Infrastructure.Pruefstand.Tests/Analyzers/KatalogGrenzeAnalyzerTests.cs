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
/// CQRS122 (GR-UEBERSETZUNG): die Domäne referenziert einen Katalog, aber nur ihre Übersetzungen nennen seine Typen — Zustand, Decide, Apply, Commands
/// an Aggregate, Events, Hinweise, Wertobjekte und Aggregat-Queries sprechen die Sprache der Domäne (docs/plan-ohne-pipeline.md).
/// </summary>
public class KatalogGrenzeAnalyzerTests
{
    private static async Task<string[]> Befunde(string domaene)
    {
        _ = typeof(IState);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var optionen = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        var katalog = CSharpCompilation.Create("Bild.Katalog", new[] { CSharpSyntaxTree.ParseText("""
            using System; using Abstractions;
            namespace Bild.Katalog.Aufbereitung {
            public record Gelesen(Guid Bild, string Pfad) : ICommand;
            public record Masse(int Breite, int Hoehe);
            }
            """) }, refs, optionen);
        katalog.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("der Katalog der Probe muss kompilieren");
        var comp = CSharpCompilation.Create("Leihwesen", new[] { CSharpSyntaxTree.ParseText("""
            using System; using System.Collections.Generic; using Abstractions; using Bild.Katalog.Aufbereitung;
            namespace Leihwesen.Bilder {
            public record NimmAuf(Id<Bild> AggregateId, string Pfad) : ICommand<Bild>;
            public record Aufgenommen(string Pfad) : IEvent;
            """ + domaene + "\n}") }, refs.Add(katalog.ToMetadataReference()), optionen);
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new KatalogGrenzeAnalyzer())).GetAnalyzerDiagnosticsAsync();
        ds.Should().OnlyContain(d => d.Id == KatalogGrenzeAnalyzer.Id);
        return ds.Select(d => d.GetMessage()).ToArray();
    }

    private const string Bild = """
        public partial class Bild : IState { public int Version { get; set; } public Guid Id { get; set; } public string Pfad { get; set; } = ""; }
        """;

    [Fact]
    public async Task Nur_die_Uebersetzung_nennt_den_Katalog()
        => (await Befunde(Bild + """
            [Feld(nameof(NimmAuf.AggregateId), nameof(Gelesen.Bild))] public sealed record Aufnehmen : IUebersetzung<Gelesen, NimmAuf>;
            public record Anfordern(string Pfad) : ICommand;
            """)).Should().BeEmpty("die Übersetzung nennt den Katalog, der Command ohne Ziel eines Clients ist frei");

    [Fact]
    public async Task Ein_Katalog_Typ_im_Event_im_Zustand_oder_im_Aggregat_Command_ist_CQRS122()
    {
        var befunde = await Befunde("""
            public partial class Bild : IState { public int Version { get; set; } public Guid Id { get; set; } public Masse? Masse { get; set; } }
            public record Vermessen(IReadOnlyList<Masse> Masse) : IEvent;
            public record Vermiss(Id<Bild> AggregateId, Gelesen Quelle) : ICommand<Bild>;
            """);
        befunde.Should().HaveCount(3);
        befunde.Should().Contain(b => b.Contains("'Bild' (ein Aggregat) nennt den Katalog-Typ Bild.Katalog.Aufbereitung.Masse aus Bild.Katalog"));
        befunde.Should().Contain(b => b.Contains("'Vermessen' (ein Event)"));
        befunde.Should().Contain(b => b.Contains("'Vermiss' (ein Command)") && b.Contains("Gelesen"));
    }
}
