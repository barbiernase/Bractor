using System.Text.Json;
using DomainEditor;
using SimHost;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SimEngine>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

var engine = app.Services.GetRequiredService<SimEngine>();

// Das Board (der generierte Wissensgraph) — beim Start neben der .sln gesucht.
var boardPath = FindBoard();

app.MapGet("/", () => boardPath != null && File.Exists(boardPath)
    ? Results.Content(File.ReadAllText(boardPath), "text/html")
    : Results.Content("<h1>knowledge-graph.html nicht gefunden</h1><p>Erst <code>dotnet run --project GraphExtractor</code> laufen lassen.</p>", "text/html"));

app.MapGet("/api/schema", () => Results.Json(engine.Schema()));
app.MapGet("/api/state", (string? sessionId) => Results.Json(engine.ZustandListe(sessionId ?? "default")));
app.MapPost("/api/reset", (ResetRequest r) => { engine.Reset(r.SessionId ?? "default"); return Results.Ok(); });
app.MapPost("/api/step", (StepRequest r) =>
    Results.Json(engine.Step(r.SessionId ?? "default", r.Command, r.Values)));

// Board-Session als DSL-Regressionstest exportieren.
app.MapPost("/api/dsl", (DslRequest r) => Results.Text(engine.Dsl(r.SessionId ?? "default"), "text/plain"));

// Abdeckung: welche Entscheidungs-Zweige bislang (über alle Sessions) gefeuert wurden → Board grün/grau.
app.MapGet("/api/coverage", () => Results.Content(engine.CoverageJson(), "application/json"));

// ── EDITOR-MODUS: Modell → C# (der EINE Scaffolder) + Struktur-Prüfung. Umkehrung C# → Board. ──
// Das aktuelle Domänen-Modell (aus dem Round-trip, neben der .sln) — Startpunkt zum Weiterbauen.
app.MapGet("/api/editor/model", () =>
{
    var p = FindNeben("domain-model.json");
    return p != null && File.Exists(p)
        ? Results.Content(File.ReadAllText(p), "application/json")
        : Results.Content("""{"schemaVersion":"1","aggregate":[],"sagas":[]}""", "application/json");
});

// Modell → C# in kanonischer Gestalt (deterministisch, rein). Body = EditorModell-JSON.
app.MapPost("/api/editor/scaffold", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetRawText());
    return Results.Json(Scaffolder.Generiere(modell), EditorModell.JsonOptionen);
});

// Struktur-Guardrails auf dem Modell (spiegeln Compiler/Analyzer). Body = EditorModell-JSON.
app.MapPost("/api/editor/validate", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetRawText());
    return Results.Json(Validator.Prüfe(modell), EditorModell.JsonOptionen);
});

Console.WriteLine($"\n▶ SimHost läuft.  Board: http://localhost:5178/   (Board: {boardPath ?? "—"})\n");
app.Run("http://localhost:5178");

static string? FindBoard() => FindNeben("knowledge-graph.html");

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

record StepRequest(string? SessionId, string Command, JsonElement Values);
record ResetRequest(string? SessionId);
record DslRequest(string? SessionId);
