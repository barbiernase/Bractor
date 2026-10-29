using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Domain.SourceGeneration;
using FluentAssertions;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Projections.SourceGeneration;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// R14 (docs/04 §4.3, docs/10 §10.6) im Build: CQRS109 — ein Store mit Schreib-Fähigkeiten implementiert genau eine Store-Klasse, und
/// jede Schreib-Fähigkeit eines Stores der Klasse 2 nimmt die Herkunft; CQRS110 — eine Projektion schreibt nur mit <c>[MindestensEinmal]</c> in einen
/// Store der Klasse 3. Dazu CQRS111 — ein Read-Model mit typisierter Id ist partial (die Speicher-Identität wird generiert).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class StoreKlasseAnalyzerTests
{
    private const string Vorspann = @"
using System; using System.Threading; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
public record Gebucht(decimal Betrag) : IEvent;
public interface ISchreibeOhne : IWriteStore { Task SchreibeAsync(Guid konto, decimal betrag); }
public interface ISchreibeMit : IWriteStore { Task SchreibeAsync(Herkunft h, decimal betrag); }
public interface IFinde : IReadStore { Task<decimal> FindeAsync(Guid konto); }
public abstract class Basis : IProjektionsZiel
{
    public void Beginne(string generation) { }
    public void Stream(Guid stream) { }
    public Task SchreibeAsync(CancellationToken ct) => Task.CompletedTask;
    public Task NeueGenerationAsync(string generation, CancellationToken ct) => Task.CompletedTask;
    public Task UmschaltenAsync(string generation, CancellationToken ct) => Task.CompletedTask;
}
}
";

    private static ImmutableArray<MetadataReference> Refs()
    {
        var noetig = new[] { typeof(Abstractions.IProjektion).Assembly, typeof(Infrastructure.Projections.Projektionsnaht).Assembly, typeof(Core.MartenStore).Assembly };
        return AppDomain.CurrentDomain.GetAssemblies().Concat(noetig).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
    }

    private static CSharpCompilation Probe(string code, string name = "StoreKlasseProbe")
    {
        var comp = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(Vorspann), CSharpSyntaxTree.ParseText("using System; using System.Threading.Tasks; using Abstractions;\n" + code) },
            Refs(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return comp;
    }

    private static async Task<string[]> Analyzer(string code)
        => (await Probe(code).WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StoreKlasseAnalyzer())).GetAnalyzerDiagnosticsAsync())
            .Select(d => d.Id).ToArray();

    private static string[] Generator(ISourceGenerator g, string code)
    {
        CSharpGeneratorDriver.Create(g).RunGeneratorsAndUpdateCompilation(Probe(code), out _, out var diags);
        return diags.Select(d => d.Id).ToArray();
    }

    [Fact]
    public async Task Ein_Store_mit_genau_einer_Klasse_ist_still()
        => (await Analyzer(@"namespace Probe {
            public sealed class Eins : Basis, IAtomaresZiel, ISchreibeOhne {
                public Task SchreibeAsync(Guid konto, decimal betrag) => Task.CompletedTask;
                public Task<PartitionsCheckpoint> LadeFortschrittAsync(string p, int n, System.Threading.CancellationToken ct) => null!;
                public Task<System.Collections.Generic.IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string p, System.Collections.Generic.IReadOnlyCollection<Guid> s, System.Threading.CancellationToken ct) => null!;
                public Task FortschrittZuruecksetzenAsync(string p, int n, string g, System.Threading.CancellationToken ct) => null!;
                public Task<int> SchreibeMitFortschrittAsync(PartitionsCheckpoint f, System.Collections.Generic.IReadOnlyDictionary<Guid, int> g, System.Threading.CancellationToken ct) => null!;
            }
            public sealed class Zwei : Basis, IWiederholungsfestesZiel, ISchreibeMit { public Task SchreibeAsync(Herkunft h, decimal betrag) => Task.CompletedTask; }
            public sealed class Drei : Basis, INurMindestensEinmal, ISchreibeOhne { public Task SchreibeAsync(Guid konto, decimal betrag) => Task.CompletedTask; }
            public sealed class NurLesen : IFinde { public Task<decimal> FindeAsync(Guid konto) => Task.FromResult(0m); } }"))
            .Should().BeEmpty("je eine Klasse; ein Store nur mit Lese-Fähigkeiten ist kein Ziel einer Projektion");

    [Fact]
    public async Task Keine_oder_zwei_Klassen_sind_CQRS109()
        => (await Analyzer(@"namespace Probe {
            public sealed class Keine : ISchreibeOhne { public Task SchreibeAsync(Guid konto, decimal betrag) => Task.CompletedTask; }
            public sealed class Doppelt : Basis, IWiederholungsfestesZiel, INurMindestensEinmal, ISchreibeMit { public Task SchreibeAsync(Herkunft h, decimal betrag) => Task.CompletedTask; } }"))
            .Should().Equal(StoreKlasseAnalyzer.Id, StoreKlasseAnalyzer.Id);

    [Fact]
    public async Task Eine_Schreib_Faehigkeit_der_Klasse_2_ohne_Herkunft_ist_CQRS109()
        => (await Analyzer(@"namespace Probe {
            public sealed class Zwei : Basis, IWiederholungsfestesZiel, ISchreibeOhne { public Task SchreibeAsync(Guid konto, decimal betrag) => Task.CompletedTask; } }"))
            .Should().Equal(StoreKlasseAnalyzer.Id);

    private const string Senke = @"namespace Probe {
            public sealed class Senke : Basis, INurMindestensEinmal, ISchreibeOhne { public Task SchreibeAsync(Guid konto, decimal betrag) => Task.CompletedTask; }
            PROJEKTION public sealed class Zaehler : IProjektion { public Task Auf(Gebucht e, Herkunft h, ISchreibeOhne s) => s.SchreibeAsync(h.Stream, e.Betrag); } }";

    [Fact]
    public void Eine_Projektion_in_einen_Store_der_Klasse_3_braucht_die_Erlaubnis_CQRS110()
    {
        Generator(new PullPathGenerator(), Senke.Replace("PROJEKTION", "")).Should().Contain(PullPathGenerator.MindestensEinmalId);
        Generator(new PullPathGenerator(), Senke.Replace("PROJEKTION", "[MindestensEinmal]")).Should().NotContain(PullPathGenerator.MindestensEinmalId);
    }

    [Fact]
    public void Ein_Read_Model_mit_typisierter_Id_muss_partial_sein_CQRS111()
    {
        IIncrementalGenerator g = new ReadModelSchluesselGenerator();
        string[] Ids(string code)
        {
            CSharpGeneratorDriver.Create(g).RunGeneratorsAndUpdateCompilation(Probe(code), out var neu, out var diags);
            neu.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("der Schlüssel muss mit dem Read-Model kompilieren");
            return diags.Select(d => d.Id).ToArray();
        }
        Ids("namespace Probe { public record Kontostand : IReadModel { public Id<Konto> Id { get; init; } } }")
            .Should().Equal(ReadModelSchluesselGenerator.NichtPartialId);
        Ids("namespace Probe { public partial record Kontostand : IReadModel { public Id<Konto> Id { get; init; } } }").Should().BeEmpty();
        Ids("namespace Probe { public record Zeile : IReadModel { public string Id { get; init; } = \"\"; } }").Should().BeEmpty("nur eine typisierte Id braucht den Schlüssel");
    }
}
