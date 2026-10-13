using System.Text.Json;
using DomainEditor;
using GraphExtractor;
using SimHost;

// Der warme Workspace (Arbeitsbereich) lädt die Solution über MSBuild — der Locator muss vor dem ersten MSBuild-Typ registriert sein.
if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered) Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();

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
    using var cliArbeit = new SimHost.Werkbank.Arbeitsbereich(SlnRoot());
    var cliErgebnis = await new LlmKonsole(SlnRoot(), new ModellSimulation(), cliArbeit).PruefePerIdAsync(args[1], File.ReadAllText(args[2]));
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

// ── WERKBANK (docs/konzept-werkbank.md §5, P3): ⟳ = bauen → Python-Basen → neu lesen → Daten prüfen → Infrastruktur → Host ersetzen.
//    Die Umgebung (Kopfzeile „Umgebung ▾") wählt Ports, DB und Cluster; der Host-Rahmen bearbeitet ihre Datei (generisches Formular).
var slnRoot = SlnRoot();
var werkbank = new SimHost.Werkbank.Werkbank(slnRoot);
// Der warme Workspace (P7.1) lädt sofort im Hintergrund — das erste Board ist da, bevor der Editor fragt.
_ = werkbank.Arbeitsbereich.StarteAsync();
app.Lifetime.ApplicationStopping.Register(werkbank.Arbeitsbereich.Dispose);

// ── EDITOR-MODUS: Modell → C# (der EINE Scaffolder) + Struktur-Prüfung. Umkehrung C# → Board. ──
// Das aktuelle Domänen-Modell (aus dem Round-trip, neben der .sln) — Startpunkt zum Weiterbauen.
// Aus dem warmen Workspace (P7.1): geänderte Dateien werden übernommen und warm neu gelesen; lädt er nicht, die letzte Datei.
app.MapGet("/api/editor/model", async (CancellationToken ct) =>
{
    try { await werkbank.Arbeitsbereich.LiesAsync(ct); if (werkbank.Arbeitsbereich.BoardJson is { } board) return Results.Content(board, "application/json"); }
    catch (InvalidOperationException) { }
    var p = FindNeben("domain-model.json");
    return p != null && File.Exists(p)
        ? Results.Content(File.ReadAllText(p), "application/json")
        : Results.Content("""{"schemaVersion":"1","aggregate":[],"sagas":[]}""", "application/json");
});

// ── GESTEN (Werkbank P7.2): jede Bearbeitung im Editor ist ein Patch am Code — Karten ändern/anlegen/löschen, Umbenennen (Renamer), ↶.
//    Danach liest der warme Workspace neu; die Antwort trägt das Board (Code = einzige Wahrheit) und die Befunde der berührten Dateien.
var gestenJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
IResult GestenErgebnis(SimHost.Werkbank.GestenAntwort a)
{
    var o = JsonSerializer.SerializeToNode(a, gestenJson)!.AsObject();
    if (werkbank.Arbeitsbereich.BoardJson is { } board) o["board"] = System.Text.Json.Nodes.JsonNode.Parse(board);
    return Results.Content(o.ToJsonString(), "application/json");
}
app.MapPost("/api/werkbank/geste", async (System.Text.Json.Nodes.JsonObject b, CancellationToken ct) =>
    GestenErgebnis(await werkbank.Gesten.AendernAsync((string?)b["titel"] ?? "Geste", GraphExtractor.KartenDelta.AusJson(b), ct)));
app.MapPost("/api/werkbank/umbenennen", async (System.Text.Json.Nodes.JsonObject b, CancellationToken ct) =>
    GestenErgebnis(await werkbank.Gesten.UmbenennenAsync((string?)b["titel"] ?? "Umbenennen", (string?)b["datei"] ?? "", (int?)b["start"] ?? -1,
        (string?)b["neu"] ?? "", ct)));
