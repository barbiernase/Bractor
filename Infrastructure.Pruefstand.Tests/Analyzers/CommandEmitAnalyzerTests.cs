using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// Auflage A6 — durabler Regressions-Guard für den <see cref="CommandEmitAnalyzer"/> (EM-1 erzwungen).
/// Fährt den Analyzer über eine eigene <see cref="CSharpCompilation"/> (Referenzen aus den bereits geladenen
/// Assemblies, damit <c>Abstractions.CommandResult</c> + <c>System.Threading.CancellationToken</c> semantisch
/// binden). Bewiesen: CQRS020 feuert bei einem rohen Command-Send außerhalb des Primitivs, CQRS021 bei einer
/// None-Token-Kante, und der erlaubte <c>CommandEmitter</c>-Typ mit bounded Token bleibt sauber.
/// </summary>
public class CommandEmitAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> Analysiere(string code)
    {
        // Die relevanten Assemblies laden (Token-Referenz erzwingt das Laden), dann alle als MetadataReference.
        _ = typeof(Abstractions.CommandResult);
        _ = typeof(Proto.Cluster.Cluster);
        _ = typeof(Infrastructure.PubSub.CommandEmitter);
        _ = typeof(System.Threading.CancellationToken);
        _ = typeof(object);

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();

        var comp = CSharpCompilation.Create(
            "AnalyzerProbe",
            new[] { CSharpSyntaxTree.ParseText(code) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzer = comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CommandEmitAnalyzer()));
        return await withAnalyzer.GetAnalyzerDiagnosticsAsync();
    }

    // Fremd-Typ, roher Send + None-Token → CQRS020 UND CQRS021.
    private const string RohSendMitNone = @"
using System.Threading; using System.Threading.Tasks; using Abstractions; using Proto.Cluster;
namespace Fremd { class Sender {
    public Task<CommandResult> M(Cluster c, ClusterIdentity id, object env)
        => c.RequestAsync<CommandResult>(id, env, CancellationToken.None);
} }";

    // Fremd-Typ, roher Send + bounded Token → NUR CQRS020.
    private const string RohSendBounded = @"
using System.Threading; using System.Threading.Tasks; using Abstractions; using Proto.Cluster;
namespace Fremd { class Sender {
    public Task<CommandResult> M(Cluster c, ClusterIdentity id, object env, CancellationToken ct)
        => c.RequestAsync<CommandResult>(id, env, ct);
} }";

    // Der ERLAUBTE Emitter-Typ (voll qualifiziert) mit bounded Token → KEIN Diagnostic.
    private const string ErlaubterEmitterBounded = @"
using System.Threading; using System.Threading.Tasks; using Abstractions; using Proto.Cluster;
namespace Infrastructure.PubSub { class CommandEmitter {
    public Task<CommandResult> M(Cluster c, ClusterIdentity id, object env, CancellationToken ct)
        => c.RequestAsync<CommandResult>(id, env, ct);
} }";

    [Fact]
    public async Task Roher_Send_mit_None_feuert_CQRS020_und_CQRS021()
    {
        var ids = (await Analysiere(RohSendMitNone)).Select(d => d.Id).ToList();
        ids.Should().Contain(CommandEmitAnalyzer.RawSendId, "roher Send außerhalb des Primitivs (EM-1)");
        ids.Should().Contain(CommandEmitAnalyzer.UnboundedTokenId, "None-Token ist eine unbounded Command-Kante (W2)");
    }

    [Fact]
    public async Task Roher_Send_mit_bounded_Token_feuert_nur_CQRS020()
    {
        var ids = (await Analysiere(RohSendBounded)).Select(d => d.Id).ToList();
        ids.Should().Contain(CommandEmitAnalyzer.RawSendId);
        ids.Should().NotContain(CommandEmitAnalyzer.UnboundedTokenId, "bounded Token → keine W2-Verletzung");
    }

    [Fact]
    public async Task Erlaubter_Emitter_mit_bounded_Token_ist_sauber()
    {
        var ids = (await Analysiere(ErlaubterEmitterBounded)).Select(d => d.Id).ToList();
        ids.Should().NotContain(CommandEmitAnalyzer.RawSendId, "CommandEmitter ist der EINE erlaubte Emit-Weg");
        ids.Should().NotContain(CommandEmitAnalyzer.UnboundedTokenId, "bounded Token");
    }
}
