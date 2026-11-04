using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Projektionen;

/// <summary>
/// BÜNDEL (docs/04 §4.3.1): <c>Auf(Buendel&lt;A, B&gt; b, …)</c> ist eine Form der Projektion; den Schlüssel leitet der Generator nur aus Code-Fakten ab
/// (welches Aggregat ein Event faltet, welche <c>Id&lt;T&gt;</c>-Felder es trägt) — Stream, Feld, global; mehrdeutig ist CQRS118, <c>[Ueber&lt;T&gt;]</c>
/// wählt. Der Weckplan nennt jedes Event eines Bündels. Die Generatoren laufen über einer Probe-Compilation wie im Build.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class BuendelGeneratorTests
{
    private const string Kopf = "using System; using System.Threading.Tasks; using Abstractions; using Core;\n";

    /// <summary>Zwei Aggregate: die Rechnung faltet Gestellt und Freigegeben, das Konto die Gutschrift (sie nennt Rechnung und Konto im Feld).</summary>
    private const string Domaene = @"namespace Probe {
public partial class Rechnung : IState { public int Version { get; set; } public Guid Id { get; set; }
    public class Applier : IApplier<Rechnung> { public void Apply(Gestellt e) { } public void Apply(Freigegeben e) { } } }
public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; }
    public class Applier : IApplier<Konto> { public void Apply(Gutschrift e) { } public void Apply(Doppelt e) { } } }
public partial class Stelle : IState { public int Version { get; set; } public Guid Id { get; set; }
    public class Applier : IApplier<Stelle> { public void Apply(Ausreisser e) { } } }
