using System.Text.Json;
using DomainEditor;
using SimHost;

// Kommandozeile (ohne Web): „C# schreiben“ für ein Board-JSON — --trocken zeigt nur, was geschähe; --schreiben schreibt.
if (args.Length >= 2 && args[0] is "--trocken" or "--schreiben")
{
    var cliModell = BoardLeseseite.AusBoard(File.ReadAllText(args[1]));
    var cliBericht = DateiSchreiber.Schreibe(cliModell, SlnRoot(), trocken: args[0] == "--trocken");
    Console.WriteLine(JsonSerializer.Serialize(cliBericht, new JsonSerializerOptions
    {
        WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

// ── EINE Oberfläche: der Domänen-Editor unter /editor (inkl. Simulation). Das alte Board ist abgelöst. ──
app.MapGet("/", () => Results.Redirect("/editor"));
var editorPath = FindNeben("editor.html");
// no-store: der Browser holt die Oberfläche immer frisch (sonst sieht man nach einem Editor-Update die alte Seite).
app.MapGet("/editor", (HttpContext ctx) => { ctx.Response.Headers.CacheControl = "no-store"; return editorPath != null && File.Exists(editorPath)
    ? Results.Content(File.ReadAllText(editorPath), "text/html")
    : Results.Content("<h1>editor.html nicht gefunden</h1><p>Erst <code>dotnet run --project GraphExtractor</code> laufen lassen.</p>", "text/html"); });

// ── EDITOR-MODUS: Modell → C# (der EINE Scaffolder) + Struktur-Prüfung. Umkehrung C# → Board. ──
// Das aktuelle Domänen-Modell (aus dem Round-trip, neben der .sln) — Startpunkt zum Weiterbauen.
app.MapGet("/api/editor/model", () =>
{
    var p = FindNeben("domain-model.json");
    return p != null && File.Exists(p)
        ? Results.Content(File.ReadAllText(p), "application/json")
        : Results.Content("""{"schemaVersion":"1","aggregate":[],"sagas":[]}""", "application/json");
});

// ── VISUELLER EDITOR: das VOLLE Board-Modell (alle Node-Sammlungen + Layout) durabel. ──
// Quelle des visuellen Editors — unabhängig vom Scaffolder. Enthält Leseseite, Reaktionen,
// Pipelines, Trigger, Code-/LLM-Knoten und die Knotenpositionen (x/y), die im EditorModell
// (Schreibseite) bewusst NICHT stehen. Fehlt die Datei → 404, der Editor fällt dann auf die
// aus C# abgeleitete Schreibseite (/api/editor/model) zurück.
app.MapGet("/api/editor/board", () =>
{
    var p = FindNeben("board-model.json");
    return p != null && File.Exists(p)
        ? Results.Content(File.ReadAllText(p), "application/json")
        : Results.NotFound();
});

// Volles Board-Modell speichern (roh, hübsch eingerückt für lesbare Diffs). Body = das komplette MODEL.
app.MapPost("/api/editor/board", (JsonElement body) =>
{
    var ziel = ZielNeben("board-model.json");
    var hübsch = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(ziel, hübsch);
    return Results.Json(new { gespeichert = ziel });
});

// Modell → C# in kanonischer Gestalt (deterministisch, rein). Body = EditorModell-JSON.
app.MapPost("/api/editor/scaffold", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetRawText());
    return Results.Json(Scaffolder.Generiere(modell), EditorModell.JsonOptionen);
});

// Modell → ECHTE .cs-Dateien schreiben (chirurgisch/additiv: neue Records/Methoden anhängen,
// Handcode nie überschreiben). Body = EditorModell-JSON. So erscheint z. B. ein neues Event in Events.cs.
// Der Body ist das volle Board-MODEL: die Leseseite (Stores/Fähigkeiten, Projektionen, Reader, Pipeline-Fähigkeiten,
// ReadModels) liest BoardLeseseite aus den Board-Sammlungen. Danach werden die betroffenen Projekte gebaut — ein gelöster
// Fähigkeits-Parameter, den der Rumpf noch benutzt, kommt so als Compiler-Fehler zurück (nicht still).
app.MapPost("/api/editor/write", (JsonElement body, bool? trocken) =>
{
    var modell = BoardLeseseite.AusBoard(body.GetRawText());
    var root = SlnRoot();
    var bericht = DateiSchreiber.Schreibe(modell, root, trocken == true);
    if (trocken == true) return Results.Json(bericht, EditorModell.JsonOptionen);
    var fehler = new List<string>();
    foreach (var projekt in bericht.Dateien.Select(d => ProjektVon(d, root)).Where(p => p != null).Distinct())
    {
        var b = System.Text.Json.JsonSerializer.SerializeToElement(CodeSync.BaueProjekt(projekt!, root));
        if (b.TryGetProperty("fehler", out var fs)) fehler.AddRange(fs.EnumerateArray().Select(f => f.GetString() ?? "").Where(f => f.Length > 0));
    }
    return Results.Json(bericht with { Fehler = fehler.Distinct().ToList() }, EditorModell.JsonOptionen);
});

// Das Projekt (relativ, nächste .csproj aufwärts) einer geschriebenen Datei.
static string? ProjektVon(string relDatei, string root)
{
    var dir = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, relDatei)));
    while (dir != null && dir.StartsWith(root, StringComparison.Ordinal))
    {
        var p = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
        if (p != null) return Path.GetRelativePath(root, p).Replace('\\', '/');
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

// Struktur-Guardrails auf dem Modell (spiegeln Compiler/Analyzer). Body = EditorModell-JSON.
app.MapPost("/api/editor/validate", (JsonElement body) =>
{
    var modell = BoardLeseseite.AusBoard(body.GetRawText());   // inkl. Leseseite (dieselbe Sicht wie „C# schreiben")
    return Results.Json(Validator.Prüfe(modell), EditorModell.JsonOptionen);
});

// Kapselung: die Module (Namespaces) mit ihren abgeleiteten Ports — dieselbe Ableitung, die der Editor live rechnet
// (Parität: der Editor vergleicht seine Ports mit diesen Zeilen). Body = das volle Board-MODEL.
app.MapPost("/api/editor/module", (JsonElement body) =>
{
    var modell = BoardLeseseite.AusBoard(body.GetRawText());
    var module = Module.Ableiten(modell);
    return Results.Json(new { module, zeilen = Module.AlsZeilen(module) }, EditorModell.JsonOptionen);
});

// ── SIMULATION: EINE Laufzeit — Modell → Scaffolder → In-Memory-Kompilat (echte Generatoren) → SagaLaufwerk. ──
var simulation = new ModellSimulation();

// Nur übersetzen: der „wird das kompiliert?"-Beweis + Self-Repair-Signal. Body = EditorModell-JSON.
app.MapPost("/api/editor/compile", (JsonElement body) =>
    Results.Json(simulation.Kompiliere(EditorModell.AusJson(body.GetRawText())), EditorModell.JsonOptionen));

// Einen Command mit Werten in die Session schicken → Frames (Kaskade inkl. Sagas), Instanzen, Saga-Markings,
// Abdeckung. Geändertes Modell ⇒ Hot-Reload (neu übersetzen + Geschichte nachspielen).
// Body = { model, sessionId, command, values }.
app.MapPost("/api/editor/sim/step", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetProperty("model").GetRawText());
    var sid = body.TryGetProperty("sessionId", out var s) ? s.GetString() ?? "editor" : "editor";
    var command = body.GetProperty("command").GetString() ?? "";
    var werte = body.TryGetProperty("values", out var v) ? v : default;
    return Results.Json(simulation.Schritt(modell, sid, command, werte), EditorModell.JsonOptionen);
});
app.MapPost("/api/editor/sim/reset", (JsonElement body) =>
{
    simulation.Reset(body.TryGetProperty("sessionId", out var s) ? s.GetString() ?? "editor" : "editor");
    return Results.Ok();
});
app.MapGet("/api/editor/sim/state", (string? sessionId) => Results.Json(simulation.Stand(sessionId ?? "editor"), EditorModell.JsonOptionen));
// Session → Regressionstest in der Test-DSL (Szenario.Für…Vorab…Wenn…Dann).
app.MapGet("/api/editor/sim/dsl", (string? sessionId) => Results.Text(simulation.Dsl(sessionId ?? "editor"), "text/plain"));