// Rumpf-Edit (✎ am Rumpf-Port, P7.4): C# = Ort aus dem Board (_rumpf bzw. _ort + Lambda-Index), Python = Anker der Client-Implementierung.
app.MapPost("/api/werkbank/rumpf", async (System.Text.Json.Nodes.JsonObject b, CancellationToken ct) =>
{
    var titel = (string?)b["titel"] ?? "✎ Rumpf";
    var text = (string?)b["text"] ?? "";
    var alt = (string?)b["alt"];
    if (b["python"] is System.Text.Json.Nodes.JsonObject py)
        return GestenErgebnis(await werkbank.Gesten.PythonRumpfAsync(titel, new SimHost.PythonAnker((string?)py["datei"] ?? "", (string?)py["klasse"] ?? "",
            (string?)py["methode"] ?? "", (string?)py["signatur"]), text, alt, ct));
    var o = b["ort"] as System.Text.Json.Nodes.JsonObject;
    return GestenErgebnis(await werkbank.Gesten.RumpfAsync(titel, new GraphExtractor.RumpfOrt((string?)o?["datei"] ?? "", (int?)o?["start"] ?? -1,
        (int?)o?["ende"] ?? -1, (int?)o?["lambda"]), text, alt, ct));
});
app.MapPost("/api/werkbank/rueckgaengig", async (CancellationToken ct) => GestenErgebnis(await werkbank.Gesten.RueckgaengigAsync(ct)));
app.MapGet("/api/werkbank/verlauf", () => Results.Json(werkbank.Gesten.Verlauf.Liste(), gestenJson));
app.MapGet("/api/werkbank/gesten", () => Results.Json(werkbank.Gesten.Messungen(), gestenJson));
// ── ABLEITUNGEN (Werkbank P7.3): was das Board nicht schon trägt (board.sicht), fragt der Editor hier — gerechnet nur auf dem Server.
// ⊕: welche Kandidaten-Ports zu einem Port passen (Anschluss-Regel + DomainEditor.Grammatik); Ports sind gezeichnete Anschlüsse, darum schickt
//   der Editor Port und Kandidaten (Schlüssel + Beschreibung), der Server urteilt.
app.MapPost("/api/werkbank/passt", async (System.Text.Json.Nodes.JsonObject b, CancellationToken ct) =>
{
    await werkbank.Arbeitsbereich.LiesAsync(ct);
    static GraphExtractor.Sicht.Port Port(System.Text.Json.Nodes.JsonNode? p) =>
        new((string?)p?["key"] ?? "", p?["info"] as System.Text.Json.Nodes.JsonObject ?? new());
    var q = Port(b["port"]);
    var kandidaten = (b["kandidaten"] as System.Text.Json.Nodes.JsonArray ?? []).Select(Port).ToList();
    var verbunden = (b["verbunden"] as System.Text.Json.Nodes.JsonArray ?? []).Select(x => (string?)x ?? "").ToHashSet(StringComparer.Ordinal);
    var u = werkbank.Arbeitsbereich.MitSicht(s => s.Passt(q, kandidaten, verbunden));
    return Results.Json(new { passt = u.Passt, gesperrt = u.Gesperrt.Select(g => new { key = g.Key, regel = g.Regel, text = g.Text }) });
});
// Die statische Kette einer Karte (der eine Fokus): details = eingeklappte Details sind eigene Karten.
app.MapGet("/api/werkbank/kette", async (string karte, bool? details, CancellationToken ct) =>
{
    await werkbank.Arbeitsbereich.LiesAsync(ct);
    return Results.Json(new { karte, ids = werkbank.Arbeitsbereich.MitSicht(s => s.KetteFuer(karte, details == true)) });
});
// Die Ansicht (Positionen, Filter, Akteur-Reihenfolge, 🤖-Knoten) — Schlüssel = Karten-Id; nicht der Code, nicht im Board.
var ansichtDatei = Path.Combine(slnRoot, ".werkbank", "ansicht.json");
app.MapGet("/api/werkbank/ansicht", () => Results.Content(File.Exists(ansichtDatei) ? File.ReadAllText(ansichtDatei) : "{}", "application/json"));
app.MapPost("/api/werkbank/ansicht", (System.Text.Json.Nodes.JsonObject inhalt) =>
{
    Directory.CreateDirectory(Path.GetDirectoryName(ansichtDatei)!);
    File.WriteAllText(ansichtDatei, inhalt.ToJsonString());
    return Results.Json(new { ok = true });
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
// LLM-Code-Blöcke (docs/konzept-llm-minimalkontext.md): Kontexte + Füllen + Übernehmen. Fehlen die Kontexte, erzeugt
// SimHost sie beim Start im Hintergrund selbst (kein CLI-Schritt nötig).
var konsole = new LlmKonsole(slnRoot, simulation, werkbank.Arbeitsbereich);
// Eine 🤖-Übernahme ist ein Patch wie jede Geste — derselbe ↶-Stapel.
konsole.Gesichert = (titel, patch) => werkbank.Gesten.Verlauf.Merke("llm", titel, patch);
konsole.ErzeugeKontexteFallsFehlend();
// Neu einlesen: der warme Workspace liest den aktuellen Code (Code = Wahrheit; der Browser merged). Die LLM-Kontexte zieht die Konsole
//   danach im Hintergrund nach (GraphExtractor --kontexte, kalt).
app.MapPost("/api/editor/extract", async (CancellationToken ct) =>
{
    try
    {
        var l = await werkbank.Arbeitsbereich.LiesAsync(ct);
        _ = Task.Run(konsole.Aktualisieren);
        return Results.Json(new { ok = true, meldung = new[] { $"✅ gelesen ({l.Routing.Source}) · Übersetzen {l.Uebersetzen.TotalMilliseconds:0} ms · Lesen {l.Extrahieren.TotalMilliseconds:0} ms" } });
    }
    catch (InvalidOperationException e) { return Results.Json(new { ok = false, grund = e.Message }); }
});

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
app.MapPost("/api/llm/pruefen", async (JsonElement b, CancellationToken ct) => Results.Json(await konsole.PruefePerIdAsync(
    b.GetProperty("id").GetString()!, b.GetProperty("rumpf").GetString()!, ct)));
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

// Der Laufzeit-Host ist ein Kind der Werkbank: endet SimHost, endet er mit (sonst hielte ein verwaister Host Ports und Sim-DB).
app.Lifetime.ApplicationStopping.Register(() => werkbank.Host.StoppeAsync().GetAwaiter().GetResult());
var werkbankJson = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
app.MapGet("/api/werkbank/zustand", () => Results.Json(werkbank.Zustand(), werkbankJson));
app.MapGet("/api/werkbank/schema", async (CancellationToken ct) =>
{
    // Dienste und Konfig-Records kommen aus dem Code (warmer Workspace) — ohne gelesenes Board bleiben sie freie Abschnitte.
    try { await werkbank.Arbeitsbereich.LiesAsync(ct); } catch (Exception) { }
    var board = werkbank.Arbeitsbereich.BoardJson is { } b ? System.Text.Json.Nodes.JsonNode.Parse(b) as System.Text.Json.Nodes.JsonObject : null;
    return Results.Content(SimHost.Werkbank.Schema.Umgebung(board).ToJsonString(), "application/json");
});
app.MapGet("/api/werkbank/umgebung/{name}", (string name) => Results.Content(werkbank.Umgebungen.Roh(name).ToJsonString(), "application/json"));
app.MapPost("/api/werkbank/umgebung/{name}", (string name, System.Text.Json.Nodes.JsonObject inhalt) =>
{
    werkbank.Umgebungen.Schreibe(name, inhalt);
    werkbank.Fortschritt.Melde("umgebung", new { name });
    // Die Werte gehören zum Board (Quellkarte, Webhook, Wahl der Dienste) — sofort neu lesen, nicht erst, wenn der Wächter meldet.
    _ = werkbank.Arbeitsbereich.UebernimmAsync([Path.GetRelativePath(slnRoot, werkbank.Umgebungen.Datei(name))]);
    return Results.Json(new { geschrieben = werkbank.Umgebungen.Datei(name) });
});
app.MapPost("/api/werkbank/aktiv/{name}", (string name) => { werkbank.Aktiv = name; return Results.Json(werkbank.Zustand(), werkbankJson); });
app.MapPost("/api/werkbank/kreislauf", async (bool? zuruecksetzen, CancellationToken ct) =>
{
    try { return Results.Json(await werkbank.KreislaufAsync(zuruecksetzen == true, ct), werkbankJson); }
    catch (Exception e) when (e is not OperationCanceledException) { return Results.Json(new { ok = false, fehler = e.Message }, werkbankJson); }
});
app.MapGet("/api/werkbank/kreislauf", () => Results.Json(werkbank.Letzter, werkbankJson));
// Befunde des letzten ⟳ (Compiler/Analyzer mit Typ + Member) und Boot-Fehler (mit Karte) — der Editor zeigt sie an der Karte.
// Dazu die Lese-Befunde des aktuellen Boards (warmer Workspace): was das Lesen nicht fassen konnte, steht an seiner Karte.
app.MapGet("/api/werkbank/diagnosen", () => Results.Json(new
{
    // Je Datei gilt der jüngere Stand: was eine Geste seit dem letzten ⟳ berührt hat, zeigt ihre Befunde (warmer Workspace), sonst der Bau.
    diagnosen = GestenUndBau(),
    boot = werkbank.Host.Laeuft || werkbank.Letzter == null ? werkbank.Host.Boot() : werkbank.Letzter.Boot,
}, werkbankJson));
// Der warme Workspace (P7.1): Zustand und Messungen; POST liest geänderte Dateien jetzt ein.
app.MapGet("/api/werkbank/lesen", () => Results.Json(werkbank.Arbeitsbereich.Zustand(), werkbankJson));
app.MapPost("/api/werkbank/lesen", async (CancellationToken ct) =>
{
    try { await werkbank.Arbeitsbereich.LiesAsync(ct); return Results.Json(werkbank.Arbeitsbereich.Zustand(), werkbankJson); }
    catch (InvalidOperationException e) { return Results.Json(new { ok = false, fehler = e.Message }, werkbankJson); }
});
IEnumerable<SimHost.Werkbank.Diagnose> GestenUndBau()
{
    var (beruehrt, gesten) = werkbank.Gesten.Befunde();
    return (werkbank.Letzter?.Diagnosen ?? []).Where(d => !d.Code.StartsWith("LESEN", StringComparison.Ordinal) && !beruehrt.Contains(d.Datei))
        .Concat(gesten).Concat(werkbank.Arbeitsbereich.Befunde());
}
// Fortschritt als Server-Sent Events (verlierbar; die Wahrheit bleibt /api/werkbank/zustand).
app.MapGet("/api/werkbank/ereignisse", (HttpContext ctx) => werkbank.Fortschritt.SendeAsync(ctx.Response, ctx.RequestAborted));
app.MapPost("/api/werkbank/host/stopp", async () => { await werkbank.Host.StoppeAsync(); return Results.Json(werkbank.Host.Zustand(), werkbankJson); });
app.MapGet("/api/werkbank/host/log", (int? max) => Results.Json(werkbank.Host.Log(max ?? 300)));
app.MapGet("/api/werkbank/simdb/staende", async () =>
{
    var u = werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv);
    if (u.Umgebung.Art != Abstractions.UmgebungsArt.Sim) return Results.Json(Array.Empty<object>());
    var proto = Path.Combine(slnRoot, "ProtoRepo", "domain.proto");
    try { return Results.Json(await werkbank.Db.StaendeAsync(u, File.Exists(proto) ? await File.ReadAllTextAsync(proto) : null), werkbankJson); }
    catch (Exception e) { return Results.Json(new { fehler = e.Message }, werkbankJson); }
});
app.MapPost("/api/werkbank/simdb/{aktion}", async (string aktion, string? stand, CancellationToken ct) =>
{
    try { return Results.Json(await werkbank.DatenbankAsync(aktion, stand, ct), werkbankJson); }
    catch (Exception e) when (e is not OperationCanceledException) { return Results.Json(new { ok = false, fehler = e.Message }, werkbankJson); }
});
// Die Uhr der Sim-DB (§4.4): der Versatz steht in der Datenbank, nicht in der Umgebungs-Datei; der Host liest ihn bei jedem Zugriff.
app.MapGet("/api/werkbank/uhr", async () =>
{
    var u = werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv);
    try { return Results.Json(new { art = u.Umgebung.Art.ToString(), versatz = (await werkbank.Db.VersatzAsync(u))?.ToString() }); }
    catch (Exception e) { return Results.Json(new { art = u.Umgebung.Art.ToString(), versatz = (string?)null, fehler = e.Message }); }
});
app.MapPost("/api/werkbank/uhr", async (string versatz) =>
{
    try
    {
        await werkbank.Db.SetzeVersatzAsync(werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv), TimeSpan.Parse(versatz, System.Globalization.CultureInfo.InvariantCulture));
        werkbank.Fortschritt.Melde("uhr", new { versatz });
        return Results.Json(new { ok = true, versatz });
    }
    catch (Exception e) { return Results.Json(new { ok = false, fehler = e.Message }); }
});
// ── DURCHSPIELEN (docs/konzept-werkbank.md §6, P5): nur echte Eingänge — als Akteur (JSON-Eingang des Hosts), Hand-Funktionen beantworten,
//   Datei in den Pfad der Quelle legen, Uhr vorstellen, externe Worker starten. Die Formulare kommen aus dem gelesenen Code.
var durchspielen = new SimHost.Werkbank.Durchspielen(werkbank, () => FindNeben("domain-model.json"));
var worker = new SimHost.Werkbank.Worker(slnRoot);
app.Lifetime.ApplicationStopping.Register(() => worker.StoppeAlleAsync().GetAwaiter().GetResult());
app.MapGet("/api/werkbank/identitaeten", () => Results.Json(durchspielen.Identitaeten()));
app.MapGet("/api/werkbank/formular", (string typ) =>
{
    try { return Results.Content(durchspielen.Formular(typ).ToJsonString(), "application/json"); }
    catch (Exception e) when (e is KeyNotFoundException or FileNotFoundException) { return Results.Json(new { fehler = e.Message }, statusCode: 404); }
});
app.MapMethods("/api/werkbank/eingang/{**pfad}", ["GET", "POST"], async (HttpContext ctx, string pfad, CancellationToken ct) =>
{
    string? inhalt = null;
    if (HttpMethods.IsPost(ctx.Request.Method)) using (var r = new StreamReader(ctx.Request.Body)) inhalt = await r.ReadToEndAsync(ct);
    var (status, antwort) = await durchspielen.EingangAsync(ctx.Request.Method, pfad, ctx.Request.Headers["akteur-token"].FirstOrDefault(), inhalt, ct);
    return Results.Content(antwort, "application/json", System.Text.Encoding.UTF8, status);
});
app.MapGet("/api/werkbank/quelle/{quelle}", (string quelle) => Results.Json(new { quelle, umgebung = werkbank.Aktiv, pfad = durchspielen.QuellPfad(quelle) }));
app.MapPost("/api/werkbank/quelle/{quelle}/datei", async (HttpContext ctx, string quelle, string name, CancellationToken ct) =>
{
    // Bilder dürfen groß sein (TIFF) — die Grenze von Kestrel (30 MB) gilt hier nicht.
    if (ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } grenze) grenze.MaxRequestBodySize = null;
    try { return Results.Json(await durchspielen.LegeAbAsync(quelle, name, ctx.Request.Body, ct)); }
    catch (Exception e) when (e is not OperationCanceledException) { return Results.Json(new { ok = false, fehler = e.Message }); }
});
app.MapPost("/api/werkbank/uhr/vor", async (string um) =>
{
    try { return Results.Json(await durchspielen.UhrVorAsync(TimeSpan.Parse(um, System.Globalization.CultureInfo.InvariantCulture))); }
    catch (Exception e) { return Results.Json(new { ok = false, fehler = e.Message }); }
});
app.MapGet("/api/werkbank/worker", () =>
{
    try { return Results.Json(new { umgebung = werkbank.Aktiv, worker = worker.Liste(werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv)) }); }
    catch (Exception e) { return Results.Json(new { umgebung = werkbank.Aktiv, worker = Array.Empty<object>(), fehler = e.Message }); }
});
app.MapPost("/api/werkbank/worker/{funktion}/start", async (string funktion) =>
{
    try
    {
        // Ohne Token in der Umgebung: ein Token der Umgebung, dessen Akteur die Funktion anbieten darf (IDarf<F>, aus dem Code).
        string? StandardToken(string f)
        {
            var board = werkbank.Arbeitsbereich.BoardJson is { } bj ? System.Text.Json.Nodes.JsonNode.Parse(bj) : null;
            var duerfen = (board?["akteure"] as System.Text.Json.Nodes.JsonArray ?? []).OfType<System.Text.Json.Nodes.JsonObject>()
                .Where(a => (a["darf"] as System.Text.Json.Nodes.JsonArray ?? []).Any(x => (string?)x == f)).Select(a => (string?)a["name"]).ToHashSet();
            var tokens = werkbank.Umgebungen.Roh(werkbank.Aktiv)["Identitaeten"]?["Tokens"] as System.Text.Json.Nodes.JsonObject;
            return tokens?.Where(kv => ((string?)kv.Value ?? "").Split(',').Any(a => duerfen.Contains(a.Trim()))).Select(kv => kv.Key).Order(StringComparer.Ordinal).FirstOrDefault();
        }
        var aus = await worker.StarteAsync(funktion, werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv), werkbank.Host.Laeuft, StandardToken);
        werkbank.Fortschritt.Melde("worker", new { funktion });
        return Results.Json(aus);
    }
    catch (Exception e) { return Results.Json(new { ok = false, funktion, fehler = e.Message }); }
});
app.MapPost("/api/werkbank/worker/{funktion}/stopp", async (string funktion) =>
{
    await worker.StoppeAsync(funktion);
    werkbank.Fortschritt.Melde("worker", new { funktion });
    return Results.Json(new { ok = true, funktion });
});
app.MapGet("/api/werkbank/worker/{funktion}/log", (string funktion, int? max) => Results.Json(worker.Log(funktion, max ?? 200)));
app.MapPost("/api/werkbank/infra/start", async (CancellationToken ct) =>
    Results.Json(await werkbank.Infra.StarteAsync(werkbank.Aktiv, werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv), ct), werkbankJson));
