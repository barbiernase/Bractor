using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
/// Werkbank P7.2: Geste → Patch. Die Sonden-Domäne „Leihwesen“ liegt als echte Dateien in einem Temp-Verzeichnis; gelesen wird wie in der
/// Werkbank (Extraktion → Board), die Geste ist eine Karten-Änderung, der Patch entsteht über den Schreiber (Scaffolder + Abgleich), Löschen
/// über den Ort der Karte, Umbenennen über den Roslyn-Renamer. Geprüft wird der Code danach — und dass Unbetroffenes Wort für Wort bleibt.
/// </summary>
public sealed class GestenTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "gesten-" + Guid.NewGuid().ToString("N")[..8]);
    private string Projekt => Path.Combine(_wurzel, "Leihwesen");

    public GestenTests()
    {
        Directory.CreateDirectory(Projekt);
        foreach (var (name, text) in Sonde.Quelltexte()) File.WriteAllText(Path.Combine(Projekt, name), text);
        File.WriteAllText(Path.Combine(Projekt, "GlobalUsings.cs"),
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;\nglobal using System.Threading;\nglobal using System.Threading.Tasks;\n");
    }

    public void Dispose() { try { Directory.Delete(_wurzel, true); } catch (IOException) { } }

    private string Text(string datei) => File.ReadAllText(Path.Combine(Projekt, datei));

    private Solution Loesung()
    {
        _ = typeof(Abstractions.IEvent); _ = typeof(Core.MartenStore); _ = typeof(Marten.IDocumentStore);
        var refs = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var ws = new AdhocWorkspace();
        var id = ProjectId.CreateNewId();
        var loesung = ws.CurrentSolution.AddProject(ProjectInfo.Create(id, VersionStamp.Create(), "Leihwesen", "Leihwesen", LanguageNames.CSharp,
            filePath: Path.Combine(Projekt, "Leihwesen.csproj"), metadataReferences: refs,
            compilationOptions: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        foreach (var datei in Directory.GetFiles(Projekt, "*.cs").OrderBy(x => x, StringComparer.Ordinal))
            loesung = loesung.AddDocument(DocumentId.CreateNewId(id), Path.GetFileName(datei), SourceText.From(File.ReadAllText(datei)), filePath: datei);
        return loesung;
    }

    /// <summary>Lesen wie die Werkbank: Extraktion → Board (Code-Stand, mit Orten).</summary>
    private async Task<JsonObject> BoardAsync()
    {
        var loesung = Loesung();
        var comp = (await loesung.Projects.Single().GetCompilationAsync())!;
        var dom = new DomainExtractor([comp], new HashSet<string> { "Leihwesen" }, new Quelltexte(loesung)).Extract();
        dom.Wurzel = _wurzel;
        dom.ProjektWurzeln["Leihwesen"] = Projekt;
        return ModellMapper.ZuBoard(new GraphBuilder(RoutingTruth.AusDecideSignaturen([comp]), dom).Build(), dom, new CompositionRoot());
    }

    private static JsonObject Karte(JsonObject board, string sammlung, Func<JsonObject, bool> wo) =>
        (JsonObject)(board[sammlung] as JsonArray)!.OfType<JsonObject>().First(wo).DeepClone();

    private static KartenDelta Geaendert(string sammlung, params JsonObject[] karten) =>
        new() { Karten = new() { [sammlung] = karten.ToList() } };

    private async Task<GestenBericht> GesteAsync(KartenDelta delta)
    {
        var puffer = new Dateipuffer(_wurzel);
        var bericht = Gesten.Schreibe(await BoardAsync(), delta, puffer);
        puffer.Speichern();
        return bericht;
    }

    private static JsonArray Liste(params string[] xs) => new(xs.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

    [Fact]
    public async Task Unveraenderte_Karte_ergibt_einen_leeren_Patch()
    {
        var board = await BoardAsync();
        var delta = new KartenDelta
        {
            Karten = new()
            {
                ["records"] = [Karte(board, "records", r => (string?)r["name"] == "Ausgeliehen")],
                ["projektionen"] = [Karte(board, "projektionen", p => (string?)p["name"] == "Leihliste")],
            },
        };

        (await GesteAsync(delta)).Patch.Should().BeEmpty("eine Geste ohne Änderung schreibt nichts");
    }

    [Fact]
    public async Task Feld_an_bestehendem_Record_aendert_nur_die_Parameterliste()
    {
        var vorher = Text("Ausleihe.cs.txt".Replace(".txt", ""));
        var karte = Karte(await BoardAsync(), "records", r => (string?)r["name"] == "Ausgeliehen");
        karte["felder"]!.AsArray().Add(new JsonObject { ["name"] = "Notiz", ["typ"] = "string" });

        var bericht = await GesteAsync(Geaendert("records", karte));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/Ausleihe.cs");
        Text("Ausleihe.cs").Should().Contain("public record Ausgeliehen(Signatur Medium, Guid LeserId, int Tage, string Notiz) : IEvent;");
        Text("Ausleihe.cs").Replace(", string Notiz", "").Should().Be(vorher, "alles andere bleibt Wort für Wort");
    }

    [Fact]
    public async Task Enum_Wert_hinzu()
    {
        var karte = Karte(await BoardAsync(), "enums", e => (string?)e["name"] == "Zustand");
        karte["werte"]!.AsArray().Add("Verloren");

        await GesteAsync(Geaendert("enums", karte));

        Text("Ausleihe.cs").Should().Contain("Beschaedigt = 3, Verloren");
    }

    [Fact]
    public async Task Decider_bekommt_einen_weiteren_Ausgang_nur_das_Typ_Argument_aendert_sich()
    {
        var karte = Karte(await BoardAsync(), "decider", d => (string?)d["command"] == "MeldeSchaden");
        karte["ergibt"]!.AsArray().Add(new JsonObject { ["event"] = "MahnungEroeffnet" });

        await GesteAsync(Geaendert("decider", karte));

        Text("Gebuehren.cs").Should().Contain("public IEnumerable<OneOf<SchadenGemeldet, SchadenExistiert, MahnungEroeffnet>> Decide(MeldeSchaden c)")
            .And.Contain("if (this.State.Gemeldet) { yield return new SchadenExistiert(c.AggregateId); yield break; }", "der Rumpf gehört dem Code");
    }

    [Fact]
    public async Task Neuer_Command_wird_im_Namespace_seiner_Domaene_angelegt()
    {
        var board = await BoardAsync();
        var vorlage = Karte(board, "records", r => (string?)r["name"] == "MeldeSchaden");
        var neu = new JsonObject
        {
            ["name"] = "StorniereSchaden", ["kind"] = "command", ["namespace"] = (string?)vorlage["namespace"],
            ["felder"] = new JsonArray(new JsonObject { ["name"] = "AggregateId", ["typ"] = "Guid" }),
        };

        var bericht = await GesteAsync(Geaendert("records", neu));

        bericht.Patch.Should().ContainSingle();
        Text(Path.GetFileName(bericht.Patch[0].Datei)).Should().Contain("record StorniereSchaden(Guid AggregateId) : ICommand");
    }

    [Fact]
    public async Task Record_loeschen_entfernt_die_Deklaration_samt_Kommentar()
    {
        Text("Fluss.cs").Should().Contain("public sealed record AusweisAbgelegt : DateiGemeldet;");

        var bericht = await GesteAsync(new KartenDelta { Entfernt = new() { ["records"] = ["Leihwesen.Pruefung|AusweisAbgelegt"] } });

        bericht.Geloescht.Should().ContainSingle();
        Text("Fluss.cs").Should().NotContain("AusweisAbgelegt").And.NotContain("Eine Quelle mit fest codiertem Treiber",
            "der Kommentar über der Deklaration geht mit");
        Text("Fluss.cs").Should().Contain("public sealed record RueckgabeGescannt(Guid LeserId, string Code, IReadOnlyList<string> Seiten) : IQuellNachricht");
    }

    [Fact]
    public async Task Decider_loeschen_entfernt_nur_seine_Methode()
    {
        var bericht = await GesteAsync(new KartenDelta { Entfernt = new() { ["decider"] = ["Leihvorgang|GibZurueck"] } });

        bericht.Geloescht.Should().ContainSingle();
        Text("Ausleihe.cs").Should().NotContain("Decide(GibZurueck befehl)").And.Contain("Decide(LeiheAus befehl)");
    }

    [Fact]
    public async Task Aggregat_loeschen_entfernt_alle_Teile_auch_in_eigener_Datei()
    {
        File.Exists(Path.Combine(Projekt, "Mahnvorgang.Decider.cs")).Should().BeTrue();

        var bericht = await GesteAsync(new KartenDelta
        {
            Entfernt = new() { ["aggregate"] = ["Leihwesen.Gebuehren|Mahnvorgang"], ["decider"] = ["Mahnvorgang|EroeffneMahnung"] },
        });

        bericht.NichtGeloescht.Should().BeEmpty();
        File.Exists(Path.Combine(Projekt, "Mahnvorgang.Decider.cs")).Should().BeFalse("ohne Typ darin entfällt die Datei");
        bericht.Patch.Should().Contain(p => p.Datei == "Leihwesen/Mahnvorgang.Decider.cs" && p.Nachher == null);
        Text("Gebuehren.cs").Should().NotContain("class Mahnvorgang").And.Contain("class Schadensfall");
    }

    [Fact]
    public async Task Projektions_Auf_bekommt_eine_Faehigkeit_als_Parameter()
    {
        var board = await BoardAsync();
        var karte = Karte(board, "projektionen", p => (string?)p["name"] == "Leihliste");
        var handle = karte["handles"]!.AsArray().OfType<JsonObject>().First(h => (string?)h["event"] == "Zurueckgegeben");
        handle["fns"]!.AsArray().Add("wf:ILeihBuch.SchreibeAsync");

        await GesteAsync(Geaendert("projektionen", karte));

        // Eingang und Herkunft bleiben vorn, die neue Fähigkeit kommt hinter die bestehende.
        Text("Lesen.cs").Should().Contain("public async Task Auf(Zurueckgegeben e, Herkunft h, ISchliesseAusleihe schliesse, INotiereAusleihe notiereAusleihe)")
            .And.Contain("Func<Guid, Task> tu = schliesse.SchliesseAsync;", "der Rumpf bleibt");
    }

    [Fact]
    public async Task Auf_loeschen_entfernt_seine_Methode()
    {
        var karte = Karte(await BoardAsync(), "projektionen", p => (string?)p["name"] == "Leihliste");
        var handles = karte["handles"]!.AsArray();
        handles.Remove(handles.OfType<JsonObject>().First(h => (string?)h["event"] == "Zurueckgegeben"));

        var bericht = await GesteAsync(Geaendert("projektionen", karte));

        bericht.Geloescht.Should().ContainSingle(x => x.Contains("Auf(Zurueckgegeben)"));
        Text("Lesen.cs").Should().NotContain("Task Auf(Zurueckgegeben e").And.Contain("Task Auf(Ausgeliehen e");
    }

    [Fact]
    public async Task Akteur_darf_einen_weiteren_Command()
    {
        var karte = Karte(await BoardAsync(), "akteure", a => (string?)a["name"] == "Gutachterin");
        karte["darf"]!.AsArray().Add("MeldeSchaden");

        await GesteAsync(Geaendert("akteure", karte));

        Text("Lesen.cs").Should().Contain("public sealed record Gutachterin : IKi, IDarf<EroeffneMahnung>, IDarf<MeldeSchaden>;");
    }

    [Fact]
    public async Task Client_sendet_einen_weiteren_Command()
    {
        var karte = Karte(await BoardAsync(), "clients", c => (string?)c["name"] == "ILesesaal");
        karte["sendet"]!.AsArray().Add("GibZurueck");

        await GesteAsync(Geaendert("clients", karte));

        Text("Lesen.cs").Should().Contain("ISendet<LeiheAus>, ISendet<GibZurueck>").And.Contain("IHoert<MahnungEroeffnet>");
    }

    [Fact]
    public async Task Umbenennen_zieht_alle_Verweise_mit()
    {
        var board = await BoardAsync();
        var ort = Karte(board, "records", r => (string?)r["name"] == "EroeffneMahnung")["_ort"]!;

        var (patch, alt) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)ort["datei"]!), (int)ort["start"]!, "OeffneMahnung", _wurzel);

        alt.Should().Be("EroeffneMahnung");
        patch.Select(p => p.Datei).Should().Contain(["Leihwesen/Gebuehren.cs", "Leihwesen/Fluss.cs", "Leihwesen/Lesen.cs"]);
        patch.Should().OnlyContain(p => !p.Nachher!.Contains("EroeffneMahnung"));
        patch.Single(p => p.Datei == "Leihwesen/Fluss.cs").Nachher.Should().Contain("new OeffneMahnung(h.NeueId<Mahnvorgang>(), anzahl * 2m)");
    }

    // ── Rumpf-Edit (Werkbank P7.4): ✎ am Rumpf-Port ersetzt genau den Bereich, den die Karte aus dem Code kennt ──

    private static RumpfOrt OrtVon(JsonNode o) => new((string)o["datei"]!, (int)o["start"]!, (int)o["ende"]!);

    private void RumpfGeste(RumpfOrt ort, string neu, string? alt)
    {
        var puffer = new Dateipuffer(_wurzel);
        Rumpf.Ersetze(puffer, ort, neu, alt);
        puffer.Speichern();
    }

    [Fact]
    public async Task Rumpf_Edit_ersetzt_genau_den_Methodenrumpf()
    {
        var vorher = Text("Ausleihe.cs");
        var dec = Karte(await BoardAsync(), "decider", d => (string?)d["command"] == "LeiheAus");

        RumpfGeste(OrtVon(dec["_ort"]!), "yield return new BereitsAusgeliehen(befehl.AggregateId);", (string?)dec["rumpf"]);

        var nachher = Text("Ausleihe.cs");
        nachher.Should().Contain("Decide(LeiheAus befehl)\n            {\n                yield return new BereitsAusgeliehen(befehl.AggregateId);\n            }");
        // Alles außerhalb des Rumpfs bleibt Wort für Wort — auch der zweite Decide.
        var start = vorher.IndexOf("Decide(GibZurueck", StringComparison.Ordinal);
        nachher.Should().EndWith(vorher[start..]);
        nachher[..nachher.IndexOf("Decide(LeiheAus", StringComparison.Ordinal)].Should().Be(vorher[..vorher.IndexOf("Decide(LeiheAus", StringComparison.Ordinal)]);
        (await BoardAsync())["decider"]!.AsArray().OfType<JsonObject>().Single(d => (string?)d["command"] == "LeiheAus")["rumpf"]!.GetValue<string>()
            .Should().Be("yield return new BereitsAusgeliehen(befehl.AggregateId);");
    }

    [Fact]
    public async Task Rumpf_Edit_behaelt_die_Prompt_Zeile()
    {
        var dec = Karte(await BoardAsync(), "decider", d => (string?)d["command"] == "LeiheAus");
        RumpfGeste(OrtVon(dec["_ort"]!), "// 🤖 Prompt: nur einmal ausleihen\nyield break;", (string?)dec["rumpf"]);
        var neu = Karte(await BoardAsync(), "decider", d => (string?)d["command"] == "LeiheAus");
        RumpfGeste(OrtVon(neu["_ort"]!), "// 🤖 Prompt: nur einmal ausleihen\nyield break; // geprüft", (string?)neu["rumpf"]);
        Text("Ausleihe.cs").Should().Contain("// 🤖 Prompt: nur einmal ausleihen\n                yield break; // geprüft");
    }

    [Fact]
    public async Task Rumpf_Edit_verweigert_wenn_der_Rumpf_inzwischen_anders_ist()
    {
        var dec = Karte(await BoardAsync(), "decider", d => (string?)d["command"] == "LeiheAus");
        var act = () => RumpfGeste(OrtVon(dec["_ort"]!), "yield break;", "yield return new Ausgeliehen(befehl.Medium, befehl.LeserId, 99);");
        act.Should().Throw<InvalidOperationException>().WithMessage("*inzwischen geändert*");
        Text("Ausleihe.cs").Should().Contain("yield return new Ausgeliehen(befehl.Medium, befehl.LeserId, befehl.Tage);");
    }

    [Fact]
    public async Task Store_Funktion_kennt_den_Ort_ihres_Rumpfs_in_der_Implementierung()
    {
        var board = await BoardAsync();
        var fn = (board["stores"] as JsonArray)!.OfType<JsonObject>().SelectMany(st => st["writeFns"]!.AsArray().OfType<JsonObject>())
            .First(f => (string?)f["name"] == "SchreibeAsync");
        var ort = OrtVon(fn["_rumpf"]!);
        ort.Datei.Should().Be("Leihwesen/Lesen.cs");
        Rumpf.Lies(Text("Lesen.cs"), ort).Should().Be((string?)fn["rumpf"]);
    }

    // ══ Dienste (docs/06 §6.5): Interface, Methoden und Implementierungen als Geste ══

    private static JsonObject Param(string typ, string name) => new() { ["typ"] = typ, ["name"] = name };

    [Fact]
    public async Task Neuer_Dienst_per_Plus_legt_Interface_und_Implementierung_an()
    {
        var dienst = new JsonObject
        {
            ["name"] = "ILeuchte", ["namespace"] = "Leihwesen.Gebuehren",
            ["methoden"] = new JsonArray(new JsonObject { ["name"] = "Hell", ["rueckgabe"] = "bool", ["parameter"] = new JsonArray(Param("int", "stufe")) }),
            ["implementierungen"] = new JsonArray(new JsonObject { ["name"] = "Leuchte" }),
        };

        var bericht = await GesteAsync(new KartenDelta { Karten = new() { ["dienste"] = [dienst] } });

        bericht.Patch.Select(p => p.Datei).Should().BeEquivalentTo("Leihwesen/ILeuchte.cs", "Leihwesen/Leuchte.cs");
        Text("ILeuchte.cs").Should().Contain("public interface ILeuchte : IDienst").And.Contain("    bool Hell(int stufe);");
        Text("Leuchte.cs").Should().Contain("public sealed class Leuchte : ILeuchte").And.Contain("public bool Hell(int stufe)")
            .And.Contain("throw new NotImplementedException(\"TODO: Leuchte.Hell\");");
    }

    [Fact]
    public async Task Methode_am_Dienst_kommt_ans_Interface_und_als_Stub_an_jede_Implementierung()
    {
        var dienst = Karte(await BoardAsync(), "dienste", d => (string?)d["name"] == "IWertTabelle");
        dienst["methoden"]!.AsArray().Add(new JsonObject { ["name"] = "Runde", ["rueckgabe"] = "decimal", ["parameter"] = new JsonArray(Param("decimal", "betrag")) });

        await GesteAsync(Geaendert("dienste", dienst));

        Text("Gebuehren.cs").Should().Contain("    Task<bool> IstSchaetzbarAsync(Guid leserId);\n    decimal Runde(decimal betrag);\n}")
            .And.Contain("    public decimal Runde(decimal betrag)\n    {\n        throw new NotImplementedException(\"TODO: LineareWertTabelle.Runde\");");
    }

    [Fact]
    public async Task Zweite_Implementierung_eines_Dienstes_ist_eine_neue_Klasse_mit_Stubs()
    {
        var dienst = Karte(await BoardAsync(), "dienste", d => (string?)d["name"] == "IWertTabelle");
        dienst["implementierungen"]!.AsArray().Add(new JsonObject { ["name"] = "FesteWertTabelle" });

        var bericht = await GesteAsync(Geaendert("dienste", dienst));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/FesteWertTabelle.cs");
        Text("FesteWertTabelle.cs").Should().Contain("public sealed class FesteWertTabelle : IWertTabelle")
            .And.Contain("public decimal Restwert(decimal neuwert, int alterJahre)").And.Contain("public Task<bool> IstSchaetzbarAsync(Guid leserId)");
    }

    [Fact]
    public async Task Umbenennen_eines_Typs_in_seiner_eigenen_Datei_zieht_die_Datei_mit()
    {
        var dienst = new JsonObject
        {
            ["name"] = "ILeuchte", ["namespace"] = "Leihwesen.Gebuehren",
            ["methoden"] = new JsonArray(new JsonObject { ["name"] = "Hell", ["rueckgabe"] = "bool", ["parameter"] = new JsonArray(Param("int", "stufe")) }),
            ["implementierungen"] = new JsonArray(new JsonObject { ["name"] = "Leuchte" }),
        };
        await GesteAsync(new KartenDelta { Karten = new() { ["dienste"] = [dienst] } });
        var karte = Karte(await BoardAsync(), "dienste", d => (string?)d["name"] == "ILeuchte");
        var methode = karte["methoden"]![0]!["_ort"]!;
        var impl = karte["implementierungen"]![0]!["_ort"]!;
        ((string?)impl["datei"]).Should().Be("Leihwesen/Leuchte.cs", "die Implementierung trägt ihren Ort — ihr Name ist umbenennbar");

        var (patch, _) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)impl["datei"]!), (int)impl["start"]!, "Lampe", _wurzel);

        patch.Should().Contain(p => p.Datei == "Leihwesen/Leuchte.cs" && p.Nachher == null, "die Datei <alt>.cs fällt weg …")
            .And.Contain(p => p.Datei == "Leihwesen/Lampe.cs" && p.Vorher == null && p.Nachher!.Contains("public sealed class Lampe : ILeuchte"), "… und heißt wie der Typ");

        (patch, _) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)methode["datei"]!), (int)methode["start"]!, "Strahlt", _wurzel);
        patch.Select(p => p.Datei).Should().BeEquivalentTo(["Leihwesen/ILeuchte.cs", "Leihwesen/Leuchte.cs"], "eine Methode benennt Interface und Implementierung um, keine Datei zieht");
        patch.Should().OnlyContain(p => p.Nachher!.Contains("Strahlt(int stufe)"));
    }

    [Fact]
    public void Ausdrucks_Rumpf_liest_sich_wie_im_Board_und_wird_beim_Ersetzen_ein_Block_ohne_Leerzeile()
    {
        const string text = "class S\n{\n    public Task Speichere(int x)\n        => throw new NotImplementedException(\"TODO\");\n\n    public int Zahl() => 42;\n    public void Tu() => Console.WriteLine();\n}\n";
        var baum = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text).GetRoot();
        var m = baum.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().ToList();
        Rumpf.AlsAnweisung(m[0]).Should().Be("throw new NotImplementedException(\"TODO\");");
        Rumpf.AlsAnweisung(m[1]).Should().Be("return 42;");
        Rumpf.AlsAnweisung(m[2]).Should().Be("Console.WriteLine();");
        var ort = new RumpfOrt("S.cs", m[0].Span.Start, m[0].Span.End);
        Rumpf.Lies(text, ort).Should().Be("throw new NotImplementedException(\"TODO\");", "dieselbe Form wie das Board-Feld rumpf — sonst greift die Sperre des ✎");
        Rumpf.MitRumpf(text, ort, "return Task.CompletedTask;", null)
            .Should().Contain("    public Task Speichere(int x)\n    {\n        return Task.CompletedTask;\n    }\n\n    public int Zahl()");
    }
}
