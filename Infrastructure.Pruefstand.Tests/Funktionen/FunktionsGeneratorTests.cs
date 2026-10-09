using System;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Der <c>FunktionsGenerator</c> liest Katalog-Funktionen NUR aus der Signatur: genau eine Methode
/// <c>Task&lt;OneOf&lt;Events&gt;&gt; RufeAsync(TAuftrag, IAusfuehrung)</c> → reflexionsfreier Dispatch + Ergebnis-Tabelle.
/// Falsche Form ist CQRS068, ein Auftrag für zwei Funktionen CQRS069 — beides bricht den Build statt still zu fehlen.
/// Dazu: <c>Rufe</c> braucht den Funktions-Typ explizit (CQRS003, wie <c>Sende</c>).
/// </summary>
public class FunktionsGeneratorTests
{
    private const string Vorspann = @"
using System; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public sealed record Gemacht(string X) : IEvent;
public sealed record Kaputt(string Grund) : IEvent;
public sealed record Hinweis() : ITransientEvent;
}
";

    private static (string Quelle, string[] Ids) Lauf(ISourceGenerator generator, string code)
    {
        _ = typeof(Abstractions.IFunktion);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("FunktionsProbe",
            new[] { CSharpSyntaxTree.ParseText(Vorspann), CSharpSyntaxTree.ParseText("using System; using System.Threading.Tasks; using Abstractions;\n" + code) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");

        CSharpGeneratorDriver.Create(generator).RunGeneratorsAndUpdateCompilation(comp, out var neu, out var diags);
        var quelle = string.Join("\n", neu.SyntaxTrees.Skip(2).Select(t => t.ToString()));
        return (quelle, diags.Select(d => d.Id).ToArray());
    }

    private static (string Quelle, string[] Ids) Funktionen(string code) => Lauf(new Infrastructure.SourceGeneration.FunktionsGenerator(), code);

    [Fact]
    public void Eine_Funktion_in_fester_Form_ergibt_Dispatch_und_Ergebnis_Tabelle()
    {
        var (quelle, ids) = Funktionen(@"namespace Probe {
            public sealed record MachAuftrag(string X) : IAuftrag<IMach>;
            public interface IMach : IFunktion { Task<OneOf<Gemacht, Kaputt>> RufeAsync(MachAuftrag a, IAusfuehrung x); } }");

        ids.Should().BeEmpty();
        quelle.Should().Contain("[typeof(global::Probe.MachAuftrag)] = typeof(global::Probe.IMach)");
        quelle.Should().Contain("[typeof(global::Probe.IMach)] = new Type[] { typeof(global::Probe.Gemacht), typeof(global::Probe.Kaputt) }");
        quelle.Should().MatchRegex(@"case global::Probe\.MachAuftrag a(\d+): return Rufe\1\(sp, a\1, x\);");
        quelle.Should().Contain("GetRequiredService<global::Probe.IMach>().RufeAsync(a, x)");
    }

    [Theory]
    [InlineData("Task<OneOf<Gemacht>> Mach(MachAuftrag a, IAusfuehrung x);")]                          // falscher Name
    [InlineData("Task<OneOf<Gemacht>> RufeAsync(MachAuftrag a);")]                                     // ohne IAusfuehrung
    [InlineData("OneOf<Gemacht> RufeAsync(MachAuftrag a, IAusfuehrung x);")]                           // nicht Task
    [InlineData("Task<OneOf<Gemacht, Hinweis>> RufeAsync(MachAuftrag a, IAusfuehrung x);")]            // transientes Ergebnis
    [InlineData("Task<OneOf<Gemacht>> RufeAsync(MachAuftrag a, IAusfuehrung x); Task<OneOf<Gemacht>> RufeAsync(string s, IAusfuehrung x);")] // zwei Methoden
    public void Eine_Funktion_ausser_Form_ist_CQRS068(string methode)
    {
        var (quelle, ids) = Funktionen(@"namespace Probe {
            public sealed record MachAuftrag(string X) : IAuftrag<IMach>;
            public interface IMach : IFunktion { " + methode + " } }");
        ids.Should().Contain("CQRS068");
        quelle.Should().NotContain("global::Probe.IMach>().RufeAsync", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    [Fact]
    public void Lese_Faehigkeiten_nach_IAusfuehrung_werden_je_Aufruf_aus_einem_Bereich_geholt()
    {
        var (quelle, ids) = Funktionen(@"namespace Probe {
            public interface ISuche : IReadStore { Task<int> ZaehleAsync(string x); }
            public sealed record MachAuftrag(string X) : IAuftrag<IMach>;
            public interface IMach : IFunktion { Task<OneOf<Gemacht>> RufeAsync(MachAuftrag a, IAusfuehrung x, ISuche suche); } }");

        ids.Should().BeEmpty();
        quelle.Should().Contain("[typeof(global::Probe.IMach)] = new Type[] { typeof(global::Probe.ISuche) }", "die Fähigkeiten-Tabelle (Boot-Guard: nur im Host)");
        quelle.Should().Contain("using var b = sp.GetRequiredService<IFaehigkeitsFabrik>().Oeffne();")
            .And.Contain(".RufeAsync(a, x, b.Hole<global::Probe.ISuche>())");
    }

    [Theory]
    [InlineData("public interface ISchreibe : IWriteStore { Task SchreibeAsync(string x); }", "ISchreibe")]   // schreibt
    [InlineData("", "string")]                                                                             // keine Fähigkeit
    public void Ein_Parameter_der_keine_Lese_Faehigkeit_ist_ist_CQRS068(string zusatz, string typ)
    {
        var (quelle, ids) = Funktionen(@"namespace Probe { " + zusatz + @"
            public sealed record MachAuftrag(string X) : IAuftrag<IMach>;
            public interface IMach : IFunktion { Task<OneOf<Gemacht>> RufeAsync(MachAuftrag a, IAusfuehrung x, " + typ + @" f); } }");
        ids.Should().Contain("CQRS068");
        quelle.Should().NotContain("global::Probe.IMach>().RufeAsync");
    }

    [Fact]
    public void Ein_Auftrag_der_zu_einer_anderen_Funktion_gehoert_ist_CQRS068()
    {
        Funktionen(@"namespace Probe {
            public sealed record FremdAuftrag(string X) : IAuftrag<IAnders>;
            public interface IAnders : IFunktion { Task<OneOf<Gemacht>> RufeAsync(FremdAuftrag a, IAusfuehrung x); }
            public interface IMach : IFunktion { Task<OneOf<Gemacht>> RufeAsync(FremdAuftrag a, IAusfuehrung x); } }")
            .Ids.Should().Contain("CQRS068");
    }

    [Fact]
    public void Ein_Auftrag_fuer_zwei_Funktionen_ist_CQRS069()
    {
        Funktionen(@"namespace Probe {
            public sealed record Doppel(string X) : IAuftrag<IEins>, IAuftrag<IZwei>;
            public interface IEins : IFunktion { Task<OneOf<Gemacht>> RufeAsync(Doppel a, IAusfuehrung x); }
            public interface IZwei : IFunktion { Task<OneOf<Kaputt>> RufeAsync(Doppel a, IAusfuehrung x); } }")
            .Ids.Should().Contain("CQRS069");
    }

    [Fact]
    public void Ohne_Funktionen_entsteht_ein_leerer_Dispatch()
    {
        var (quelle, ids) = Funktionen("namespace Probe { }");
        ids.Should().BeEmpty();
        quelle.Should().Contain("public static class GeneratedFunktionen").And.Contain("default: throw new NotSupportedException");
    }

    [Fact]
    public void Rufe_ohne_expliziten_Funktions_Typ_ist_CQRS003()
    {
        const string prozess = @"namespace Probe {
            public sealed record Los(string X) : IEvent;
            public sealed record MachAuftrag(string X) : IAuftrag<IMach>;
            public interface IMach : IFunktion { Task<OneOf<Gemacht>> RufeAsync(MachAuftrag a, IAusfuehrung x); }
            public sealed class P : IProzessDefinition {
                public ProzessRegeln Regeln => Prozess<Los>.Definiere(p => p.Auf<Los>().{0}(e => new MachAuftrag(e.X))); } }";

        Lauf(new Domain.SourceGeneration.ProzessRegelDiagnosticGenerator(), prozess.Replace("{0}", "Rufe<IMach>")).Ids.Should().BeEmpty();
        Lauf(new Domain.SourceGeneration.ProzessRegelDiagnosticGenerator(), prozess.Replace("{0}", "Rufe")).Ids.Should().Contain("CQRS003");
    }
}
