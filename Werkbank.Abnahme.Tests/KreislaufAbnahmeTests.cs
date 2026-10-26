using System.Text.Json.Nodes;
using FluentAssertions;

namespace Werkbank.Abnahme.Tests;

/// <summary>
/// Abnahme der Werkbank ohne Browser (docs/konzept-werkbank.md §5–§8, docs/13 WB-2): derselbe Weg, den der Editor über HTTP geht, gegen eine echte
/// Werkbank, einen echten Host in Umgebung <c>sim</c> und eine eigene Sim-DB (<see cref="Werkbanklauf"/>). Ein einziger Lauf, weil jeder Schritt auf
/// dem vorigen aufbaut:
/// <list type="number">
/// <item>⟳: alle Schritte grün, Board-Stand = Lauf-Stand = Stand, den der Host an die Spur stempelt;</item>
/// <item>als Akteur über den JSON-Eingang ein Command, die Projektion antwortet auf die Query; Vorgang und Spur über die Spur-API;</item>
/// <item>„▶ Worker“: die Verkleinerung rechnet ein Python-Worker (Laufort Extern), eine Datei in der Quelle läuft durch bis zum Fakt; der erste Draht
///   des Flusses kommt aus dem warmen Board;</item>
/// <item>Formular und Tokens aus Board bzw. Umgebung; Umgebungs-Werte und 🤖-Übernahmen sind Gesten mit ↶ (docs/13 W11-1, WB-1);</item>
/// <item>Geste (Feld mit Standardwert an einem Event) → ⟳ grün (verträglich, der Host siegelt das neue Proto) → ↶ (Code byte-gleich) →
///   ⟳ „Daten prüfen“ meldet das entfernte Feld → ⟳ mit Zurücksetzen startet wieder.</item>
/// </list>
/// </summary>
public class KreislaufAbnahmeTests
{
    private static readonly string[] Schritte = ["Bauen", "Python-Basen", "Neu lesen", "Daten prüfen", "Infrastruktur", "Starten"];

