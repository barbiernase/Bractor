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
/// CQRS107 (Plan Pub/Sub-Neustrukturierung §2.9, E10): der Lebenszyklus (<c>ILebenszyklus</c>) gehört an eine Funktions-Klasse im Host — nicht an eine
/// Schnittstelle (draußen gehört er dem Worker), nicht an einen Dienst; <c>VorDemSichernAsync</c> nur mit Zustand. Dazu: eine Funktion mit Lebenszyklus
/// ist nie gleichbleibend (Generator).
/// </summary>
public class LebenszyklusAnalyzerTests
{
    private const string Vorspann = """
        using System; using System.Threading; using System.Threading.Tasks; using Abstractions;
        namespace Probe {
        public sealed record Gemessen(double Wert) : IEvent;
        public sealed record Auffaellig(double Wert) : ITransientEvent;
        public sealed class Netz { public double Gewicht { get; set; } }
        public interface IGpu : IDienst { Task<double> LadeAsync(); }
        """;

    private static async Task<(string Id, string Text)[]> Befunde(string code)
    {
        _ = typeof(ILebenszyklus);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Pipeline", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new LebenszyklusAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => (d.Id, d.GetMessage())).ToArray();
    }

    [Fact]
    public async Task An_einer_Funktions_Klasse_mit_Zustand_ist_der_Lebenszyklus_erlaubt()
    {
        (await Befunde("""
            public sealed class Klassifikator(IZustand<Netz> netz, IGpu gpu) : IFunktion, ILebenszyklus
            {
                public async Task BeimAktivierenAsync(CancellationToken ct) => netz.Wert.Gewicht = await gpu.LadeAsync();
                public Task VorDemSichernAsync(CancellationToken ct) => Task.CompletedTask;
                public Auffaellig? Auf(Gemessen e) => null;
            }
            public sealed class Laden(IGpu gpu) : IFunktion, ILebenszyklus
            {
                public Task BeimAktivierenAsync(CancellationToken ct) => gpu.LadeAsync();
                public Auffaellig? Auf(Gemessen e) => null;
            }
            """)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("public interface IDraussen : IFunktion, ILebenszyklus { Auffaellig? Auf(Gemessen e); }", "Schnittstelle")]
    [InlineData("public sealed class Gpu : IGpu, ILebenszyklus { public Task<double> LadeAsync() => Task.FromResult(1.0); }", "Funktions-Klasse")]
    [InlineData("public sealed class OhneZustand : IFunktion, ILebenszyklus { public Task VorDemSichernAsync(CancellationToken ct) => Task.CompletedTask; public Auffaellig? Auf(Gemessen e) => null; }", "nie gerufen")]
    public async Task Lebenszyklus_am_falschen_Ort_ist_CQRS107(string code, string worum)
    {
        (await Befunde(code)).Should().ContainSingle(b => b.Id == LebenszyklusAnalyzer.Id).Which.Text.Should().Contain(worum);
    }
}
