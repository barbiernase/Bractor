using System;
using System.Collections.Immutable;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Infrastructure.Pruefstand.Wecken;

/// <summary>
/// Der Weckplan (docs/03 §3.5) entsteht NUR aus Signaturen: eine Projektion mit <c>Auf(TEvent …)</c> steht mit ihrem Batch-Kind unter TEvent (gelesen
/// wie im PullPathGenerator, eine Lesart), die Uhr unter <c>FristGesetzt</c>. Eine Funktion liest nie das Log (docs/04 §4.4) und steht nie darin. Was
/// eine Projektion hören soll und der Plan nicht führen kann (Schnittstelle, abstrakt, Hinweis), ist schon ein Formfehler (CQRS057).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class WeckplanGeneratorTests
{
    private const string Vorspann = @"
using System; using System.Threading.Tasks; using Abstractions; using Core;
namespace Probe {
public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
public sealed record Mahne(Id<Konto> AggregateId, int Stufe) : ICommand<Konto>;
public sealed record Ueberfaellig(int Tage) : IEvent;
public sealed record Bezahlt(decimal Betrag) : IEvent;
public sealed record Messwert(double Wert) : ITransientEvent;
public sealed record Erinnert(int Stufe) : ICommand;
public interface IKontoEreignis : IEvent { }
public abstract record Basis : IEvent;
public interface ISchreibeKonto : IWriteStore { Task SetzeAsync(Guid konto); }
}
";

    private static (string Quelle, string[] Ids) Lauf(string code)
    {
        // Die Vertrags-Assemblies ausdrücklich (eine nur per typeof erwähnte ist zur Laufzeit womöglich noch nicht geladen).
        var noetig = new[] { typeof(IFunktion).Assembly, typeof(FunktionsBaustein).Assembly, typeof(Core.MartenStore).Assembly };
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(noetig).Distinct()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("WeckplanProbe",
            new[] { CSharpSyntaxTree.ParseText(Vorspann), CSharpSyntaxTree.ParseText("using System; using System.Threading.Tasks; using Abstractions; using Core;\n" + code) },
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");

        CSharpGeneratorDriver.Create(new Infrastructure.SourceGeneration.WeckplanGenerator()).RunGeneratorsAndUpdateCompilation(comp, out var neu, out var diags);
        neu.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("das Generat muss mit der Probe kompilieren");
        return (string.Join("\n", neu.SyntaxTrees.Skip(2).Select(t => t.ToString())), diags.Select(d => d.Id).ToArray());
    }

    private const string Projektion = @"namespace Probe {
        public sealed class KontoProjektion : IProjektion
        {
            public Task Auf(Ueberfaellig e, Herkunft h, ISchreibeKonto s) => Task.CompletedTask;
            public Task Auf(Bezahlt e, ISchreibeKonto s) => Task.CompletedTask;
        } }";

    [Fact]
    public void Projektion_und_Uhr_stehen_mit_ihren_Kinds_im_Plan_eine_Funktion_nie()
    {
        var (quelle, ids) = Lauf(Projektion + @"namespace Probe {
            public sealed record Folge(int Tage) : ICommand;
            public sealed class Mahnwesen : IFunktion { public Task<Erinnert> Auf(Folge f) => null!; } }");

        ids.Should().BeEmpty();
        quelle.Should().Contain("[typeof(global::Probe.Ueberfaellig)] = new[] { new Weckziel(\"batch-KontoProjektion\") }");
        quelle.Should().Contain("[typeof(global::Probe.Bezahlt)] = new[] { new Weckziel(\"batch-KontoProjektion\") }",
            "nur die Projektion hört Bezahlt — eine Funktion liest nie das Log, sie nimmt Commands ohne Ziel");
        quelle.Should().NotContain("funktion-");
        quelle.Should().Contain("[typeof(global::Abstractions.FristGesetzt)] = new[] { new Weckziel(\"uhr\", EineAdresse: true) }", "eine gesetzte Frist weckt die Uhr");
    }

    [Fact]
    public void Ein_Hinweis_als_Eingang_einer_Funktion_steht_nicht_im_Plan()
    {
        var (quelle, ids) = Lauf(@"namespace Probe {
            public sealed class Sensorik : IFunktion { public Task<Erinnert> Auf(Messwert m) => null!; } }");

        ids.Should().BeEmpty();
        quelle.Should().NotContain("Messwert", "ein Hinweis steht nicht im Log, ihn stellen die Verteiler zu");
    }

    [Theory]
    [InlineData("IKontoEreignis")]
    [InlineData("Basis")]
    [InlineData("Messwert")]
    public void Was_der_Plan_nicht_fuehren_kann_steht_nicht_darin(string typ)
    {
        var (quelle, ids) = Lauf(@"namespace Probe {
            public sealed class Falsch : IProjektion
            {
                public Task Auf(" + typ + @" e, ISchreibeKonto s) => Task.CompletedTask;
            }
            public sealed class Falsche : IFunktion { public Task<Erinnert> Auf(" + typ + @" e) => null!; } }");

        ids.Should().BeEmpty("die Form meldet es (CQRS057 bzw. CQRS100), nicht der Weckplan");
        quelle.Should().NotContain("batch-Falsch").And.NotContain("funktion-Falsche",
            "ein Hörer aus dem Log hört nur konkrete, persistente Events — alles andere ist ein Formfehler");
    }
}