    [Fact(Skip = "Editor zurückgestellt (plan-pubsub-neustrukturierung E4)")]
    public async Task Kreislauf_Durchspielen_Worker_Geste_und_Daten_Pruefung()
    {
        await using var w = await Werkbanklauf.StarteAsync();
        string Log() => "\n── Werkbank ──\n" + w.Log();

        // Die gewählte Umgebung ist die Test-Kopie (Kopfzeile „Umgebung ▾“); die Werkbank kennt den Branch der Kopie.
        var (_, zustand) = await w.SendeAsync($"/api/werkbank/aktiv/{Werkbanklauf.Umgebung}");
        ((string?)zustand["aktiv"]).Should().Be(Werkbanklauf.Umgebung);
        ((string?)zustand["branch"]).Should().Be(w.Branch);
        ((string?)zustand["datenbank"]).Should().Be(w.Datenbank, "die Sim-DB folgt dem Branch der Kopie — nie die des Entwicklers");

        // Die LLM-Kontexte entstehen beim ersten Start im Hintergrund (GraphExtractor im selben Baum) — nicht gleichzeitig mit ⟳ bauen.
        await Werkbanklauf.Warte(async () => await w.HoleAsync("/api/llm/status") is var s && s["index"] is JsonObject && (bool?)s["aktualisierungLaeuft"] == false,
            TimeSpan.FromMinutes(6), "Die LLM-Kontexte wurden nicht fertig." + Log());
        (await w.SendeAsync("/api/werkbank/simdb/zuruecksetzen")).Status.Should().Be(200);

        // ── 1. ⟳ ──
        var lauf = await KreislaufAsync(w, zuruecksetzen: false);
        SchritteGruen(lauf, Log);
        var stand = (string)lauf["stand"]!;
        zustand = await w.HoleAsync("/api/werkbank/zustand");
        ((string?)zustand["stand"]).Should().Be(stand, "Board-Stand = Stand des Laufs");
        ((string?)zustand["laufStand"]).Should().Be(stand, "der Host läuft mit dem gebauten Stand");
        ((bool?)zustand["host"]!["laeuft"]).Should().BeTrue();
        using (var einblick = new HttpClient { BaseAddress = new Uri($"http://localhost:{w.EinblickPort}") })
        {
            var hostStand = JsonNode.Parse(await einblick.GetStringAsync("/einblick/stand"))!;
            ((string?)hostStand["stand"]).Should().Be(stand, "der Host stempelt denselben Stand an jedes Event (Spur)");
            ((string?)hostStand["umgebung"]).Should().Be(Werkbanklauf.Umgebung);
        }
        var nachLauf = w.Abbild();

        // ── 2. Als Akteur handeln (JSON-Eingang) → Vorgang und Spur ──
        var datensatz = Guid.NewGuid();
        var (status, antwort) = await w.SendeAsync("/api/werkbank/eingang/command",
            new { typ = "ErstelleDatensatz", daten = new { AggregateId = datensatz, Name = "Abnahme" } }, token: "sim-kioperator");
        status.Should().Be(200, antwort.ToJsonString());
        ((bool?)antwort["ok"]).Should().BeTrue(antwort.ToJsonString());
        ((string?)antwort["akteur"]).Should().Be("KIOperator");
        ((string?)antwort["events"]![0]!["typ"]).Should().Be("DatensatzErstellt");
        var korrelation = (string)antwort["korrelation"]!;

        (await w.SendeAsync("/api/werkbank/eingang/command",
            new { typ = "ErstelleDatensatz", daten = new { AggregateId = Guid.NewGuid(), Name = "x" } }, token: "sim-inspekteur"))
            .Status.Should().Be(403, "das Akteur-Tor gilt auch für die Werkbank");

        JsonObject query = new();
        await Werkbanklauf.Warte(async () =>
        {
            query = (await w.SendeAsync("/api/werkbank/eingang/query", new { typ = "HoleDatensatz", daten = new { DatensatzId = datensatz } }, "sim-kioperator")).Antwort;
            return (bool?)query["ok"] == true && (string?)query["antwort"]?["typ"] == "DatensatzAntwort";
        }, TimeSpan.FromSeconds(30), "Die Projektion hat den Datensatz nicht geliefert: " + query.ToJsonString());
        ((string?)query["antwort"]!["daten"]!["Name"]).Should().Be("Abnahme");

        await Werkbanklauf.Warte(async () => ((await w.HoleAsync("/api/werkbank/spur/vorgaenge"))["vorgaenge"] as JsonArray ?? [])
            .Any(v => (string?)v?["korrelation"] == korrelation), TimeSpan.FromSeconds(30), "Der Vorgang erscheint nicht in der Spur." + Log());
        var vorgang = await w.HoleAsync($"/api/werkbank/spur/vorgang/{korrelation}");
        var schritt = (vorgang["schritte"] as JsonArray)!.OfType<JsonObject>().First(s => (string?)s["name"] == "DatensatzErstellt");
        ((string?)schritt["wirker"]).Should().Be("decide:Datensatz/ErstelleDatensatz", "der Wirker ist die Karten-Id (W9)");
        ((string?)schritt["ueber"]).Should().Be("akteur:KIOperator");
        ((string?)schritt["stand"]).Should().Be(stand);
        ((bool?)schritt["alt"]).Should().BeFalse();
        (schritt["karten"] as JsonArray).Should().NotBeEmpty("der Schritt liegt auf Karten des Boards");

        // ── 3. ▶ Worker: die Verkleinerung rechnet Python (Laufort Extern), eine Datei in der Quelle läuft bis zum Fakt ──
        var (_, worker) = await w.SendeAsync("/api/werkbank/worker/IBildVerkleinerung/start");
        ((bool?)worker["ok"]).Should().BeTrue(worker.ToJsonString());
        async Task<string> WorkerLog() => string.Join("\n", (JsonNode.Parse(await w.Http.GetStringAsync("/api/werkbank/worker/IBildVerkleinerung/log")) as JsonArray ?? [])
            .Select(z => (string?)z));
        await Werkbanklauf.Warte(async () => (await WorkerLog()).Contains("angeboten", StringComparison.Ordinal), TimeSpan.FromSeconds(60),
            "Der Python-Worker hat sich nicht angemeldet.");

        var t = new DateTimeOffset(2026, 10, 15, 8, 0, 0, Random.Shared.Next(0, 999), TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 100000));
        var dateiname = $"{t:yyyy_MM_dd_HH_mm_ss_fff}_DC0.tiff";
        var (_, abgelegt) = await w.SendeAsync($"/api/werkbank/quelle/DateiErkannt/datei?name={dateiname}",
            new ByteArrayContent(Werkbanklauf.Tiff(96, 64)));
        ((bool?)abgelegt["ok"]).Should().BeTrue(abgelegt.ToJsonString());

