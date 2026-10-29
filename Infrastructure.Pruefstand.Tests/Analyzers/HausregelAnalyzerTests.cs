using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Hausregel.SourceGeneration;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// Die Hausregel für Actors als Build-Prüfung (R18, docs/06 §6.7): <see cref="HausregelAnalyzer"/> meldet jedes Verbots-Muster in
/// einem Actor und in dem, was er besitzt (Feld, primärer Konstruktor, Typ-Argument), schweigt außerhalb, beim erwiesen fertigen Task und
/// bei einer begründeten Ausnahme; eine Ausnahme ohne Begründung ist selbst ein Fehler.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class HausregelAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> Analysiere(string code)
    {
        _ = typeof(Proto.IActor);
        _ = typeof(Proto.Cluster.Cluster);
        _ = typeof(Infrastructure.Lebenszyklus.HausregelAusnahmeAttribute);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("HausregelProbe", new[] { CSharpSyntaxTree.ParseText(code) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe muss kompilieren");
        return await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new HausregelAnalyzer())).GetAnalyzerDiagnosticsAsync();
    }

    private const string Kopf = @"
using System; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks;
using Proto; using Proto.Cluster; using Infrastructure.Lebenszyklus;
namespace Probe {
static class Hilfe { public static Task ArbeitAsync() => Task.CompletedTask; public static Task<int> ZahlAsync() => Task.FromResult(1); }
";