// ── CODE-SYNC: Board ⇄ echte .cs-Datei (Decider/Applier-Rümpfe). Anker = aus dem Graphen abgeleitet. ──
var slnRoot = SlnRoot();
// LLM-Code-Blöcke (docs/konzept-llm-minimalkontext.md): Kontexte + Füllen + Übernehmen. Fehlen die Kontexte, erzeugt
// SimHost sie beim Start im Hintergrund selbst (kein CLI-Schritt nötig).
var konsole = new LlmKonsole(slnRoot, simulation);
konsole.ErzeugeKontexteFallsFehlend();
static CodeAnker Anker(JsonElement b) => new(
    b.GetProperty("kind").GetString() ?? "decider",
    b.GetProperty("namespace").GetString() ?? "",
    b.GetProperty("disc").GetString() ?? "",
    b.TryGetProperty("datei", out var d) ? d.GetString() : null);

// „✎ Im Editor öffnen“ — SimHost öffnet die echte Datei im Standard-Editor (VS Code, Sprung zur Rumpfzeile).
app.MapPost("/api/editor/open", (JsonElement b) => Results.Json(CodeSync.Oeffne(Anker(b), slnRoot)));

// Spiegel (Datei → Browser): aktuellen Rumpf + Prompt + Hash lesen. Polling dieses Endpunkts = das Double-Binding.
app.MapGet("/api/editor/code", (string kind, string @namespace, string disc, string? datei) =>
    Results.Json(CodeSync.Lese(new CodeAnker(kind, @namespace, disc, datei), slnRoot)));

