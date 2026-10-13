using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using GraphExtractor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Infrastructure.Pruefstand.Extractor;

/// <summary>
/// Werkbank P7.1 (konzept-werkbank §8.2): der Extractor liest Code MIT Fehlern — nichts verschwindet still, nichts wirft. Ein unaufgelöster Basistyp wird eine
/// Code-Karte (bzw. ein Befund an der Karte), eine Fluss-Anweisung ohne Knoten ein Befund am Fluss, ein doppelter Name ein Befund statt einer
/// Ausnahme. Dazu: jede Karte trägt Datei + Spanne. In-Memory-Workspace (kein MSBuild), Vertrag als Metadaten.
/// </summary>
public class LesenToleranzTests
{
    private const string Probe = @"
using System; using System.Threading.Tasks; using Abstractions;
namespace Probe
{
    public sealed record Kaputt(string Name) : IEvnt;
    public record Teilweise(int Wert) : IEvent, IFehlt;
    public sealed record DateiDa(string Pfad) : IQuellNachricht { public string Kennung => Pfad; }
    public sealed record Melde(Guid AggregateId, string Pfad) : ICommand;
    public sealed class ProbeFluss : IPipeline
    {
        public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
        {
            var eingang = p.Quelle<DateiDa>();
            var zahl = 1;
            eingang.Sende<Melde>(e => new Melde(Guid.Empty, e.Pfad));
        });
    }
}
namespace Probe.A { public interface ILiesZahl : IReadStore { Task<int> LiesAsync(string id); } }
namespace Probe.B { public interface ILiesZahl : IReadStore { Task<int> LiesAsync(string id); } }
";

    private static async Task<JsonObject> LiesAsync(string code)
    {
        _ = typeof(Abstractions.IEvent);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        using var ws = new AdhocWorkspace();
        var projekt = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Probe.Domain", "Probe.Domain",
            LanguageNames.CSharp, filePath: "/probe/Probe.Domain.csproj", metadataReferences: refs,
            compilationOptions: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        ws.AddDocument(DocumentInfo.Create(DocumentId.CreateNewId(projekt.Id), "Probe.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code), VersionStamp.Create())), filePath: "/probe/Probe.cs"));
        var solution = ws.CurrentSolution;
        var comp = (await solution.Projects.Single().GetCompilationAsync())!;
        var quelltexte = new Quelltexte(solution);
        var dom = new DomainExtractor([comp], new System.Collections.Generic.HashSet<string> { "Probe.Domain" }, quelltexte).Extract();
        dom.Wurzel = "/probe";
        var graph = new GraphBuilder(RoutingTruth.AusDecideSignaturen([comp]), dom).Build();
        return ModellMapper.ZuBoard(graph, dom, new CompositionRoot());
    }

    private static JsonObject[] Alle(JsonObject board, string sammlung) => (board[sammlung] as JsonArray ?? []).OfType<JsonObject>().ToArray();

    [Fact]
    public async Task Unaufgeloester_Basistyp_wird_Code_Karte_mit_Befund_statt_still_zu_verschwinden()
    {
        var board = await LiesAsync(Probe);

        var karte = Alle(board, "codeKarten").Should().ContainSingle(c => (string?)c["name"] == "Kaputt").Subject;
        ((string?)karte["_id"]).Should().Be("code:Kaputt");
        karte["unaufgeloest"]!.AsArray().Select(x => (string?)x).Should().Equal("IEvnt");
        ((string?)karte["code"]).Should().Contain("record Kaputt");
        ((string?)karte["_ort"]!["datei"]).Should().Be("Probe.cs");
        Alle(board, "records").Should().NotContain(r => (string?)r["name"] == "Kaputt", "ohne aufgelöste Basis ist die Rolle unbekannt — kein Value Object raten");
        Alle(board, "lesebefunde").Should().Contain(b => (string?)b["code"] == "LESEN-BASIS" && (string?)b["karte"] == "code:Kaputt");
    }

    [Fact]
    public async Task Teilweise_aufgeloester_Typ_bleibt_Baustein_und_traegt_den_Befund()
    {
        var board = await LiesAsync(Probe);

        Alle(board, "records").Should().Contain(r => (string?)r["name"] == "Teilweise" && (string?)r["kind"] == "event");
        Alle(board, "codeKarten").Should().NotContain(c => (string?)c["name"] == "Teilweise");
        Alle(board, "lesebefunde").Should().Contain(b => (string?)b["code"] == "LESEN-BASIS" && (string?)b["karte"] == "rec:Teilweise"
                                                        && ((string?)b["text"])!.Contains("IFehlt"));
    }

    [Fact]
    public async Task Fluss_Anweisung_ohne_Knoten_wird_Befund_am_Fluss_und_die_Knoten_bleiben()
    {
        var board = await LiesAsync(Probe);

        var fluss = Alle(board, "fluesse").Should().ContainSingle(f => (string?)f["name"] == "ProbeFluss").Subject;
        Alle(fluss, "knoten").Select(k => (string?)k["art"]).Should().Equal("quelle", "command");
        Alle(board, "lesebefunde").Should().Contain(b => (string?)b["code"] == "LESEN-FLUSS" && (string?)b["karte"] == "pipeline:ProbeFluss"
                                                        && ((string?)b["text"])!.Contains("var zahl = 1;") && (int?)b["zeile"] == 14);
    }

    [Fact]
    public async Task Doppelter_Name_wirft_nicht_sondern_wird_Befund()
    {
        var board = await LiesAsync(Probe);

        Alle(board, "lesebefunde").Should().Contain(b => (string?)b["code"] == "LESEN-DOPPELT" && (string?)b["karte"] == "store:ILiesZahl");
    }

    [Fact]
    public async Task Karten_tragen_Datei_und_Spanne_ihrer_Deklaration()
    {
        var board = await LiesAsync(Probe);

        var rec = Alle(board, "records").Single(r => (string?)r["name"] == "DateiDa");
        var ort = rec["_ort"]!.AsObject();
        Probe[(int)ort["start"]!..(int)ort["ende"]!].Should().StartWith("public sealed record DateiDa(string Pfad)").And.EndWith("}");
        ((int?)ort["zeile"]).Should().Be(7);
        // Fluss-Knoten: die Anweisung im Definiere-Lambda.
        var knoten = Alle(Alle(board, "fluesse").Single(), "knoten");
        var o = knoten[1]["_ort"]!.AsObject();
        Probe[(int)o["start"]!..(int)o["ende"]!].Should().Be("eingang.Sende<Melde>(e => new Melde(Guid.Empty, e.Pfad));");
    }

    private const string MitHilfscode = @"
using System; using System.Threading.Tasks; using Abstractions;
namespace Probe
{
    public sealed record Zaehler : IReadModel { public string Id { get; init; } = """"; }
    public interface ISetzeZaehler : IWriteStore { Task SetzeAsync(Zaehler z); }
    public interface ILiesZaehler : IReadStore { Task<Zaehler?> LiesAsync(string id); }
    public interface IZaehlerBuch : IStore, ISetzeZaehler, ILiesZaehler { }
    public sealed class ZaehlerBuch : IZaehlerBuch
    {
        public Task SetzeAsync(Zaehler z) => Task.CompletedTask;
        public Task<Zaehler?> LiesAsync(string id) => Task.FromResult<Zaehler?>(null);
    }
    public static partial class Rechner { public static int Doppelt(int x) => 2 * x; }
    public static partial class Rechner { public static int Halb(int x) => x / 2; }
}
";

    [Fact]
    public async Task Typ_ohne_Baustein_wird_Code_Karte_und_alles_andere_liegt_in_einer_Karten_Spanne()
    {
        var board = await LiesAsync(MitHilfscode);

        // Werkbank P7.4 (Zielbild §9): nur der Hilfscode ist roh — beide partial-Teile an EINER Karte.
        var karte = Alle(board, "codeKarten").Should().ContainSingle().Subject;
        ((string?)karte["_id"]).Should().Be("code:Rechner");
        ((string?)karte["art"]).Should().Be("class");
        karte["_teile"]!.AsArray().Should().HaveCount(1);
        ((string?)karte["code"]).Should().Contain("Doppelt").And.Contain("Halb");
        // Die Store-Implementierung gehört zur Store-Karte, jede Store-Funktion steht an ihrer Fähigkeit.
        var store = Alle(board, "stores").Single();
        var spannen = store["_teile"]!.AsArray().Select(t => MitHilfscode[(int)t!["start"]!..(int)t["ende"]!]).ToList();
        spannen.Should().Contain(x => x.StartsWith("public sealed class ZaehlerBuch"));
        Alle(store, "writeFns").Concat(Alle(store, "readFns")).Select(f => MitHilfscode[(int)f["_ort"]!["start"]!..(int)f["_ort"]!["ende"]!])
            .Should().BeEquivalentTo("public interface ISetzeZaehler : IWriteStore { Task SetzeAsync(Zaehler z); }",
                "public interface ILiesZaehler : IReadStore { Task<Zaehler?> LiesAsync(string id); }");
        // Jede Typ-Deklaration liegt in der Spanne einer Karte (das Gate von --check).
        var abdeckung = ModellMapper.Abdeckung(board);
        foreach (var name in new[] { "Zaehler", "ISetzeZaehler", "ILiesZaehler", "IZaehlerBuch", "ZaehlerBuch", "Rechner" })
        {
            var i = MitHilfscode.IndexOf(" " + name + " ", StringComparison.Ordinal);
            abdeckung["Probe.cs"].Should().Contain(x => x.Start <= i && i < x.Ende, name);
        }
    }
}