    /// <summary>Ein Actor, dessen Behandlung <paramref name="rumpf"/> enthält; dazu ein Feld <c>_cluster</c> und ein Objekt <c>_schloss</c>.</summary>
    private static string ImActor(string rumpf, string mitglieder = "") => Kopf + @"
class A : IActor {
    private readonly object _schloss = new();
    private readonly Cluster _cluster = null!;
    " + mitglieder + @"
    public async Task ReceiveAsync(IContext context) { await Task.Yield(); " + rumpf + @" }
} }";

    public static TheoryData<string, string> Verbote => new()
    {
        { "Task.Run", "await Task.Run(() => { });" },
        { "TaskFactory.StartNew", "await Task.Factory.StartNew(() => { });" },
        { "Parallel", "Parallel.For(0, 2, _ => { });" },
        { "ThreadPool", "ThreadPool.QueueUserWorkItem(_ => { });" },
        { "new Thread", "var t = new Thread(() => { });" },
        { "lock", "lock (_schloss) { }" },
        { "_ = …Async", "_ = Hilfe.ArbeitAsync();" },
        { "nicht abgewarteter Task", "Hilfe.ArbeitAsync();" },
        { "ContinueWith", "await Hilfe.ArbeitAsync().ContinueWith(_ => { });" },
        { ".Result", "var n = Hilfe.ZahlAsync().Result;" },
        { ".Wait()", "Hilfe.ArbeitAsync().Wait();" },
        { "GetAwaiter().GetResult()", "Hilfe.ArbeitAsync().GetAwaiter().GetResult();" },
        { "rohe Cluster-API (Cluster.RequestAsync)", "await _cluster.RequestAsync<object>(\"id\", \"kind\", new object(), CancellationToken.None);" },
        { "rohe Cluster-API (Cluster.GetAsync)", "await _cluster.GetAsync(ClusterIdentity.Create(\"id\", \"kind\"), CancellationToken.None);" },
        { "rohe Anfrage (IContext.RequestAsync)", "await context.RequestAsync<object>(context.Self, new object());" },
    };

    [Theory]
    [MemberData(nameof(Verbote))]
    public async Task Jedes_Verbots_Muster_im_Actor_ist_CQRS112(string muster, string rumpf)
    {
        var d = await Analysiere(ImActor(rumpf));
        d.Where(x => x.Id == HausregelAnalyzer.VerstossId).Should().ContainSingle(muster);
    }

    [Theory]
    [InlineData("class A : IActor { private readonly Teil _teil = new(); public Task ReceiveAsync(IContext c) => Task.CompletedTask; }", "Feld")]
    [InlineData("class A(Teil teil) : IActor { public Task ReceiveAsync(IContext c) => teil.Tu(); }", "primärer Konstruktor")]
    [InlineData("class A : IActor { private readonly Dictionary<Guid, List<Teil>> _teile = new(); public Task ReceiveAsync(IContext c) => Task.CompletedTask; }", "Typ-Argument")]
    [InlineData("class A : IActor { private Huelle H { get; } = new(); public Task ReceiveAsync(IContext c) => Task.CompletedTask; } class Huelle { private readonly Teil _teil = new(); }", "transitiv über eine Auto-Property")]
    [InlineData("abstract class Basis : IActor { public Task ReceiveAsync(IContext c) => Task.CompletedTask; } class A : Basis { private readonly Teil[] _teile = []; }", "Array im abgeleiteten Actor")]
    public async Task Was_ein_Actor_besitzt_gilt_mit(string actor, string wie)
    {
        var code = Kopf + actor + @"
class Teil { public Task Tu() => Task.Run(() => { }); } }";
        var d = await Analysiere(code);
        d.Where(x => x.Id == HausregelAnalyzer.VerstossId).Should().ContainSingle(wie)
            .Which.GetMessage().Should().Contain("'Teil'");
    }

    [Fact]
    public async Task Ausserhalb_eines_Actors_und_im_nicht_besessenen_geschachtelten_Typ_schweigt_er()
    {
        var code = Kopf + @"
class Fremd { public void Tu() { Task.Run(() => { }); lock (this) { } } }
class A : IActor {
    public Task ReceiveAsync(IContext c) => Task.CompletedTask;
    private sealed class Fehler : Exception { public void Tu() { Task.Run(() => { }); } }
} }";
        (await Analysiere(code)).Should().BeEmpty();
    }

    [Fact]
    public async Task Das_Ergebnis_eines_erwiesen_fertigen_Tasks_und_await_sind_erlaubt()
    {
        var code = ImActor(@"
            var n = await Hilfe.ZahlAsync();
            Task t = Hilfe.ZahlAsync();
            var a = t is Task<int> { IsCompletedSuccessfully: true } fertig ? fertig.Result : 0;
            context.ReenterAfter(Hilfe.ZahlAsync(), r => { var b = r is Task<int> { IsCompleted: true } x ? x.Result : 0; return Task.CompletedTask; });");
        (await Analysiere(code)).Should().BeEmpty();
    }

    [Fact]
    public async Task Ein_nur_mit_Typ_geprueftes_Muster_reicht_fuer_Result_nicht()
    {
        var code = ImActor(@"Task t = Hilfe.ZahlAsync(); var a = t is Task<int> fertig ? fertig.Result : 0;");
        (await Analysiere(code)).Select(x => x.Id).Should().Equal(HausregelAnalyzer.VerstossId);
    }

    [Fact]
    public async Task Eine_begruendete_Ausnahme_am_Member_oder_Typ_schweigt()
    {
        var amMember = ImActor("Tu();", @"[HausregelAusnahme(""Probe: begründet"")] private void Tu() { _ = Hilfe.ArbeitAsync(); }");
        (await Analysiere(amMember)).Should().BeEmpty();

        var amTyp = Kopf + @"
[HausregelAusnahme(""Probe: begründet"")]
class A : IActor { public Task ReceiveAsync(IContext c) { lock (this) { } return Task.CompletedTask; } } }";
        (await Analysiere(amTyp)).Should().BeEmpty();
    }

    [Fact]
    public async Task Eine_Ausnahme_ohne_Begruendung_ist_CQRS113()
    {
        var code = ImActor("", @"[HausregelAusnahme("" "")] private void Tu() { }");
        (await Analysiere(code)).Select(x => x.Id).Should().Equal(HausregelAnalyzer.OhneBegruendungId);
    }
}