// Browser → Datei: NUR die Prompt-Kommentarzeile setzen (optimistische Sperre via baseHash).
app.MapPost("/api/editor/code", (JsonElement b) =>
{
    var prompt = b.TryGetProperty("prompt", out var p) ? p.GetString() : null;
    var baseHash = b.TryGetProperty("baseHash", out var h) ? h.GetString() : null;
    return Results.Json(CodeSync.SetzePrompt(Anker(b), prompt, baseHash, slnRoot));
});

// Neu einlesen: GraphExtractor über den aktuellen Code → domain-model.json (Code = Wahrheit; der Browser merged).
// Ein Lauf schreibt domain-model.json UND die LLM-Kontexte — Modell und Kontexte bleiben so immer auf demselben Code-Stand.
app.MapPost("/api/editor/extract", () => Results.Json(konsole.Aktualisieren()));

// Bauen (entprellt vom Browser aufgerufen): Generatoren + Proto-Prepass laufen bei dotnet build mit.
app.MapPost("/api/editor/build", () => Results.Json(CodeSync.Baue(slnRoot)));

// ── LLM-KONSOLE: einen Code-Block füllen → prüfen → anpassen → übernehmen (docs/konzept-llm-minimalkontext.md). ──
// Anbieter: ausschließlich Claude Code (`claude -p`) mit der Anmeldung dieses Rechners — Abo, keine API-Kosten
// (ein gesetzter ANTHROPIC_API_KEY blockiert den Aufruf). BRACTOR_LLM_MODELL wählt optional das Modell.
app.MapGet("/konsole", () => Results.Content(KonsoleSeite.Html, "text/html"));
app.MapGet("/api/llm/status", () => Results.Json(konsole.Status()));
app.MapGet("/api/llm/slots", () => Results.Json(konsole.Slots()));
app.MapGet("/api/llm/slot", (string id) => Results.Json(konsole.SlotDetail(id), EditorModell.JsonOptionen));
app.MapPost("/api/llm/fuellen", async (JsonElement b, CancellationToken ct) => Results.Json(await konsole.FuellenAsync(new LlmKonsole.FuellAnfrage(
    b.GetProperty("id").GetString()!,
    b.TryGetProperty("auftrag", out var au) ? au.GetString() ?? "" : "",
    b.TryGetProperty("rumpf", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null,
    b.TryGetProperty("anpassung", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() : null,
    !b.TryGetProperty("autoReparatur", out var ar) || ar.ValueKind != JsonValueKind.False,
    b.TryGetProperty("maxRunden", out var mr) && mr.TryGetInt32(out var m) ? m : 3), ct)));
// Der EINE Durchlauf am 🤖-Knoten: füllen → prüfen → geprüften Rumpf in die Datei → Generatoren (Laufzeit-Build).
app.MapPost("/api/llm/ausfuehren", async (JsonElement b, CancellationToken ct) => Results.Json(await konsole.AusfuehrenAsync(new LlmKonsole.FuellAnfrage(
    b.GetProperty("id").GetString()!,
    b.TryGetProperty("auftrag", out var au) ? au.GetString() ?? "" : "",
    b.TryGetProperty("rumpf", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null,
    b.TryGetProperty("anpassung", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() : null,
    true, b.TryGetProperty("maxRunden", out var mr) && mr.TryGetInt32(out var m) ? m : 3), ct)));
app.MapPost("/api/llm/simulieren", (JsonElement b) => Results.Json(konsole.Simuliere(
    b.GetProperty("id").GetString()!, b.GetProperty("rumpf").GetString()!, b.GetProperty("command").GetString()!,
    b.TryGetProperty("werte", out var w) ? w : default, b.TryGetProperty("neu", out var n) && n.ValueKind == JsonValueKind.True), EditorModell.JsonOptionen));
app.MapPost("/api/llm/uebernehmen", (JsonElement b) => Results.Json(konsole.Uebernehmen(
    b.GetProperty("id").GetString()!, b.GetProperty("rumpf").GetString()!,
    b.TryGetProperty("auftrag", out var a) ? a.GetString() : null,
    b.TryGetProperty("basisHash", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null,
    b.TryGetProperty("bauen", out var bau) && bau.ValueKind == JsonValueKind.True)));
app.MapPost("/api/llm/rueckgaengig", (JsonElement b) => Results.Json(konsole.Rueckgaengig(b.GetProperty("id").GetString()!,
    b.TryGetProperty("bauen", out var bau) && bau.ValueKind == JsonValueKind.True)));
app.MapPost("/api/llm/bauen", (JsonElement b) => Results.Json(konsole.Bauen(b.GetProperty("id").GetString()!)));
app.MapPost("/api/llm/aktualisieren", () => Results.Json(konsole.Aktualisieren()));
// Datei-Spiegel für JEDE Slot-Art (Projektion, Reader, Pipeline, Store …): aktueller Rumpf + Prompt + Hash.
app.MapGet("/api/llm/rumpf", (string id) => Results.Json(konsole.Rumpf(id)));
app.MapGet("/api/llm/protokoll", (string? id, int? max) => Results.Json(konsole.Protokoll(string.IsNullOrEmpty(id) ? null : id, max ?? 300)));

// Port: 5178, außer die Umgebung weist einen zu (PORT) — z. B. wenn mehrere Editor-Instanzen parallel laufen.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5178";
Console.WriteLine($"\n▶ SimHost läuft.  Editor: http://localhost:{port}/editor   ({editorPath ?? "editor.html fehlt"})   LLM-Konsole: http://localhost:{port}/konsole\n");
app.Run($"http://localhost:{port}");

// Wurzelverzeichnis der .sln (für die Code-Sync-Dateipfade).
static string SlnRoot()
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10 && dir != null; i++)
    {
        if (Directory.GetFiles(dir, "*.sln").Length > 0) return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return Directory.GetCurrentDirectory();
}

// Eine Datei neben der .sln finden (bis zu 10 Ebenen aufwärts vom CWD).
static string? FindNeben(string name)
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10 && dir != null; i++)
    {
        var p = Path.Combine(dir, name);
        if (File.Exists(p)) return p;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

// Ziel-Pfad neben der .sln, AUCH wenn die Datei noch nicht existiert (zum Speichern).
// Anker = das Verzeichnis mit der .sln; sonst CWD.
static string ZielNeben(string name)
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10 && dir != null; i++)
    {
        if (Directory.GetFiles(dir, "*.sln").Length > 0)
            return Path.Combine(dir, name);
        dir = Path.GetDirectoryName(dir);
    }
    return Path.Combine(Directory.GetCurrentDirectory(), name);
}


