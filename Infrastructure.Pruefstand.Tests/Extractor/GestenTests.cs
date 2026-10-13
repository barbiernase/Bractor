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
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;\nglobal using System.Threading.Tasks;\n");
    }

    public void Dispose() { try { Directory.Delete(_wurzel, true); } catch (IOException) { } }

    private string Text(string datei) => File.ReadAllText(Path.Combine(Projekt, datei));

    private Solution Loesung()
    {
        _ = typeof(Abstractions.IEvent); _ = typeof(Core.ProjectionWriter);
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
                ["sagas"] = [Karte(board, "sagas", s => true)],
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
        Text("Fluss.cs").Should().Contain("public sealed record PruefeSeite(string Seite) : IAuftrag<ISeitenPruefung>;");
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
    public async Task Projektions_Handle_bekommt_eine_Faehigkeit_als_Parameter()
    {
        var board = await BoardAsync();
        var karte = Karte(board, "projektionen", p => (string?)p["name"] == "Leihliste");
        var handle = karte["handles"]!.AsArray().OfType<JsonObject>().First(h => (string?)h["event"] == "Zurueckgegeben");
        handle["fns"]!.AsArray().Add("rf:ILeihBuch.OffeneAsync");

        await GesteAsync(Geaendert("projektionen", karte));

        Text("Lesen.cs").Should().Contain("public async Task Handle(Zurueckgegeben evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISchliesseAusleihe schliesse, IZaehleOffene zaehleOffene)")
            .And.Contain("Func<Guid, Task> tu = schliesse.SchliesseAsync;", "der Rumpf bleibt");
    }

    [Fact]
    public async Task Handle_loeschen_entfernt_seine_Methode()
    {
        var karte = Karte(await BoardAsync(), "projektionen", p => (string?)p["name"] == "Leihliste");
        var handles = karte["handles"]!.AsArray();
        handles.Remove(handles.OfType<JsonObject>().First(h => (string?)h["event"] == "Zurueckgegeben"));

        var bericht = await GesteAsync(Geaendert("projektionen", karte));

        bericht.Geloescht.Should().ContainSingle(x => x.Contains("Handle(Zurueckgegeben)"));
        Text("Lesen.cs").Should().NotContain("Handle(Zurueckgegeben evt").And.Contain("Handle(Ausgeliehen evt");
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
    public async Task Funktion_bekommt_ein_weiteres_Ergebnis()
    {
        var karte = Karte(await BoardAsync(), "funktionen", f => (string?)f["name"] == "ISchadensSchaetzung");
        karte["ergebnisse"]!.AsArray().Add("SchadenGemeldet");

        await GesteAsync(Geaendert("funktionen", karte));

        Text("Gebuehren.cs").Should().Contain("Task<OneOf<SchadenGeschaetzt, NichtSchaetzbar, SchadenGemeldet>> RufeAsync(SchaetzeSchaden auftrag, IAusfuehrung x);");
    }

    [Fact]
    public async Task Prozess_Regel_entfernen_laesst_die_anderen_Wort_fuer_Wort()
    {
        var karte = Karte(await BoardAsync(), "sagas", s => true);
        var schritte = karte["schritte"]!.AsArray();
        schritte.Count.Should().BeGreaterThan(1);
        var weg = (JsonObject)schritte[^1]!;
        schritte.Remove(weg);

        var bericht = await GesteAsync(Geaendert("sagas", karte));

        bericht.Patch.Should().ContainSingle();
        var ziel = (string?)weg["sende"] is { Length: > 0 } s ? s : (string?)weg["rufe"];
        Text(Path.GetFileName(bericht.Patch[0].Datei)).Split('\n').Count(z => z.Contains($"<{ziel}>"))
            .Should().BeLessThan(bericht.Patch[0].Vorher!.Split('\n').Count(z => z.Contains($"<{ziel}>")));
    }

    [Fact]
    public async Task Client_sendet_einen_weiteren_Command()
    {
        var karte = Karte(await BoardAsync(), "clients", c => (string?)c["name"] == "ILesesaal");
        karte["sendet"]!.AsArray().Add("GibZurueck");

        await GesteAsync(Geaendert("clients", karte));

        Text("Lesen.cs").Should().Contain("ISendet<LeiheAus>, ISendet<GibZurueck>").And.Contain("void Auf(MahnungEroeffnet e);");
    }

    [Fact]
    public async Task Zusage_im_Akteur_Vertrag_bekommt_einen_Ausgang()
    {
        var karte = Karte(await BoardAsync(), "akteure", a => (string?)a["name"] == "Archiv");
        var zusage = karte["vertrag"]!.AsArray().OfType<JsonObject>().First(z => (string?)z["eingang"] == "Ausgeliehen");
        zusage["ausgaenge"]!.AsArray().Add("MeldeSchaden");

        await GesteAsync(Geaendert("akteure", karte));

        Text("Lesen.cs").Should().Contain("OneOf<MeldeSchaden> Auf(Ausgeliehen e);", "eine Zusage mit Ausgabe ist OneOf (A.2)")
            .And.Contain("    MeldeSchaden Auf(Zurueckgegeben e);", "eine unberührte Zusage bleibt wörtlich");
    }

    [Fact]
    public async Task Fluss_Knoten_entfernen_laesst_die_anderen_Anweisungen_wortgetreu()
    {
        var karte = Karte(await BoardAsync(), "fluesse", f => (string?)f["name"] == "RueckgabePruefung");
        var knoten = karte["knoten"]!.AsArray();
        var letzter = (JsonObject)knoten[^1]!;
        knoten.Remove(letzter);

        var bericht = await GesteAsync(Geaendert("fluesse", karte));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/Fluss.cs");
        Text("Fluss.cs").Should().NotContain("mahnung.BeiAbgelehnt()")
            .And.Contain("var seiten = eingang.Je(e => e.Seiten);")
            .And.Contain(".Oder(schnell.BeiZeitlimit(), z => new EroeffneMahnung(Guid.Empty, 0m));", "unveränderte Lambdas bleiben wortgetreu");
    }

    [Fact]
    public async Task Umbenennen_zieht_alle_Verweise_mit_auch_in_Lambdas()
    {
        var board = await BoardAsync();
        var ort = Karte(board, "records", r => (string?)r["name"] == "EroeffneMahnung")["_ort"]!;

        var (patch, alt) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)ort["datei"]!), (int)ort["start"]!, "OeffneMahnung", _wurzel);

        alt.Should().Be("EroeffneMahnung");
        patch.Select(p => p.Datei).Should().Contain(["Leihwesen/Gebuehren.cs", "Leihwesen/Fluss.cs", "Leihwesen/Lesen.cs"]);
        patch.Should().OnlyContain(p => !p.Nachher!.Contains("EroeffneMahnung"));
        patch.Single(p => p.Datei == "Leihwesen/Fluss.cs").Nachher.Should().Contain(".Sende<OeffneMahnung>((g, e) => new OeffneMahnung(e.LeserId, g.Betrag))");
    }

    [Fact]
    public async Task Fluss_Knoten_umbenennen_benennt_die_Variable_und_ihre_Verwendungen()
    {
        var board = await BoardAsync();
        var fluss = Karte(board, "fluesse", f => (string?)f["name"] == "RueckgabePruefung");
        var ort = fluss["knoten"]!.AsArray().OfType<JsonObject>().First(k => (string?)k["name"] == "seiten")["_ort"]!;

        var (patch, _) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)ort["datei"]!), (int)ort["start"]!, "blaetter", _wurzel);

        var neu = patch.Single().Nachher!;
        neu.Should().Contain("var blaetter = eingang.Je(e => e.Seiten);").And.Contain("blaetter.Rufe<ISeitenPruefung>").And.Contain("blaetter.Sammle(");
    }

    // ── Rumpf-Edit (Werkbank P7.4): ✎ am Rumpf-Port ersetzt genau den Bereich, den die Karte aus dem Code kennt ──

    private static RumpfOrt OrtVon(JsonNode o, int? lambda = null) => new((string)o["datei"]!, (int)o["start"]!, (int)o["ende"]!, lambda);

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
    public async Task Rumpf_Edit_eines_Fluss_Lambdas_ersetzt_nur_diesen_Lambda()
    {
        var fluss = Karte(await BoardAsync(), "fluesse", f => (string?)f["name"] == "RueckgabePruefung");
        var mahnung = fluss["knoten"]!.AsArray().OfType<JsonObject>().First(k => (string?)k["name"] == "mahnung");

        // Eingang 1 = das ∨ (Oder) — der zweite Lambda der Anweisung.
        RumpfGeste(OrtVon(mahnung["_ort"]!, 1), "z => new EroeffneMahnung(Guid.Empty, 1m)", "z => new EroeffneMahnung(Guid.Empty, 0m)");

        Text("Fluss.cs").Should().Contain(".Oder(schnell.BeiZeitlimit(), z => new EroeffneMahnung(Guid.Empty, 1m));")
            .And.Contain(".Sende<EroeffneMahnung>((g, e) => new EroeffneMahnung(e.LeserId, g.Betrag))", "der Haupt-Lambda bleibt wortgetreu");
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
    public async Task Store_Funktion_und_Funktion_kennen_den_Ort_ihres_Rumpfs_in_der_Implementierung()
    {
        var board = await BoardAsync();
        var fn = (board["stores"] as JsonArray)!.OfType<JsonObject>().SelectMany(st => st["writeFns"]!.AsArray().OfType<JsonObject>())
            .First(f => (string?)f["name"] == "SchreibeAsync");
        var ort = OrtVon(fn["_rumpf"]!);
        ort.Datei.Should().Be("Leihwesen/Lesen.cs");
        Rumpf.Lies(Text("Lesen.cs"), ort).Should().Be((string?)fn["rumpf"]);
    }

    // ══ Dienste (Plan E11): ⊕ am Konstruktor der Implementierung, Methoden und Implementierungen als Geste ══

    private static JsonObject Param(string typ, string name) => new() { ["typ"] = typ, ["name"] = name };

    [Fact]
    public async Task Neuer_Dienst_per_Plus_an_die_Funktion_legt_Interface_und_Implementierung_an_und_erweitert_den_Konstruktor()
    {
        var board = await BoardAsync();
        var funktion = Karte(board, "funktionen", f => (string?)f["name"] == "ISchadensSchaetzung");
        funktion["konstruktor"]!.AsArray().Add(Param("ILeuchte", "licht"));
        var dienst = new JsonObject
        {
            ["name"] = "ILeuchte", ["namespace"] = "Leihwesen.Gebuehren",
            ["methoden"] = new JsonArray(new JsonObject { ["name"] = "Hell", ["rueckgabe"] = "bool", ["parameter"] = new JsonArray(Param("int", "stufe")) }),
            ["implementierungen"] = new JsonArray(new JsonObject { ["name"] = "Leuchte" }),
        };

        var bericht = await GesteAsync(new KartenDelta { Karten = new() { ["funktionen"] = [funktion], ["dienste"] = [dienst] } });

        bericht.Patch.Select(p => p.Datei).Should().BeEquivalentTo("Leihwesen/Gebuehren.cs", "Leihwesen/ILeuchte.cs", "Leihwesen/Leuchte.cs");
        Text("Gebuehren.cs").Should().Contain("public sealed class Schadensschaetzer(IWertTabelle tabelle, ILeuchte licht) : ISchadensSchaetzung");
        Text("ILeuchte.cs").Should().Contain("public interface ILeuchte : IDienst").And.Contain("    bool Hell(int stufe);");
        Text("Leuchte.cs").Should().Contain("public sealed class Leuchte : ILeuchte").And.Contain("public bool Hell(int stufe)")
            .And.Contain("throw new NotImplementedException(\"TODO: Leuchte.Hell\");");
        var neu = await BoardAsync();
        Karte(neu, "funktionen", f => (string?)f["name"] == "ISchadensSchaetzung")["konstruktor"]!.AsArray().Select(p => (string?)p!["typ"])
            .Should().Equal("IWertTabelle", "ILeuchte");
        neu["sicht"]!["kanten"]!.AsArray().Select(k => k!.ToJsonString()).Should().Contain("[\"fk:funktion:ISchadensSchaetzung\",\"dn:dienst:ILeuchte\"]");
    }

    [Fact]
    public async Task Dienst_loesen_entfernt_nur_den_Konstruktor_Parameter()
    {
        var funktion = Karte(await BoardAsync(), "funktionen", f => (string?)f["name"] == "ISchadensSchaetzung");
        funktion["konstruktor"] = new JsonArray();

        await GesteAsync(Geaendert("funktionen", funktion));

        Text("Gebuehren.cs").Should().Contain("public sealed class Schadensschaetzer : ISchadensSchaetzung")
            .And.Contain("if (!await tabelle.IstSchaetzbarAsync(auftrag.LeserId))", "der Rumpf bleibt — der Compiler meldet ihn an der Karte");
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
    public async Task C_Sharp_Implementierung_einer_Funktion_anlegen_mit_Dienst_im_Konstruktor()
    {
        var funktion = Karte(await BoardAsync(), "funktionen", f => (string?)f["name"] == "ISeitenPruefung");
        funktion["entwurf"] = "";
        funktion["konstruktor"] = new JsonArray(Param("IWertTabelle", "tabelle"));

        var bericht = await GesteAsync(Geaendert("funktionen", funktion));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/SeitenPruefung.cs");
        Text("SeitenPruefung.cs").Should().Contain("using Leihwesen.Gebuehren;")
            .And.Contain("public sealed class SeitenPruefung(IWertTabelle tabelle) : ISeitenPruefung")
            .And.Contain("public async Task<OneOf<SeiteGeprueft>> RufeAsync(PruefeSeite auftrag, IAusfuehrung x)");
        var neu = Karte(await BoardAsync(), "funktionen", f => (string?)f["name"] == "ISeitenPruefung");
        ((string?)neu["implementierung"]).Should().Be("SeitenPruefung");
    }
}
