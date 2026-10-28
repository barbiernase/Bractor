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
/// Der Weckplan (Plan Pub/Sub-Neustrukturierung §2.3, Phase 6) entsteht NUR aus Signaturen: eine Projektion mit <c>Handle(TEvent, IAggregateEnvelope,
/// ProjectionWriter, …)</c> steht mit ihrem Batch-Kind unter TEvent, eine Funktion mit <c>Auf(TEvent …)</c> mit ihrem Funktions-Kind (eine Adresse,
/// wenn sie <c>[EineAdresse]</c> trägt), die Uhr unter <c>FristGesetzt</c>; ein Hinweis-Eingang steht nicht darin. Was ein Hörer aus dem Log hört und
/// der Plan nicht führen kann (Schnittstelle, abstrakt, Hinweis), ist CQRS108.
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
public interface IKontoEreignis : IEvent { }
public abstract record Basis : IEvent;
public interface ISchreibeKonto : IWriteStore { Task SetzeAsync(Guid konto); }
}
";

    private static (string Quelle, string[] Ids) Lauf(string code)
    {
        _ = typeof(IFunktion);
        _ = typeof(FunktionsBaustein);
        _ = typeof(Core.ProjectionWriter);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
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
        public sealed class KontoProjektion : ISubscriber, IPullSubscriber
        {
            public string SubscriberId => ""konto"";
            public Task Handle(Ueberfaellig e, IAggregateEnvelope env, ProjectionWriter w, ISchreibeKonto s) => Task.CompletedTask;
            public Task Handle(Bezahlt e, IAggregateEnvelope env, ProjectionWriter w, ISchreibeKonto s) => Task.CompletedTask;
        } }";

    [Fact]
    public void Projektion_Funktion_und_Uhr_stehen_mit_ihren_Kinds_im_Plan()
    {
        var (quelle, ids) = Lauf(Projektion + @"namespace Probe {
            public sealed class Mahnwesen : IFunktion { public Mahne? Auf(Ueberfaellig e) => null; }
            [EineAdresse] public sealed class Statistik : IFunktion { public Mahne? Auf(Bezahlt e) => null; } }");

        ids.Should().BeEmpty();
        quelle.Should().Contain("[typeof(global::Probe.Ueberfaellig)] = new[] { new Weckziel(\"batch-KontoProjektion\"), new Weckziel(\"funktion-Mahnwesen\") }");
        quelle.Should().Contain("[typeof(global::Probe.Bezahlt)] = new[] { new Weckziel(\"batch-KontoProjektion\"), new Weckziel(\"funktion-Statistik\", EineAdresse: true) }",
            "eine Funktion mit einer Adresse wird an dieser geweckt, nicht je Partition");
        quelle.Should().Contain("[typeof(global::Abstractions.FristGesetzt)] = new[] { new Weckziel(\"uhr\", EineAdresse: true) }", "eine gesetzte Frist weckt die Uhr");
    }

    [Fact]
    public void Ein_Hinweis_als_Eingang_einer_Funktion_steht_nicht_im_Plan()
    {
        var (quelle, ids) = Lauf(@"namespace Probe {
            public sealed class Sensorik : IFunktion { public Mahne? Auf(Messwert m) => null; } }");

        ids.Should().BeEmpty();
        quelle.Should().NotContain("Messwert", "ein Hinweis steht nicht im Log, ihn stellt das Hinweis-Netz zu");
    }

    [Theory]
    [InlineData("IKontoEreignis")]
    [InlineData("Basis")]
    [InlineData("Messwert")]
    public void Eine_Projektion_die_keinen_konkreten_persistenten_Typ_hoert_ist_CQRS108(string typ)
    {
        var (quelle, ids) = Lauf(@"namespace Probe {
            public sealed class Falsch : ISubscriber, IPullSubscriber
            {
                public string SubscriberId => ""falsch"";
                public Task Handle(" + typ + @" e, IAggregateEnvelope env, ProjectionWriter w, ISchreibeKonto s) => Task.CompletedTask;
            } }");

        ids.Should().Contain(Infrastructure.SourceGeneration.WeckplanGenerator.NichtImPlanId);
        quelle.Should().NotContain("batch-Falsch", "was der Plan nicht führen kann, steht nicht darin");
    }
}
