using System.Text.Json;
using DomainEditor;
using SimHost;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

// ── EINE Oberfläche: der Domänen-Editor unter /editor (inkl. Simulation). Das alte Board ist abgelöst. ──
app.MapGet("/", () => Results.Redirect("/editor"));
var editorPath = FindNeben("editor.html");
app.MapGet("/editor", () => editorPath != null && File.Exists(editorPath)
    ? Results.Content(File.ReadAllText(editorPath), "text/html")
    : Results.Content("<h1>editor.html nicht gefunden</h1><p>Erst <code>dotnet run --project GraphExtractor</code> laufen lassen.</p>", "text/html"));

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
app.MapPost("/api/editor/write", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetRawText());
    return Results.Json(DateiSchreiber.Schreibe(modell, SlnRoot()), EditorModell.JsonOptionen);
});

// Struktur-Guardrails auf dem Modell (spiegeln Compiler/Analyzer). Body = EditorModell-JSON.
app.MapPost("/api/editor/validate", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetRawText());
    return Results.Json(Validator.Prüfe(modell), EditorModell.JsonOptionen);
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
app.MapPost("/api/editor/extract", () => Results.Json(CodeSync.Extrahiere(slnRoot)));

// Bauen (entprellt vom Browser aufgerufen): Generatoren + Proto-Prepass laufen bei dotnet build mit.
app.MapPost("/api/editor/build", () => Results.Json(CodeSync.Baue(slnRoot)));

// Port: 5178, außer die Umgebung weist einen zu (PORT) — z. B. wenn mehrere Editor-Instanzen parallel laufen.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5178";
Console.WriteLine($"\n▶ SimHost läuft.  Editor: http://localhost:{port}/editor   ({editorPath ?? "editor.html fehlt"})\n");
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