        JsonObject? bild = null;
        await Werkbanklauf.Warte(async () =>
        {
            bild = ((await w.HoleAsync("/api/werkbank/spur/vorgaenge?quelle=DateiErkannt"))["vorgaenge"] as JsonArray ?? []).OfType<JsonObject>()
                .FirstOrDefault(v => (string?)v["zustand"] is "fertig" or "gescheitert");
            return bild != null;
        }, TimeSpan.FromSeconds(120), "Der Bildeingang lief nicht zu Ende.\n── Worker ──\n" + await WorkerLog() + Log());
        ((string?)bild!["zustand"]).Should().Be("fertig", bild.ToJsonString());
        var bildweg = await w.HoleAsync($"/api/werkbank/spur/vorgang/{(string)bild["korrelation"]!}");
        (bildweg["schritte"] as JsonArray)!.Select(s => (string?)s?["name"]).Should().Contain("BildVerfuegbar");
        (await WorkerLog()).Should().Contain("[BildVerkleinerung]").And.Contain(dateiname, "genau diesen Auftrag hat der Python-Worker gerechnet");

        // Der erste Draht des Flusses (Quelle → erster Knoten) ist ein gegangener Weg — der Quell-Knoten kommt aus dem warmen Board, nicht aus einer Datei.
        var board = JsonNode.Parse(await w.Http.GetStringAsync("/api/editor/model"))!.AsObject();
        var quellKnoten = (board["fluesse"] as JsonArray)!.OfType<JsonObject>().Single(f => (string?)f["name"] == "Bildeingang")["knoten"]!.AsArray()
            .OfType<JsonObject>().Single(k => (string?)k["art"] == "quelle")["_id"]!.GetValue<string>();
        (bildweg["schritte"] as JsonArray)!.SelectMany(s => s?["paare"] as JsonArray ?? []).Select(p => (string?)p?[0])
            .Should().Contain(quellKnoten, "der Spur-Spiegel liest den Quell-Knoten aus dem warmen Board");

        // ── 4. Durchspielen liest das warme Board; Tokens wie im Host ──
        var formular = await w.HoleAsync("/api/werkbank/formular?typ=ErstelleDatensatz");
        (formular["felder"] as JsonObject).Should().NotBeNull(formular.ToJsonString()).And.Subject.As<JsonObject>().ContainsKey("Name").Should().BeTrue();
        var tokens = (await w.HoleAsync("/api/werkbank/identitaeten"))["tokens"]!.AsArray().OfType<JsonObject>()
            .ToDictionary(t => (string)t["token"]!, t => t["akteure"]!.AsArray().Select(a => (string)a!).ToList());
        tokens["sim-arbeitsplatz"].Should().Equal("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");

