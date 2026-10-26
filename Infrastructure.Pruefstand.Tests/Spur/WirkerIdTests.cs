using System;
using System.Collections.Immutable;
using System.Linq;
using Abstractions;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Infrastructure.Pruefstand.SpurPruefung;

/// <summary>
/// Werkbank P4.1/P4.2: das Wirker-Format (eine Quelle, <see cref="WirkerId"/>) und die Namen zur Laufzeit — der ProzessRegelnGenerator
/// benennt Fluss-Knoten und Prozess-Regeln aus dem Quelltext nach derselben Regel wie der Extractor (dort prüft <c>--check</c> die Gleichheit).
/// </summary>
public class WirkerIdTests
{
    [Fact]
    public void Die_acht_Formen()
    {
        WirkerId.FuerDecide("Datensatz", "ErstelleDatensatz").Should().Be("decide:Datensatz/ErstelleDatensatz");
        WirkerId.FuerFluss("Bildeingang", "vorschau").Should().Be("fluss:Bildeingang#vorschau");
        WirkerId.FuerProzess("TeilFertigProzess", "A→C").Should().Be("prozess:TeilFertigProzess#A→C");
        WirkerId.FuerFunktion("IBildVerkleinerung").Should().Be("funktion:IBildVerkleinerung");
        WirkerId.FuerQuelle("DateiErkannt").Should().Be("quelle:DateiErkannt");
        WirkerId.FuerProjektion("P").Should().Be("projektion:P");
        WirkerId.FuerReaktion("R").Should().Be("reaktion:R");
        WirkerId.FuerAkteur("Disponent").Should().Be("akteur:Disponent");
        WirkerId.Zerlege("fluss:Bildeingang#datei", out var art, out var rest).Should().BeTrue();
        (art, rest).Should().Be(("fluss", "Bildeingang#datei"));
        WirkerId.Zerlege("rec:DateiErkannt", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Regel_heisst_nach_Ausloeser_Und_Sammel_und_Ziel()
    {
        WirkerId.Regel(new[] { "A" }, null, "C").Should().Be("A→C");
        WirkerId.Regel(new[] { "A", "B" }, null, "C").Should().Be("A+B→C");
        WirkerId.Regel(new[] { "A" }, "S", "C").Should().Be("A+S*→C");
    }

    [Fact]
    public void Knoten_ohne_Variable_heissen_nach_dem_Typ_und_werden_durchnummeriert()
        => WirkerId.KnotenNamen(new[] { "datei", null, null, "meldeFertig2" }, new[] { "DateiErkannt", "MeldeFertig", "MeldeFertig", "X" })
            .Should().Equal("datei", "meldeFertig", "meldeFertig2", "meldeFertig2");

    [Fact]
    public void Die_Laufzeit_kennt_die_Namen_der_Domaene()
    {
        var bild = Domain.Pipeline.Prozess.GeneratedProzessRegeln.Alle["Bildeingang"];
        bild.KnotenNamen.Should().Equal("datei", "deuten", "paar", "vorschau", "kontrast", "melden");
        bild.Wirker(0).Should().StartWith("fluss:Bildeingang#");
        var teil = Domain.Prozess.GeneratedProzessRegeln.Alle["TeilFertigProzess"];
        teil.RegelNamen.Should().Equal("TeilauftragAbgeschlossen→MeldeTeilFertig");
        teil.Wirker(0).Should().Be("prozess:TeilFertigProzess#TeilauftragAbgeschlossen→MeldeTeilFertig");
    }

    [Fact]
    public void Generator_benennt_Fluss_und_Prozess_aus_dem_Quelltext()
    {
        var (quelle, diag) = Generiere(@"
public sealed record Los(Guid Id) : IEvent;
public sealed record Weiter(Guid Id) : IEvent;
public sealed record Mach(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public sealed record Melde(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public sealed class P : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<Los>.Definiere(p =>
    {
        p.Auf<Los>().Sende<Mach>(e => new Mach(new(e.Id)));
        p.Auf<Los>().Und<Weiter>().Sende<Melde>((a, b) => new Melde(new(a.Id)));
    });
}
public sealed class F : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var los = p.Auf<Los>();
        los.Sende<Mach>(e => new Mach(new(e.Id)));
        var weiter = los.Sende<Melde>(e => new Melde(new(e.Id))).Zeitlimit(TimeSpan.FromSeconds(5));
        los.Sende<Mach>(e => new Mach(new(e.Id)));
    });
}");
        diag.Should().BeEmpty();
        quelle.Should().Contain("((IProzessDefinition)new Probe.P()).Regeln.Benannt(\"P\", \"Los→Mach\", \"Los+Weiter→Melde\")");
        quelle.Should().Contain("((IPipeline)new Probe.F()).Fluss.Regeln.Benannt(\"F\", \"los\", \"mach\", \"weiter\", \"mach2\")");
    }

    [Fact]
    public void Zwei_gleich_benannte_Regeln_sind_CQRS072()
    {
        var (_, diag) = Generiere(@"
public sealed record Los(Guid Id) : IEvent;
public sealed record Mach(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
public sealed class P : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<Los>.Definiere(p =>
    {
        p.Auf<Los>().Sende<Mach>(e => new Mach(new(e.Id)));
        p.Auf<Los>().Sende<Mach>(e => new Mach(new(e.Id)));
    });
}");
        diag.Should().Contain("CQRS072");
    }

    private static (string Quelle, string[] Diagnosen) Generiere(string code)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { typeof(IProzessDefinition).Assembly }).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe",
            new[] { CSharpSyntaxTree.ParseText("using System; using Abstractions; namespace Probe {\n" + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var lauf = CSharpGeneratorDriver.Create(new ProzessRegelnGenerator()).RunGenerators(comp).GetRunResult();
        var quelle = string.Join("\n", lauf.GeneratedTrees.Select(t => t.ToString()));
        return (quelle, lauf.Diagnostics.Select(d => d.Id).ToArray());
    }
}