public record Gestellt(Id<Rechnung> Rechnung, Id<Konto> Konto, decimal Betrag) : IEvent;
public record Freigegeben() : IEvent;
public record Gutschrift(Id<Rechnung> Rechnung, decimal Betrag) : IEvent;
public record Doppelt(Id<Rechnung> Alt, Id<Rechnung> Neu) : IEvent;
public record Ausreisser(double Wert) : IEvent;
public record Hinweis() : ITransientEvent;
public interface ISchreibe : IWriteStore { Task SchreibeAsync(string x, Herkunft h); }
public sealed class Ablage : IWiederholungsfestesZiel, ISchreibe
{
    public Task SchreibeAsync(string x, Herkunft h) => Task.CompletedTask;
    public void Beginne(string generation) { } public void Stream(Guid stream) { }
    public Task SchreibeAsync(System.Threading.CancellationToken ct) => Task.CompletedTask;
    public Task NeueGenerationAsync(string generation, System.Threading.CancellationToken ct) => Task.CompletedTask;
    public Task UmschaltenAsync(string generation, System.Threading.CancellationToken ct) => Task.CompletedTask;
}
";

    private static ImmutableArray<MetadataReference> Refs() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Concat(new[] { typeof(Abstractions.IState).Assembly, typeof(Core.MartenStore).Assembly, typeof(Infrastructure.Funktionen.GraphActor).Assembly })
            .Distinct().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();

    private static CSharpCompilation Probe(string projektion)
    {
        var comp = CSharpCompilation.Create("Probe.Laufzeit", new[] { CSharpSyntaxTree.ParseText(Kopf + Domaene + projektion + " }") }, Refs(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return comp;
    }

    /// <summary>Der PullPathGenerator über der Probe: seine Diagnosen und sein Quelltext.</summary>
    private static (string[] Ids, string Quelle) Generiere(string projektion, ISourceGenerator? generator = null)
    {
        var lauf = CSharpGeneratorDriver.Create(generator ?? new Infrastructure.SourceGeneration.PullPathGenerator()).RunGenerators(Probe(projektion)).GetRunResult();
        return (lauf.Diagnostics.Select(d => d.Id).ToArray(), string.Concat(lauf.GeneratedTrees.Select(t => t.ToString())));
    }

    private static string Projektion(string auf) =>
        $"public sealed class Bild : IProjektion {{ public Task {auf} => s.SchreibeAsync(\"\", h); }}";

    [Fact]
    public void Ein_Buendel_aus_einem_Aggregat_hat_den_Stream_als_Schluessel()
    {
        var (ids, quelle) = Generiere(Projektion("Auf(Buendel<Gestellt, Freigegeben> b, Herkunft h, ISchreibe s)"));
        ids.Should().BeEmpty();
        quelle.Should().Contain("new Buendelglied(typeof(global::Probe.Gestellt), Schluesselart.Stream), new Buendelglied(typeof(global::Probe.Freigegeben), Schluesselart.Stream)");
        quelle.Should().Contain("Bild_Typen = { typeof(global::Probe.Gestellt), typeof(global::Probe.Freigegeben) }", "gehört werden die Events des Bündels");
        quelle.Should().Contain("blick.Gebuendelt(0, e) is { } b0").And.Contain("buendel: blick");
        quelle.Should().NotContain("[typeof(global::Probe.Gestellt)] = new[] { new Rueckblickeintrag", "ein Bündel über den Stream braucht kein Verzeichnis");
    }

    [Fact]
    public void Mehrere_gemeinsame_Ids_sind_CQRS118_und_Ueber_waehlt_Stream_und_Feld()
    {
        Generiere(Projektion("Auf(Buendel<Gestellt, Gutschrift> b, Herkunft h, ISchreibe s)")).Ids
            .Should().Equal("CQRS118");   // Gestellt nennt Rechnung (Stream, Feld) und Konto (Feld), die Gutschrift Konto (Stream) und Rechnung (Feld)

        var (ids, quelle) = Generiere(Projektion("Auf([Ueber<Rechnung>] Buendel<Gestellt, Gutschrift> b, Herkunft h, ISchreibe s)"));
        ids.Should().BeEmpty();
        quelle.Should().Contain("new Buendelglied(typeof(global::Probe.Gestellt), Schluesselart.Stream)", "die Rechnung faltet Gestellt: der Stream gewinnt");
        quelle.Should().Contain("new Buendelglied(typeof(global::Probe.Gutschrift), Schluesselart.Feld, \"Rechnung\", x => ((global::Probe.Gutschrift)x).Rechnung.Wert)");
        quelle.Should().Contain("[typeof(global::Probe.Gutschrift)] = new[] { new Rueckblickeintrag(\"Rechnung\"", "ein Bündel über ein Feld schreibt ins Verzeichnis");
    }

    [Fact]
    public void Ohne_gemeinsame_Id_ist_das_Buendel_global()
    {
        var (ids, quelle) = Generiere(Projektion("Auf(Buendel<Freigegeben, Ausreisser> b, Herkunft h, ISchreibe s)"));
        ids.Should().BeEmpty();
        quelle.Should().Contain("new Buendelglied(typeof(global::Probe.Freigegeben), Schluesselart.Global), new Buendelglied(typeof(global::Probe.Ausreisser), Schluesselart.Global)");
        quelle.Should().Contain("[typeof(global::Probe.Ausreisser)] = new[] { new Rueckblickeintrag(\"\", x => Guid.Empty) }");
    }

    [Fact]
    public void Ein_Event_mit_zwei_Feldern_des_Schluessels_ist_CQRS118()
        => Generiere(Projektion("Auf(Buendel<Freigegeben, Doppelt> b, Herkunft h, ISchreibe s)")).Ids.Should().Equal("CQRS118");

    [Fact]
    public void Ueber_mit_einem_Event_ohne_diese_Id_ist_CQRS118()
        => Generiere(Projektion("Auf([Ueber<Konto>] Buendel<Freigegeben, Gutschrift> b, Herkunft h, ISchreibe s)")).Ids.Should().Equal("CQRS118");

    [Fact]
    public void Der_Weckplan_nennt_jedes_Event_eines_Buendels()
    {
        var (ids, quelle) = Generiere(Projektion("Auf(Buendel<Freigegeben, Ausreisser> b, Herkunft h, ISchreibe s)"), new Infrastructure.SourceGeneration.WeckplanGenerator());
        ids.Should().BeEmpty();
        quelle.Should().Contain("[typeof(global::Probe.Freigegeben)] = new[] { new Weckziel(\"batch-Bild\") }");
        quelle.Should().Contain("[typeof(global::Probe.Ausreisser)] = new[] { new Weckziel(\"batch-Bild\") }");
    }

    private static async Task<string[]> Form(string projektion)
    {
        var ds = await Probe(projektion).WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new Domain.SourceGeneration.HandlerFormAnalyzer(),
            new Domain.SourceGeneration.EinEingangAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    [Fact]
    public async Task Ein_Buendel_ist_eine_Form_des_Auf_ein_Hinweis_oder_ein_Typ_doppelt_nicht()
    {
        (await Form(Projektion("Auf(Buendel<Gestellt, Freigegeben> b, Herkunft h, ISchreibe s)"))).Should().BeEmpty();
        (await Form(Projektion("Auf(Buendel<Gestellt, Hinweis> b, Herkunft h, ISchreibe s)"))).Should().Contain("CQRS057", "ein Hinweis steht nie im Log");
        (await Form(Projektion("Auf(Buendel<Gestellt, Gestellt> b, Herkunft h, ISchreibe s)"))).Should().Contain("CQRS057", "je Event-Typ eine Stelle");
    }

    [Fact]
    public async Task Zwei_Buendel_gleicher_Stelligkeit_sind_verschiedene_Eingaenge()
        => (await Form(@"public sealed class Bild : IProjektion {
            public Task Auf(Buendel<Gestellt, Freigegeben> b, Herkunft h, ISchreibe s) => s.SchreibeAsync("""", h);
            public Task Auf(Buendel<Gestellt, Ausreisser> b, Herkunft h, ISchreibe s) => s.SchreibeAsync("""", h); }")).Should().BeEmpty();
}