app.MapPost("/api/werkbank/infra/stopp", async (CancellationToken ct) => Results.Json(await werkbank.Infra.StoppeAsync(werkbank.Aktiv, ct), werkbankJson));

// ── SPUR (docs/konzept-werkbank.md §7, P6): der Spiegel folgt dem laufenden Host (SSE + Cursor) und liefert Vorgänge, nummerierte Wege
//   und den Live-Puls fertig aus — der Editor schlägt nur Karten-Ids nach.
var spur = new SimHost.Werkbank.SpurSpiegel(werkbank, () => FindNeben("domain-model.json"));
_ = Task.Run(() => spur.LaufeAsync(app.Lifetime.ApplicationStopping));
app.MapGet("/api/werkbank/spur/puls", async (CancellationToken ct) => Results.Json(await spur.PulsAsync(ct), werkbankJson));
app.MapGet("/api/werkbank/spur/vorgaenge", async (string? quelle, string? akteur, string? aggregat, string? zustand, CancellationToken ct) =>
{
    try { return Results.Json(await spur.VorgaengeAsync(quelle, akteur, aggregat, zustand, ct), werkbankJson); }
    catch (Exception e) when (e is not OperationCanceledException) { return Results.Json(new { laeuft = true, fehler = e.Message, vorgaenge = Array.Empty<object>() }, werkbankJson); }
});
app.MapGet("/api/werkbank/spur/vorgang/{korrelation}", async (string korrelation, CancellationToken ct) =>
{
    try { return Results.Json(await spur.VorgangAsync(korrelation, ct), werkbankJson); }
    catch (Exception e) when (e is not OperationCanceledException) { return Results.Json(new { laeuft = true, korrelation, fehler = e.Message }, werkbankJson); }
});

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
