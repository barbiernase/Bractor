using System.Text.Json;
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
app.MapPost("/api/reset", (ResetRequest r) => { engine.Reset(r.SessionId ?? "default"); return Results.Ok(); });
app.MapPost("/api/step", (StepRequest r) =>
    Results.Json(engine.Step(r.SessionId ?? "default", r.Command, r.Values)));

// Board-Session als DSL-Regressionstest exportieren.
app.MapPost("/api/dsl", (DslRequest r) => Results.Text(engine.Dsl(r.SessionId ?? "default"), "text/plain"));

// Abdeckung: welche Entscheidungs-Zweige bislang (über alle Sessions) gefeuert wurden → Board grün/grau.
app.MapGet("/api/coverage", () => Results.Content(engine.CoverageJson(), "application/json"));

Console.WriteLine($"\n▶ SimHost läuft.  Board: http://localhost:5178/   (Board: {boardPath ?? "—"})\n");
app.Run("http://localhost:5178");

static string? FindBoard()
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10 && dir != null; i++)
    {
        var p = Path.Combine(dir, "knowledge-graph.html");
        if (File.Exists(p)) return p;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

record StepRequest(string? SessionId, string Command, JsonElement Values);
record ResetRequest(string? SessionId);
record DslRequest(string? SessionId);