        // ── 5. Umgebungs-Werte sind eine Geste: Verlauf und ↶ ──
        var umgDatei = Path.Combine(w.Kopie, "umgebungen", $"umgebung.{Werkbanklauf.Umgebung}.json");
        var umgVorher = await File.ReadAllBytesAsync(umgDatei);
        var umg = await w.HoleAsync($"/api/werkbank/umgebung/{Werkbanklauf.Umgebung}");
        umg["Funktionen"]!["IRangeSuche"]!["Slots"] = 3;
        var (_, umgGeste) = await w.SendeAsync($"/api/werkbank/umgebung/{Werkbanklauf.Umgebung}", umg);
        ((bool?)umgGeste["ok"]).Should().BeTrue(umgGeste.ToJsonString());
        (await File.ReadAllTextAsync(umgDatei)).Should().Contain("\"Slots\": 3");
        ((bool?)(await w.HoleAsync("/api/werkbank/zustand"))["umgebungGeaendert"]).Should().BeTrue("der laufende Host kennt den neuen Wert erst nach ⟳");
        var (_, umgZurueck) = await w.SendeAsync("/api/werkbank/rueckgaengig");
        ((bool?)umgZurueck["ok"]).Should().BeTrue(umgZurueck.ToJsonString());
        (await File.ReadAllBytesAsync(umgDatei)).Should().Equal(umgVorher, "↶ nimmt auch Umgebungs-Werte zurück");

        // ── 6. 🤖-Übernahme = Rumpf-Geste (derselbe Weg wie ✎): Methode und Fluss-Lambda, je mit ↶ ──
        var slots = JsonNode.Parse(await w.Http.GetStringAsync("/api/llm/slots"))!.AsArray().OfType<JsonObject>().ToList();
        foreach (var (art, teil) in new[] { ("decide", "ErstelleDatensatz"), ("fluss", "Bildeingang") })
        {
            var id = (string)slots.First(x => (string?)x["art"] == art && ((string?)x["id"])!.Contains(teil, StringComparison.Ordinal))["id"]!;
            var aktuell = (await w.HoleAsync($"/api/llm/slot?id={Uri.EscapeDataString(id)}"))["aktuell"]!;
            var rumpf = (string)aktuell["body"]!;
            var datei = Path.Combine(w.Kopie, (string)aktuell["pfad"]!);
            var vorher = await File.ReadAllBytesAsync(datei);
            // Ein Lambda bekommt einen anderen, gleichwertigen Text (sonst wäre der Patch leer); eine Methode die Prompt-Zeile.
            var neu = art == "fluss" ? rumpf.Replace("=>", "=>  ") : rumpf;
            var (_, ueb) = await w.SendeAsync("/api/llm/uebernehmen", new { id, rumpf = neu, auftrag = "Abnahme", basisHash = (string?)aktuell["hash"] });
            ((bool?)ueb["ok"]).Should().BeTrue($"{id}: {ueb.ToJsonString()}");
            var text = await File.ReadAllTextAsync(datei);
            if (art == "decide") text.Should().Contain("// 🤖 Prompt: Abnahme");
            else text.Should().Contain(neu.Trim());
            var (_, llmZurueck) = await w.SendeAsync("/api/werkbank/rueckgaengig");
            ((string?)llmZurueck["titel"]).Should().Be($"↶ 🤖 {id}", "die Übernahme steht im Verlauf der Werkbank");
            (await File.ReadAllBytesAsync(datei)).Should().Equal(vorher, $"{id}: ↶ stellt die Datei byte-gleich wieder her");
        }

        // ── 7. Geste → ⟳ → ↶ → ⟳ meldet das entfernte Feld ──
        var karte = (JsonObject)(board["records"] as JsonArray)!.OfType<JsonObject>().First(r => (string?)r["name"] == "DatensatzErstellt").DeepClone();
        karte["felder"]!.AsArray().Add(new JsonObject { ["name"] = "Notiz", ["typ"] = "string?", ["standard"] = "null" });
        var (_, geste) = await w.SendeAsync("/api/werkbank/geste",
            new JsonObject { ["titel"] = "Abnahme: Feld Notiz", ["karten"] = new JsonObject { ["records"] = new JsonArray(karte) } });
        ((bool?)geste["ok"]).Should().BeTrue(geste.ToJsonString());
        var dateien = (geste["dateien"] as JsonArray)!.Select(d => (string)d!).ToList();
        dateien.Should().ContainSingle();
        (await File.ReadAllTextAsync(Path.Combine(w.Kopie, dateien[0]))).Should().Contain("string? Notiz = null");
        ((string?)(await w.HoleAsync("/api/werkbank/zustand"))["stand"]).Should().NotBe(stand, "der Entwurf weicht jetzt vom Lauf ab");

