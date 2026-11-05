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

        Text("Gebuehren.cs").Should().Contain("public IEnumerable<OneOf<SchadenGemeldet, SchaetzeSchaden, SchadenExistiert, MahnungEroeffnet>> Decide(MeldeSchaden c)")
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
        Text("Gebuehren.cs").Should().Contain("public record SchaetzeSchaden(Id<Schadensfall> Fall, Guid LeserId) : ICommand;");

        var bericht = await GesteAsync(new KartenDelta { Entfernt = new() { ["records"] = ["Leihwesen.Gebuehren|SchaetzeSchaden"] } });

        bericht.Geloescht.Should().ContainSingle();
        Text("Gebuehren.cs").Should().NotContain("public record SchaetzeSchaden").And.NotContain("der Schaden ist zu schätzen (der Kopf der Schadensbewertung)",
            "der Kommentar über der Deklaration geht mit");
        Text("Gebuehren.cs").Should().Contain("public record SchadenErfasst(Guid LeserId, decimal Neuwert, int AlterJahre) : ICommand;");
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

        Text("Lesen.cs").Should().Contain("public sealed record Gutachterin : IAkteur, IDarf<EroeffneMahnung>, IDarf<MeldeSchaden>, IHoert<MahnungEroeffnet>;",
            "erst was er hineingibt, dann was er hört");
    }

    [Fact]
    public async Task Akteur_hoert_einen_weiteren_Hinweis_die_Zeilen_der_Basisliste_bleiben()
    {
        var karte = Karte(await BoardAsync(), "akteure", a => (string?)a["name"] == "Theke");
        karte["hoert"]!.AsArray().Add("BereitsAusgeliehen");

        await GesteAsync(Geaendert("akteure", karte));

        Text("Lesen.cs").Should().Contain("public sealed record Theke : IAkteur, IDarf<LeiheAus>, IDarf<GibZurueck>, IDarf<OffeneAusleihen>, IDarf<RueckgabeGescannt>,\n"
            + "    IHoert<NichtAusgeliehen>, IHoert<NichtSchaetzbar>, IHoert<LaufGescheitert>, IHoert<BereitsAusgeliehen>;", "der Umbruch der Basisliste bleibt");
    }

    [Fact]
    public async Task Client_verkoerpert_einen_weiteren_Akteur()
    {
        var karte = Karte(await BoardAsync(), "clients", c => (string?)c["name"] == "Lesesaal");
        karte["verkoerpert"]!.AsArray().Add("Gutachterin");

        await GesteAsync(Geaendert("clients", karte));

        Text("Lesen.cs").Should().Contain("public sealed record Lesesaal : IClient, IVerkoerpert<Theke>, IVerkoerpert<Archiv>, IVerkoerpert<Gutachterin>;",
            "ein Client nennt nur, wen er verkörpert — gehört wird am Akteur");
    }

    [Fact]
    public async Task Funktion_bekommt_eine_weitere_Ausgabe()
    {
        var karte = Karte(await BoardAsync(), "bausteinFunktionen", f => (string?)f["name"] == "SchadenBeiRueckgabe");
        var ein = karte["eingaenge"]!.AsArray().OfType<JsonObject>().Single(e => (string?)e["eingang"] == "PruefeRueckgabe");
        ein["ausgaben"]!.AsArray().Add("EroeffneMahnung");

        await GesteAsync(Geaendert("bausteinFunktionen", karte));

        Text("Gebuehren.cs").Should().Contain("IEnumerable<OneOf<MeldeSchaden, EroeffneMahnung>> Auf(PruefeRueckgabe p, Herkunft h)",
            "die Ausgaben stehen in der Signatur (CQRS050); der Strom bleibt ein Strom");
    }

    [Fact]
    public async Task Die_Form_der_Ausgabe_steht_in_der_Signatur_Strom_und_asynchron()
    {
        // Lesen: SchadenBeiRueckgabe gibt einen Strom aus (synchron), Schadensschaetzer ein Task<…> — beides aus der Signatur.
        var board = await BoardAsync();
        var strom = Karte(board, "bausteinFunktionen", f => (string?)f["name"] == "SchadenBeiRueckgabe")["eingaenge"]!.AsArray().OfType<JsonObject>().Single();
        ((bool?)strom["strom"]).Should().BeTrue();
        ((bool?)strom["asynchron"]).Should().BeFalse();
        var task = Karte(board, "bausteinFunktionen", f => (string?)f["name"] == "Schadensschaetzer")["eingaenge"]!.AsArray().OfType<JsonObject>().Single();
        ((bool?)task["asynchron"]).Should().BeTrue();

        // Schreiben: asynchron macht aus dem Strom einen IAsyncEnumerable, ohne Strom wird aus dem Task die bloße Ausgabe.
        var karte = Karte(board, "bausteinFunktionen", f => (string?)f["name"] == "SchadenBeiRueckgabe");
        karte["eingaenge"]!.AsArray().OfType<JsonObject>().Single()["asynchron"] = true;
        await GesteAsync(Geaendert("bausteinFunktionen", karte));
        Text("Gebuehren.cs").Should().Contain("IAsyncEnumerable<MeldeSchaden> Auf(PruefeRueckgabe p, Herkunft h)");

        var schaetzer = Karte(await BoardAsync(), "bausteinFunktionen", f => (string?)f["name"] == "Schadensschaetzer");
        schaetzer["eingaenge"]!.AsArray().OfType<JsonObject>().Single()["asynchron"] = false;
        await GesteAsync(Geaendert("bausteinFunktionen", schaetzer));
        Text("Gebuehren.cs").Should().Contain(" SchadenGeschaetzt Auf(SchadenErfasst e)").And.NotContain("Task<SchadenGeschaetzt> Auf(SchadenErfasst e)",
            "nur die Rückgabe folgt der Form — der Rumpf gehört dem Code (sein await baut danach nicht mehr: unfertig, W1)");
    }

    [Fact]
    public async Task Neue_Funktion_wird_eine_Klasse_mit_einem_Auf_je_Eingang()
    {
        var neu = new JsonObject
        {
            ["name"] = "Mahnlauf", ["namespace"] = "Leihwesen.Gebuehren", ["klasse"] = true,
            ["eingaenge"] = new JsonArray(new JsonObject { ["eingang"] = "SchaetzeSchaden", ["herkunft"] = true, ["ausgaben"] = Liste("MeldeSchaden") }),
            ["konstruktor"] = new JsonArray(new JsonObject { ["typ"] = "WertTabellenKonfig", ["name"] = "konfig" }),
        };

        await GesteAsync(Geaendert("bausteinFunktionen", neu));

        Text("Mahnlauf.cs").Should().Contain("public sealed class Mahnlauf(WertTabellenKonfig konfig) : IFunktion")
            .And.Contain("public MeldeSchaden Auf(SchaetzeSchaden e, Herkunft h)", "kein T? als Ausgabe (Entscheidung 4)").And.Contain("NotImplementedException");
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

    // ══ Graphen (docs/04 §4.4): ein Knoten ist ein Parameter des Records — jede Kante steht als [Von(nameof(…))] am nehmenden Knoten, jeder Kopf als [Kopf] ══

    private static JsonObject Knoten(string typ, string name, bool kopf = false, params string[] von) =>
        new() { ["typ"] = typ, ["name"] = name, ["kopf"] = kopf, ["von"] = Liste(von) };

    [Fact]
    public async Task Knoten_hinzufuegen_ist_ein_Parameter_mehr_am_Graph_Record()
    {
        var vorher = Text("Fluss.cs");
        var karte = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Mahnbemessung");
        karte["knoten"]!.AsArray().Add(Knoten("SchadenBeiRueckgabe", "Rueckgabe", kopf: true));

        var bericht = await GesteAsync(Geaendert("graphen", karte));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/Fluss.cs");
        Text("Fluss.cs").Should().Contain("public sealed record Mahnbemessung([Kopf] Offenzaehlung Zaehlung, [Kopf] SchadenBeiRueckgabe Rueckgabe) : IGraph;");
        Text("Fluss.cs").Replace(", [Kopf] SchadenBeiRueckgabe Rueckgabe", "").Should().Be(vorher, "alles andere bleibt Wort für Wort");
        // Neu gelesen: der Knoten ist da, mit [Kopf]; die Köpfe folgen daraus.
        var neu = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Mahnbemessung");
        neu["knoten"]!.AsArray().Select(k => $"{(string?)k!["name"]}:{(bool?)k["kopf"]}").Should().Equal("Zaehlung:True", "Rueckgabe:True");
        neu["koepfe"]!.AsArray().Select(x => (string?)x).Should().BeEquivalentTo(["BemesseMahnung", "PruefeRueckgabe"]);
    }

    [Fact]
    public async Task Knoten_entfernen_nimmt_nur_seinen_Parameter()
    {
        var karte = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Schadensbewertung");
        var knoten = karte["knoten"]!.AsArray();
        knoten.Remove(knoten.OfType<JsonObject>().Single(k => (string?)k["name"] == "Gutachten"));

        await GesteAsync(Geaendert("graphen", karte));

        Text("Gebuehren.cs").Should().Contain("""
            public sealed record Schadensbewertung(
                [Kopf] Schadensaufnahme Aufnahme,
                [Von(nameof(Aufnahme))] Schadensschaetzer Schaetzung,
                MahnungNachSchaetzung Mahnung) : IGraph;
            """, "die Kante vom entfernten Knoten geht mit ihm (die Mahnung ist danach lose — Unfertiges ist Code, der nicht baut)")
            .And.Contain("public sealed class Gutachtenanfrage(GutachtenKonfig konfig) : IFunktion", "die Funktion bleibt — nur der Knoten geht");
    }

    [Fact]
    public async Task Trennen_und_verbinden_entfernt_bzw_setzt_ein_Von_am_nehmenden_Knoten()
    {
        var vorher = Text("Gebuehren.cs");
        async Task MahnungVon(params string[] von)
        {
            var karte = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Schadensbewertung");
            karte["knoten"]!.AsArray().OfType<JsonObject>().Single(k => (string?)k["name"] == "Mahnung")["von"] = Liste(von);
            (await GesteAsync(Geaendert("graphen", karte))).Patch.Select(p => p.Datei).Should().Equal("Leihwesen/Gebuehren.cs");
        }

        await MahnungVon();
        Text("Gebuehren.cs").Should().Be(vorher.Replace("[Von(nameof(Gutachten))] MahnungNachSchaetzung Mahnung",
            "MahnungNachSchaetzung Mahnung"), "nur der Parameter des nehmenden Knotens ändert sich");
        var getrennt = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Schadensbewertung");
        getrennt["kanten"]!.AsArray().Select(k => $"{(string?)k!["von"]}->{(string?)k["nach"]}").Should().NotContain("Gutachten->Mahnung");

        await MahnungVon("Gutachten");
        Text("Gebuehren.cs").Should().Be(vorher, "verbinden setzt das Von wieder, wie es stand");
    }

    [Fact]
    public async Task Verlierbar_schreibt_ein_eigenes_Von_mit_Verlierbar_und_nimmt_es_wieder_weg()
    {
        var vorher = Text("Gebuehren.cs");
        async Task<JsonObject> MahnungVerlierbar(params string[] verlierbar)
        {
            var karte = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Schadensbewertung");
            karte["knoten"]!.AsArray().OfType<JsonObject>().Single(k => (string?)k["name"] == "Mahnung")["verlierbar"] = Liste(verlierbar);
            (await GesteAsync(Geaendert("graphen", karte))).Patch.Select(p => p.Datei).Should().Equal("Leihwesen/Gebuehren.cs");
            return Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Schadensbewertung");
        }

        var gelesen = await MahnungVerlierbar("Gutachten");
        Text("Gebuehren.cs").Should().Be(vorher.Replace("[Von(nameof(Gutachten))] MahnungNachSchaetzung Mahnung",
            "[Von(nameof(Gutachten), Verlierbar = true)] MahnungNachSchaetzung Mahnung"), "die Zustellung steht am Von des nehmenden Knotens");
        gelesen["knoten"]!.AsArray().OfType<JsonObject>().Single(k => (string?)k["name"] == "Mahnung")["verlierbar"]!.AsArray()
            .Select(v => (string?)v).Should().Equal(["Gutachten"], "gelesen, wie geschrieben");

        await MahnungVerlierbar();
        Text("Gebuehren.cs").Should().Be(vorher, "ohne Verlierbar ist die Kante wieder gesichert");
    }

    [Fact]
    public async Task Kopf_setzt_bzw_entfernt_das_Attribut_am_Knoten()
    {
        var vorher = Text("Gebuehren.cs");
        async Task Kopf(bool kopf)
        {
            var karte = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Rueckgabepruefung");
            karte["knoten"]!.AsArray()[0]!["kopf"] = kopf;
            await GesteAsync(Geaendert("graphen", karte));
        }

        await Kopf(false);
        Text("Gebuehren.cs").Should().Contain("public sealed record Rueckgabepruefung(SchadenBeiRueckgabe Pruefung) : IGraph;");
        Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Rueckgabepruefung")["koepfe"]!.AsArray()
            .Should().BeEmpty("ohne [Kopf] nimmt der Graph nichts von außen");

        await Kopf(true);
        Text("Gebuehren.cs").Should().Be(vorher);
    }

    [Fact]
    public async Task Neuer_Graph_kommt_in_die_Graph_Datei_seines_Namespace_und_liest_sich_zurueck()
    {
        var neu = new JsonObject
        {
            ["name"] = "Rueckgabelauf", ["namespace"] = "Leihwesen.Gebuehren",
            ["knoten"] = new JsonArray(Knoten("Rueckgabepruefung", "Pruefung", kopf: true)),
        };

        var bericht = await GesteAsync(Geaendert("graphen", neu));

        bericht.Patch.Select(p => p.Datei).Should().Equal(["Leihwesen/Gebuehren.cs"], "dort stehen die Graphen des Namespace");
        Text("Gebuehren.cs").Should().Contain("public sealed record Rueckgabelauf([Kopf] Rueckgabepruefung Pruefung) : IGraph;");
        var karte = Karte(await BoardAsync(), "graphen", g => (string?)g["name"] == "Rueckgabelauf");
        karte["knoten"]!.AsArray().Select(k => $"{(string?)k!["name"]}:{(string?)k["typ"]}").Should().Equal("Pruefung:Rueckgabepruefung");
        karte["koepfe"]!.AsArray().Select(x => (string?)x).Should().Equal(["PruefeRueckgabe"], "ein Graph im Graphen: die Signatur der Schachtel");
        ((string?)karte["datei"]).Should().Be("Leihwesen/Gebuehren.cs");
    }

    [Fact]
    public async Task Neuer_Graph_ohne_Graph_Datei_im_Namespace_wird_Graphen_cs_mit_den_usings_seiner_Knoten()
    {
        var neu = new JsonObject
        {
            ["name"] = "Ausleihlauf", ["namespace"] = "Leihwesen.Ausleihe",
            ["knoten"] = new JsonArray(Knoten("Offenzaehlung", "Zaehlung", kopf: true), Knoten("Glaettung<Kurzfenster>", "Glaettung", kopf: true)),
        };

        var bericht = await GesteAsync(Geaendert("graphen", neu));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/Graphen.cs");
        Text("Graphen.cs").Should().Contain("using Leihwesen.Pruefung;").And.Contain("using Wetterdienst.Vorhersage;")
            .And.Contain("namespace Leihwesen.Ausleihe;")
            .And.Contain("public sealed record Ausleihlauf([Kopf] Offenzaehlung Zaehlung, [Kopf] Glaettung<Kurzfenster> Glaettung) : IGraph;",
                "der Typ einer generischen Funktion steht mit Typargument");
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

    // ══ Technik (docs/06 §6.5, ohne Marker): Schnittstelle, Methoden und Umsetzungen als Geste ══

    private static JsonObject Param(string typ, string name) => new() { ["typ"] = typ, ["name"] = name };

    private static JsonObject Leuchte() => new()
    {
        ["name"] = "ILeuchte", ["namespace"] = "Leihwesen.Gebuehren", ["klasse"] = false,
        ["methoden"] = new JsonArray(new JsonObject { ["name"] = "Hell", ["rueckgabe"] = "bool", ["parameter"] = new JsonArray(Param("int", "stufe")) }),
        ["umsetzungen"] = new JsonArray(new JsonObject { ["name"] = "Leuchte" }),
    };

    [Fact]
    public async Task Neue_Technik_per_Plus_legt_Schnittstelle_ohne_Marker_und_Umsetzung_an()
    {
        var bericht = await GesteAsync(new KartenDelta { Karten = new() { ["bausteinFunktionen"] = [Leuchte()] } });

        bericht.Patch.Select(p => p.Datei).Should().BeEquivalentTo("Leihwesen/Funktionen.cs", "Leihwesen/Leuchte.cs");
        Text("Funktionen.cs").Should().Contain("public interface ILeuchte\n{").And.Contain("    bool Hell(int stufe);");
        Text("Leuchte.cs").Should().Contain("public sealed class Leuchte : ILeuchte").And.Contain("public bool Hell(int stufe)")
            .And.Contain("throw new NotImplementedException(\"TODO: Leuchte.Hell\");");
    }

    [Fact]
    public async Task Methode_an_der_Technik_kommt_an_die_Schnittstelle_und_als_Stub_an_jede_Umsetzung()
    {
        var f = Karte(await BoardAsync(), "bausteinFunktionen", d => (string?)d["name"] == "IWertTabelle");
        f["methoden"]!.AsArray().Add(new JsonObject { ["name"] = "Runde", ["rueckgabe"] = "decimal", ["parameter"] = new JsonArray(Param("decimal", "betrag")) });

        await GesteAsync(Geaendert("bausteinFunktionen", f));

        Text("Gebuehren.cs").Should().Contain("    Task<bool> IstSchaetzbarAsync(Guid leserId);\n    decimal Runde(decimal betrag);\n}")
            .And.Contain("    public decimal Runde(decimal betrag)\n    {\n        throw new NotImplementedException(\"TODO: LineareWertTabelle.Runde\");");
    }

    [Fact]
    public async Task Zweite_Umsetzung_einer_Technik_ist_eine_neue_Klasse_mit_Stubs()
    {
        var f = Karte(await BoardAsync(), "bausteinFunktionen", d => (string?)d["name"] == "IWertTabelle");
        f["umsetzungen"]!.AsArray().Add(new JsonObject { ["name"] = "FesteWertTabelle" });

        var bericht = await GesteAsync(Geaendert("bausteinFunktionen", f));

        bericht.Patch.Select(p => p.Datei).Should().Equal("Leihwesen/FesteWertTabelle.cs");
        Text("FesteWertTabelle.cs").Should().Contain("public sealed class FesteWertTabelle : IWertTabelle")
            .And.Contain("public decimal Restwert(decimal neuwert, int alterJahre)").And.Contain("public Task<bool> IstSchaetzbarAsync(Guid leserId)");
    }

    [Fact]
    public async Task Einen_Konfig_Record_in_den_Konstruktor_einer_Umsetzung_nehmen()
    {
        var f = Karte(await BoardAsync(), "bausteinFunktionen", d => (string?)d["name"] == "IWertTabelle");
        f["umsetzungen"]![0]!["konstruktor"]!.AsArray().Add(Param("GutachtenKonfig", "gutachten"));

        await GesteAsync(Geaendert("bausteinFunktionen", f));

        Text("Gebuehren.cs").Should().Contain("public sealed class LineareWertTabelle(WertTabellenKonfig konfig, GutachtenKonfig gutachten) : IWertTabelle");
    }

    [Fact]
    public async Task Technik_in_den_Konstruktor_einer_Funktion_nehmen()
    {
        var f = Karte(await BoardAsync(), "bausteinFunktionen", d => (string?)d["name"] == "Gutachtenanfrage");
        f["konstruktor"]!.AsArray().Add(Param("IWertTabelle", "tabelle"));

        await GesteAsync(Geaendert("bausteinFunktionen", f));

        Text("Gebuehren.cs").Should().Contain("public sealed class Gutachtenanfrage(GutachtenKonfig konfig, IWertTabelle tabelle) : IFunktion")
            .And.Contain("await Task.Delay(konfig.Bedenkzeit);", "der Rumpf gehört dem Code");
    }

    [Fact]
    public async Task Umbenennen_eines_Typs_in_seiner_eigenen_Datei_zieht_die_Datei_mit()
    {
        await GesteAsync(new KartenDelta { Karten = new() { ["bausteinFunktionen"] = [Leuchte()] } });
        // Technik ist, was der Konstruktor einer Funktion nimmt: erst im Konstruktor einer Funktion wird ILeuchte eine Karte.
        var schaetzung = Karte(await BoardAsync(), "bausteinFunktionen", d => (string?)d["name"] == "Schadensschaetzer");
        schaetzung["konstruktor"]!.AsArray().Add(Param("ILeuchte", "leuchte"));
        await GesteAsync(Geaendert("bausteinFunktionen", schaetzung));
        var karte = Karte(await BoardAsync(), "bausteinFunktionen", d => (string?)d["name"] == "ILeuchte");
        var methode = karte["methoden"]![0]!["_ort"]!;
        var impl = karte["umsetzungen"]![0]!["_ort"]!;
        ((string?)impl["datei"]).Should().Be("Leihwesen/Leuchte.cs", "die Implementierung trägt ihren Ort — ihr Name ist umbenennbar");

        var (patch, _) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)impl["datei"]!), (int)impl["start"]!, "Lampe", _wurzel);

        patch.Should().Contain(p => p.Datei == "Leihwesen/Leuchte.cs" && p.Nachher == null, "die Datei <alt>.cs fällt weg …")
            .And.Contain(p => p.Datei == "Leihwesen/Lampe.cs" && p.Vorher == null && p.Nachher!.Contains("public sealed class Lampe : ILeuchte"), "… und heißt wie der Typ");

        (patch, _) = await Umbenennen.Async(Loesung(), Path.Combine(_wurzel, (string)methode["datei"]!), (int)methode["start"]!, "Strahlt", _wurzel);
        patch.Select(p => p.Datei).Should().BeEquivalentTo(["Leihwesen/Funktionen.cs", "Leihwesen/Leuchte.cs"], "eine Methode benennt Schnittstelle und Umsetzung um, keine Datei zieht");
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
