using System;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Infrastructure.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Garantien zur Build- und Startzeit (docs/11 §11.10), die bisher ohne eigenen Test waren: ein Command gehört genau einem Aggregat (CQRS010),
/// Aggregat-Identitäten sind eindeutig (CQRS011), die Schreib-Fähigkeiten eines Konsumenten gehören zu genau einem Store (CQRS052), eine
/// Fähigkeit hat genau eine Store-Klasse (CQRS053), und jeder Wire-Typ der Anwendung ist serialisierbar (Boot-Check). Die Generatoren laufen
/// über einer Probe-Compilation, wie im Build.
/// </summary>
public class BuildGarantieTests
{
    private const string Kopf = "using System; using System.Threading.Tasks; using Abstractions; using Core;\n";

    private static string[] Ids(ISourceGenerator generator, string code)
    {
        // Ausdrücklich angehängt (wie FaehigkeitAnalyzerTests): ein „_ = typeof(…)" darf der JIT verwerfen.
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(Abstractions.IState).Assembly, typeof(Core.ProjectionWriter).Assembly }).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Laufzeit", new[] { CSharpSyntaxTree.ParseText(Kopf + code) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        CSharpGeneratorDriver.Create(generator).RunGeneratorsAndUpdateCompilation(comp, out _, out var diags);
        return diags.Select(d => d.Id).ToArray();
    }

    private const string Aggregat = @"
public partial class {0} : IState
{{
    public int Version {{ get; set; }} public Guid Id {{ get; set; }}
    public class Decider : IDecider<{0}> {{ public OneOf<ProbeGemacht> Decide({1} c) => default!; }}
    public class Applier : IApplier<{0}> {{ public void Apply(ProbeGemacht e) {{ }} }}
}}";

    private const string Typen = @"namespace ProbeTypen {
public record ProbeMach(Guid AggregateId) : ICommand;
public record ProbeSchliess(Guid AggregateId) : ICommand;
public record ProbeGemacht() : IEvent; }
";

    [Fact]
    public void Ein_Command_mit_zwei_Aggregaten_bricht_den_Build_CQRS010()
        => Ids(new Infrastructure.SourceGeneration.CommandAggregateMapGenerator(), Typen + "namespace Probe { using ProbeTypen;"
               + string.Format(Aggregat, "ProbeA", "ProbeMach") + string.Format(Aggregat, "ProbeB", "ProbeMach") + "}")
            .Should().Contain("CQRS010");

    [Fact]
    public void Zwei_Aggregate_mit_derselben_Identitaet_brechen_den_Build_CQRS011()
        => Ids(new Infrastructure.SourceGeneration.CommandAggregateMapGenerator(), Typen
               + "namespace Probe.Eins { using ProbeTypen;" + string.Format(Aggregat, "ProbeKonto", "ProbeMach") + "}"
               + "namespace Probe.Zwei { using ProbeTypen;" + string.Format(Aggregat, "ProbeKonto", "ProbeSchliess") + "}")
            .Should().Contain("CQRS011");

    private const string Faehigkeiten = @"namespace Probe {
public record ProbeGemacht() : IEvent;
public interface ISchreibeA : IWriteStore { Task SchreibeAAsync(string x); }
public interface ISchreibeB : IWriteStore { Task SchreibeBAsync(string x); }
";

    [Fact]
    public void Ein_Konsument_mit_Schreib_Faehigkeiten_aus_zwei_Stores_bricht_den_Build_CQRS052()
        => Ids(new Infrastructure.SourceGeneration.PullPathGenerator(), Faehigkeiten + @"
public sealed class StoreA : ISchreibeA { public Task SchreibeAAsync(string x) => Task.CompletedTask; }
public sealed class StoreB : ISchreibeB { public Task SchreibeBAsync(string x) => Task.CompletedTask; }
public partial class ZweiStores : ISubscriber, IPullSubscriber
{
    public string SubscriberId => ""zwei"";
    public Task Handle(ProbeGemacht e, IAggregateEnvelope env, ProjectionWriter w, ISchreibeA a, ISchreibeB b) => Task.CompletedTask;
} }")
            .Should().Contain("CQRS052");

    [Fact]
    public void Eine_Faehigkeit_mit_zwei_Store_Klassen_bricht_den_Build_CQRS053()
        => Ids(new global::Projections.SourceGeneration.ProjectionServicesGenerator(), Faehigkeiten + @"
public sealed class StoreEins : ISchreibeA { public Task SchreibeAAsync(string x) => Task.CompletedTask; }
public sealed class StoreZwei : ISchreibeA { public Task SchreibeAAsync(string x) => Task.CompletedTask; } }")
            .Should().Contain("CQRS053");

    [Fact]
    public void Jeder_Wire_Typ_der_Anwendung_ist_serialisierbar()
    {
        // Der Boot-Check des Hosts über die echte Komposition (Beispielanwendung): Hüllen, alle Commands/Events/Trigger, alle Signale.
        var akt = () => WireSerializerBootCheck.Verify();
        akt.Should().NotThrow();
    }
}
