using System;
using System.Collections.Generic;
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
/// Werkbank P7.3: Ableitungen nur auf dem Server. Die Sonden-Domäne „Leihwesen“ wird wie in der Werkbank gelesen (Extraktion → Board); die
/// Sicht (<c>board.sicht</c>, <see cref="Sicht"/>) trägt Domäne, Block, Kanten, Einklappen, Inseln, die statische Kette und das Urteil für ⊕.
/// Geprüft wird an Code-Fakten der Sonde — nie an Namen geraten: eine Projektion im Namespace <c>Leihwesen.Lesen</c> steht in der Domäne
/// des Aggregats, dessen Events sie liest.
/// </summary>
public class SichtTests
{
    private static async Task<JsonObject> BoardAsync()
    {
        _ = typeof(Abstractions.IEvent); _ = typeof(Core.ProjectionWriter);
        var refs = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        using var ws = new AdhocWorkspace();
        var projekt = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Leihwesen", "Leihwesen", LanguageNames.CSharp,
            filePath: "/sonde/Leihwesen.csproj", metadataReferences: refs,
            compilationOptions: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        foreach (var (name, text) in Sonde.Quelltexte().Append(("GlobalUsings.cs",
                     "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;\nglobal using System.Threading.Tasks;\n")))
            ws.AddDocument(DocumentInfo.Create(DocumentId.CreateNewId(projekt.Id), name,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())), filePath: "/sonde/" + name));
        var loesung = ws.CurrentSolution;
        var comp = (await loesung.Projects.Single().GetCompilationAsync())!;
        var dom = new DomainExtractor([comp], new HashSet<string> { "Leihwesen" }, new Quelltexte(loesung)).Extract();
        dom.Wurzel = "/sonde";
        return ModellMapper.ZuBoard(new GraphBuilder(RoutingTruth.AusDecideSignaturen([comp]), dom).Build(), dom, new CompositionRoot());
    }

    private static string Id(JsonObject board, string sammlung, string feld, string wert) =>
        (string)(board[sammlung] as JsonArray)!.OfType<JsonObject>().First(x => (string?)x[feld] == wert)["_id"]!;
    private static JsonObject Karte(JsonObject board, string id) => (JsonObject)board["sicht"]!["karten"]![id]!;

    [Fact]
    public async Task Das_Board_traegt_die_Sicht_Karten_Kanten_Kette()
    {
        var board = await BoardAsync();
        var s = board["sicht"]!.AsObject();
        var dec = "dec:" + Id(board, "decider", "command", "LeiheAus");
        s["karten"]!.AsObject().ContainsKey(dec).Should().BeTrue();
        var kanten = s["kanten"]!.AsArray().Select(k => (string)k![0]! + "→" + (string)k[1]!).ToList();
        kanten.Should().Contain(["rec:LeiheAus→" + dec, dec + "→rec:Ausgeliehen", dec + "→agg:Leihvorgang"]);
        // Die Prozess-Regeln werden Kette (Kanten statt Karten): Start → Command, Funktion → Command.
        var kette = s["kette"]!.AsArray().Select(k => (string)k!["von"]! + "→" + (string)k["nach"]!).ToList();
        kette.Should().Contain("saga:SchadenBeiRueckgabe→rec:MeldeSchaden");
        kette.Should().Contain(x => x.StartsWith("fk:") && x.EndsWith("→rec:EroeffneMahnung"));
    }

    [Fact]
    public async Task Domaene_und_Block_folgen_dem_Graphen_nicht_dem_Namespace()
    {
        var board = await BoardAsync();
        Karte(board, "rec:Ausgeliehen")["block"]!.GetValue<string>().Should().Be("Leihvorgang");
        Karte(board, "rec:Ausgeliehen")["domaene"]!.GetValue<string>().Should().Be("Leihwesen.Ausleihe");
        Karte(board, "rec:Ausgeliehen")["aggregat"]!.GetValue<string>().Should().Be("Leihvorgang");
        // Projektion, Reader, Store und ReadModel stehen in Leihwesen.Lesen — gehören aber zum Leihvorgang (sie lesen seine Events).
        var prj = "prj:" + Id(board, "projektionen", "name", "Leihliste");
        Karte(board, prj)["domaene"]!.GetValue<string>().Should().Be("Leihwesen.Ausleihe");
        Karte(board, prj)["storeScope"]!.AsArray().Select(x => (string)x!).Should().Equal("ILeihBuch");
        Karte(board, "rdr:" + Id(board, "reader", "name", "LeihAuskunftLeser"))["block"]!.GetValue<string>().Should().Be("Leihvorgang");
        Karte(board, "sto:" + Id(board, "stores", "name", "ILeihBuch"))["domaene"]!.GetValue<string>().Should().Be("Leihwesen.Ausleihe");
        // Wert-Typ: die Signatur steckt nur in Records des Leihvorgangs → dort; Pipeline-Knoten haben ihren eigenen Rahmen.
        Karte(board, "rec:Signatur")["domaene"]!.GetValue<string>().Should().Be("Leihwesen.Ausleihe");
        var fl = board["fluesse"]!.AsArray().OfType<JsonObject>().First(f => (string?)f["name"] == "RueckgabePruefung");
        var knoten = "fs:" + (string)fl["knoten"]![0]!["_id"]!;
        Karte(board, knoten)["domaene"]!.GetValue<string>().Should().Be(Sicht.FlussPraefix + (string)fl["_id"]!);
    }

    [Fact]
    public async Task Details_klappen_in_ihren_Besitzer_ein_Inseln_bleiben_sichtbar_markiert()
    {
        var board = await BoardAsync();
        var dec = "dec:" + Id(board, "decider", "command", "LeiheAus");
        Karte(board, dec)["vertreter"]!.AsArray().Select(x => (string)x!).Should().Equal("rec:LeiheAus");
        Karte(board, "rec:Signatur")["vertreter"]!.AsArray().Select(x => (string)x!).Should().Contain(["rec:LeiheAus", "rec:Ausgeliehen"]);
        Karte(board, "rec:LeiheAus").ContainsKey("vertreter").Should().BeFalse("eine Karte, die niemandem gehört, vertritt sich selbst");
        // Der geteilte Zähler hat keinen Store und keine Nachbarn: eine Insel.
        Karte(board, "rm:" + Id(board, "readModels", "name", "Ausleihzaehler"))["insel"]!.GetValue<bool>().Should().BeTrue();
        Karte(board, "rec:LeiheAus").ContainsKey("insel").Should().BeFalse();
    }

    [Fact]
    public async Task Die_statische_Kette_folgt_dem_Nachrichtenfluss()
    {
        var board = await BoardAsync();
        var sicht = Sicht.Aus(board);
        var kette = sicht.KetteFuer("rec:LeiheAus", details: true);
        var dec = "dec:" + Id(board, "decider", "command", "LeiheAus");
        kette.Should().Contain([dec, "rec:Ausgeliehen", "agg:Leihvorgang", "prj:" + Id(board, "projektionen", "name", "Leihliste")]);
        kette.Should().NotContain("rec:MeldeSchaden", "die Kette läuft nicht über den Besitzer (Aggregat) in fremde Commands");
    }

    [Fact]
    public async Task Passt_urteilt_nach_Anschluss_und_Grammatik()
    {
        var board = await BoardAsync();
        var sicht = Sicht.Aus(board);
        var leiheAus = Id(board, "decider", "command", "LeiheAus");
        var gibZurueck = Id(board, "decider", "command", "GibZurueck");
        Sicht.Port P(string key, JsonObject info) => new(key, info);
        // Decider-Eingang ⊕ → ein Command: LeiheAus entscheidet schon ein anderer Decider (Kardinalität „genau 1“) → gesperrt mit Regel.
        var q = P("dec:cmd:" + gibZurueck, new JsonObject { ["type"] = "cmd", ["dir"] = "in", ["dec"] = gibZurueck });
        var u = sicht.Passt(q, [
            P("k1", new JsonObject { ["type"] = "cmd", ["dir"] = "out", ["rec"] = "LeiheAus" }),
            P("k2", new JsonObject { ["type"] = "cmd", ["dir"] = "out", ["rec"] = "GibZurueck" }),
            P("k3", new JsonObject { ["type"] = "evtOut", ["dir"] = "out", ["rec"] = "Ausgeliehen" }),
        ], new HashSet<string>());
        u.Passt.Should().Equal("k2");
        u.Gesperrt.Should().ContainSingle(g => g.Key == "k1" && g.Text.Contains("Decider (Leihvorgang)"));
        // Dieselbe Stelle (der Decider, der LeiheAus schon entscheidet) bleibt verbindbar; ein schon gezeichneter Partner bleibt lösbar.
        var q2 = P("dec:cmd:" + leiheAus, new JsonObject { ["type"] = "cmd", ["dir"] = "in", ["dec"] = leiheAus });
        sicht.Passt(q2, [P("k1", new JsonObject { ["type"] = "cmd", ["dir"] = "out", ["rec"] = "LeiheAus" })], new HashSet<string>()).Passt.Should().Equal("k1");
        sicht.Passt(q, [P("k1", new JsonObject { ["type"] = "cmd", ["dir"] = "out", ["rec"] = "LeiheAus" })], new HashSet<string> { "k1" }).Passt.Should().Equal("k1");
    }
}
