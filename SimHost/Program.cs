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

// Kommandozeile (ohne Web, ohne LLM): einen Kandidaten-Rumpf für einen Code-Block prüfen, wie die Konsole es vor dem
// Schreiben tut — --pruefe-rumpf <Slot-Id> <Datei mit Rumpf>. Schreibt nichts.
if (args.Length >= 3 && args[0] == "--pruefe-rumpf")
{
    var cliErgebnis = new LlmKonsole(SlnRoot(), new ModellSimulation()).PruefePerId(args[1], File.ReadAllText(args[2]));
    Console.WriteLine(JsonSerializer.Serialize(cliErgebnis, new JsonSerializerOptions { WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

// ── EINE Oberfläche: der Domänen-Editor unter /editor — statische Dateien aus wwwroot/editor (index.html, editor.css, editor.js). ──
// no-store: der Browser holt die Oberfläche immer frisch (sonst sieht man nach einem Editor-Update die alte Seite).
app.Use(async (ctx, next) =>
{
    var pfad = ctx.Request.Path.Value ?? "";
    if (pfad is "" or "/" or "/editor") { ctx.Response.Redirect("/editor/"); return; }
    if (pfad.StartsWith("/editor/", StringComparison.Ordinal)) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();

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
    // Client-Implementierungen (Python) entstehen aus der GENERIERTEN Basis — die schreibt der Build (Codegen-Prepass). Daher erst nach
    //   dem Bauen; im Trockenlauf nur gemeldet.
    void Python() { var (g, u) = PythonSync.ImplementierungenAnlegen(modell, root, trocken == true); bericht.Geschrieben.AddRange(g); bericht.Uebersprungen.AddRange(u); }
    if (trocken == true) { Python(); return Results.Json(bericht, EditorModell.JsonOptionen); }
    var fehler = new List<string>();
    foreach (var projekt in bericht.Dateien.Select(d => ProjektVon(d, root)).Where(p => p != null).Distinct())
    {
        var b = System.Text.Json.JsonSerializer.SerializeToElement(CodeSync.BaueProjekt(projekt!, root));
        if (b.TryGetProperty("fehler", out var fs)) fehler.AddRange(fs.EnumerateArray().Select(f => f.GetString() ?? "").Where(f => f.Length > 0));
    }
    Python();
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
    JsonElement? wahl = body.TryGetProperty("wahl", out var w) ? w : null;
    return Results.Json(simulation.Schritt(modell, sid, command, werte, wahl), EditorModell.JsonOptionen);
});
// Pipeline als Fluss durchspielen (§14): Quell-Nachricht einspeisen, je Funktionsknoten der gewählte Ausgang (Ergebnis / ⏳ / ✕),
// Commands laufen in dieselbe Aggregat-Kaskade. Body = { model, sessionId, pipeline, values, wahl, strom? } (strom = Aggregat bei p.Auf<E>()).
app.MapPost("/api/editor/sim/fluss", (JsonElement body) =>
{
    var modell = EditorModell.AusJson(body.GetProperty("model").GetRawText());
    var sid = body.TryGetProperty("sessionId", out var s) ? s.GetString() ?? "editor" : "editor";
    var pipeline = body.GetProperty("pipeline").GetString() ?? "";
    var werte = body.TryGetProperty("values", out var v) ? v : default;
    JsonElement? wahl = body.TryGetProperty("wahl", out var w) ? w : null;
    Guid? strom = body.TryGetProperty("strom", out var st) && Guid.TryParse(st.GetString(), out var g) ? g : null;
    return Results.Json(simulation.StarteFluss(modell, sid, pipeline, werte, wahl, strom), EditorModell.JsonOptionen);
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
app.MapPost("/api/llm/pruefen", (JsonElement b) => Results.Json(konsole.PruefePerId(
    b.GetProperty("id").GetString()!, b.GetProperty("rumpf").GetString()!)));
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

// ── WERKBANK (docs/konzept-werkbank.md §5, P3): ⟳ = bauen → Python-Basen → neu lesen → Daten prüfen → Infrastruktur → Host ersetzen.
//    Die Umgebung (Kopfzeile „Umgebung ▾") wählt Ports, DB und Cluster; der Host-Rahmen bearbeitet ihre Datei (generisches Formular).
var werkbank = new SimHost.Werkbank.Werkbank(slnRoot);
var werkbankJson = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
app.MapGet("/api/werkbank/zustand", () => Results.Json(werkbank.Zustand(), werkbankJson));
app.MapGet("/api/werkbank/schema", () => Results.Content(SimHost.Werkbank.Schema.Umgebung().ToJsonString(), "application/json"));
app.MapGet("/api/werkbank/umgebung/{name}", (string name) => Results.Content(werkbank.Umgebungen.Roh(name).ToJsonString(), "application/json"));
app.MapPost("/api/werkbank/umgebung/{name}", (string name, System.Text.Json.Nodes.JsonObject inhalt) =>
{
    werkbank.Umgebungen.Schreibe(name, inhalt);
    return Results.Json(new { geschrieben = werkbank.Umgebungen.Datei(name) });
});
app.MapPost("/api/werkbank/aktiv/{name}", (string name) => { werkbank.Aktiv = name; return Results.Json(werkbank.Zustand(), werkbankJson); });
app.MapPost("/api/werkbank/kreislauf", async (bool? zuruecksetzen, CancellationToken ct) =>
    Results.Json(await werkbank.KreislaufAsync(zuruecksetzen == true, ct), werkbankJson));
app.MapGet("/api/werkbank/kreislauf", () => Results.Json(werkbank.Letzter, werkbankJson));
app.MapPost("/api/werkbank/host/stopp", async () => { await werkbank.Host.StoppeAsync(); return Results.Json(werkbank.Host.Zustand(), werkbankJson); });
app.MapGet("/api/werkbank/host/log", (int? max) => Results.Json(werkbank.Host.Log(max ?? 300)));
app.MapGet("/api/werkbank/simdb/staende", async () => Results.Json(await werkbank.Db.StaendeAsync(werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv))));
app.MapPost("/api/werkbank/simdb/{aktion}", async (string aktion, string? stand, CancellationToken ct) =>
{
    try { return Results.Json(await werkbank.DatenbankAsync(aktion, stand, ct), werkbankJson); }
    catch (Exception e) when (e is InvalidOperationException or ArgumentException) { return Results.Json(new { ok = false, fehler = e.Message }); }
});
app.MapPost("/api/werkbank/infra/start", async (CancellationToken ct) =>
    Results.Json(await werkbank.Infra.StarteAsync(werkbank.Aktiv, werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv), ct), werkbankJson));
app.MapPost("/api/werkbank/infra/stopp", async (CancellationToken ct) => Results.Json(await werkbank.Infra.StoppeAsync(werkbank.Aktiv, ct), werkbankJson));

// Port: 5178, außer die Umgebung weist einen zu (PORT) — z. B. wenn mehrere Editor-Instanzen parallel laufen.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5178";
Console.WriteLine($"\n▶ SimHost läuft.  Editor: http://localhost:{port}/editor/   LLM-Konsole: http://localhost:{port}/konsole\n");
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