        lauf = await KreislaufAsync(w, zuruecksetzen: false);
        SchritteGruen(lauf, Log);
        Meldungen(lauf, "Daten prüfen").Should().Contain("Events passen zum Code", "ein Feld mit Standardwert dazu ist verträglich");

        var (_, zurueck) = await w.SendeAsync("/api/werkbank/rueckgaengig");
        ((bool?)zurueck["ok"]).Should().BeTrue(zurueck.ToJsonString());
        ((string?)(await w.HoleAsync("/api/werkbank/zustand"))["stand"]).Should().Be(stand, "↶ stellt den handgeschriebenen Code byte-gleich wieder her");
        File.ReadAllBytes(Path.Combine(w.Kopie, dateien[0])).Should().Equal(nachLauf[dateien[0]]);

        lauf = await KreislaufAsync(w, zuruecksetzen: false);
        ((bool?)lauf["ok"]).Should().BeFalse("die Sim-DB trägt Events mit dem Feld, das der Code nicht mehr hat");
        var pruefen = (lauf["schritte"] as JsonArray)!.OfType<JsonObject>().Single(s => (string?)s["name"] == "Daten prüfen");
        ((bool?)pruefen["ok"]).Should().BeFalse();
        (lauf["rueckfrage"] as JsonArray)!.Select(x => (string?)x).Should()
            .Contain(x => x!.StartsWith("DatensatzErstellt.", StringComparison.Ordinal) && x.Contains("otiz entfällt", StringComparison.Ordinal),
                "die Rückfrage nennt das entfernte Feld");
        var abweichend = w.Abbild().Where(kv => !nachLauf.TryGetValue(kv.Key, out var alt) || !alt.AsSpan().SequenceEqual(kv.Value)).Select(kv => kv.Key);
        abweichend.Should().BeEmpty("nach ↶ und erneutem Bauen ist jede Quell-Datei (auch die Generate) wieder byte-gleich");

        lauf = await KreislaufAsync(w, zuruecksetzen: true);
        SchritteGruen(lauf, Log);
        Meldungen(lauf, "Daten prüfen").Should().Contain(m => m.StartsWith($"Sim-DB {w.Datenbank} zurückgesetzt", StringComparison.Ordinal));
    }

    private static async Task<JsonObject> KreislaufAsync(Werkbanklauf w, bool zuruecksetzen)
    {
        var (status, lauf) = await w.SendeAsync($"/api/werkbank/kreislauf?zuruecksetzen={(zuruecksetzen ? "true" : "false")}");
        status.Should().Be(200, lauf.ToJsonString());
        return lauf;
    }

    private static void SchritteGruen(JsonObject lauf, Func<string> log)
    {
        var schritte = (lauf["schritte"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var text = lauf.ToJsonString() + log();
        schritte.Select(s => (string?)s["name"]).Should().Equal(Schritte, text);
        schritte.Where(s => (bool?)s["ok"] != true).Should().BeEmpty(text);
        ((bool?)lauf["ok"]).Should().BeTrue(text);
    }

    private static IReadOnlyList<string> Meldungen(JsonObject lauf, string schritt)
        => (lauf["schritte"] as JsonArray)!.OfType<JsonObject>().Single(s => (string?)s["name"] == schritt)["meldungen"]!.AsArray()
            .Select(m => (string)m!).ToList();
}
