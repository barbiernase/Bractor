using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// CQRS079–083 — Grammatik-Regeln und Graph-Diagnosen im Build (Werkbank P7.4): MISSING-APPLY, GR-FALTUNG, ENUM-ZERO, GR-GARANTIE und
/// Regel Z (GR-ZUSTAND); CQRS013 — ein Command nennt sein Ziel im Typ (R7). Alles aus Signaturen und Symbolen, nie aus Rümpfen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ModellAnalyzerTests
{
    private static async Task<Diagnostic[]> Befunde(string code)
    {
        _ = typeof(Abstractions.IState);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var comp = CSharpCompilation.Create("ModellProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return (await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ModellAnalyzer())).GetAnalyzerDiagnosticsAsync()).ToArray();
    }

    private static async Task<string[]> Ids(string code) => (await Befunde(code)).Select(d => d.Id).ToArray();

    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public record Eroeffne(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public record Schliesse(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public record Eroeffnet() : IEvent;
public record Geschlossen() : IEvent;
public record Fremd() : IEvent;
public record SchonOffen() : ITransientEvent;
public record Mahne(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;\npublic partial class ProbeZiel : IState { public int Version { get; set; } public Guid Id { get; set; } }
";

    private const string Konto = @"
public partial class Konto : IState
{
    public int Version { get; set; } public Guid Id { get; set; }
    public class Decider : IDecider<Konto>
    {
        public OneOf<Eroeffnet, SchonOffen> Decide(Eroeffne c) => default!;
        public OneOf<Geschlossen> Decide(Schliesse c) => default!;
    }
}
";

    [Fact]
    public async Task Ein_vollstaendiges_Aggregat_ist_still()
        => (await Ids(Konto + "public partial class Konto { public class Applier : IApplier<Konto> { public void Apply(Eroeffnet e) { } public void Apply(Geschlossen e) { } } }"))
            .Should().BeEmpty();

    [Fact]
    public async Task Ein_Command_ohne_typisiertes_Ziel_ist_ein_Fehler_CQRS013()
    {
        // Der Umweg an ICommand<TAggregat> vorbei: die Framework-Sicht (Ziel als Guid) von Hand implementiert.
        var befunde = await Befunde("public record Umweg(Guid Ziel) : ICommand;");
        befunde.Should().ContainSingle().Which.Id.Should().Be(ModellAnalyzer.ZielId);
        befunde[0].GetMessage().Should().Contain("'Umweg'");
    }

    [Fact]
    public async Task Ein_Command_mit_typisiertem_Ziel_ist_still()
        => (await Ids("public record Direkt(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>, ICreationCommand;")).Should().BeEmpty();

    [Fact]
    public async Task Ein_entschiedenes_Event_ohne_Apply_ist_ein_Fehler_die_Ablehnung_nicht()
    {
        var befunde = await Befunde(Konto + "public partial class Konto { public class Applier : IApplier<Konto> { public void Apply(Eroeffnet e) { } } }");
        befunde.Should().ContainSingle().Which.Id.Should().Be(ModellAnalyzer.ApplyFehltId);
        befunde[0].GetMessage().Should().Contain("'Geschlossen'").And.Contain("Decide(Schliesse)");
    }

    [Fact]
    public async Task Ein_gefaltetes_Event_das_das_Aggregat_nicht_entscheidet_ist_eine_Warnung()
    {
        var befunde = await Befunde(Konto + @"public partial class Konto { public class Applier : IApplier<Konto>
            { public void Apply(Eroeffnet e) { } public void Apply(Geschlossen e) { } public void Apply(Fremd e) { } } }");
        befunde.Should().ContainSingle().Which.Id.Should().Be(ModellAnalyzer.FaltungId);
        befunde[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task Ein_Enum_Wert_0_ist_eine_Warnung()
    {
        (await Ids("public enum Stufe { Neu, Alt }")).Should().Equal(ModellAnalyzer.EnumNullId);
        (await Ids("public enum Stufe { Neu = 1, Alt = 2 }")).Should().BeEmpty();
    }

    private const string Projektion = @"
public sealed class Mahner : ISubscriber
{
    public string SubscriberId => ""m"";
    public OneOf<Mahne> Handle(SchonOffen e, IAggregateEnvelope env) => default!;
    public Task Handle(Eroeffnet e, IAggregateEnvelope env) => Task.CompletedTask;
}";

    [Fact]
    public async Task Ein_Command_ab_einem_transienten_Eingang_ist_eine_Warnung()
        => (await Ids(Projektion)).Should().Equal(ModellAnalyzer.GarantieId);

    [Fact]
    public async Task Ein_Konsument_mit_Zustand_ist_eine_Warnung_injizierte_Felder_nicht()
    {
        var mitZustand = "public sealed class Zaehler : ISubscriber { public string SubscriberId => \"z\"; private int _n; private readonly List<Guid> _gesehen = new(); }";
        var befunde = await Befunde(mitZustand);
        befunde.Should().ContainSingle().Which.Id.Should().Be(ModellAnalyzer.ZustandId);
        befunde[0].GetMessage().Should().Contain("_n, _gesehen");

        (await Ids("public sealed record Konfig(int Wert); public sealed class Leser : ISubscriber { public string SubscriberId => \"l\"; "
                   + "private readonly Konfig _k; private readonly string _name = \"x\"; public Leser(Konfig k) { _k = k; } }"))
            .Should().BeEmpty("injiziert (Ctor) und string halten keinen Zustand");
    }
}
