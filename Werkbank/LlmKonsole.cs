using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Werkbank;

// ════════════════════════════════════════════════════════════════════════════
//  LLM-Konsole (LLM-Minimalkontext, Konzept im Git-Verlauf bei 861ea7f): einen Code-Block füllen, prüfen, anpassen, übernehmen.
//
//  • Kontext = die Dateien aus `GraphExtractor --kontexte .llm-kontext` (Graph-Skelett + Slot-Teil je Code-Block).
//  • Jede Runde ist ein NEUER, zustandsloser Aufruf. Reihenfolge stabil → veränderlich:
//      Anweisung · Graph-Skelett · Slot-Teil · Auftrag · [aktueller Rumpf · Befund/Anpassung]
//    Nie ein Gesprächsverlauf — nur der letzte Rumpf reist mit.
//  • Prüfen = Bauen (Werkbank P7.4, CLAUDE.md W5): Syntax, dann der Kandidat probeweise im warmen Workspace — die Datei mit dem neuen Rumpf,
//    ihr Projekt mit seinen echten Generatoren und Analyzern (CQRS050, CQRS066/067 …); Python über den Parser.
//    Nur NEUE Fehler gegenüber dem Unveränderten zählen. Nichts wird geschrieben, der Fork wird verworfen.
//  • Ausführen (Editor, ▶ am 🤖-Knoten): ein GEPRÜFTER Rumpf geht sofort in die echte Datei, danach laufen die Generatoren
//    (Build des Laufzeit-Projekts). Übernehmen (Konsole bzw. ungeprüfter Vorschlag) schreibt auf Klick. Beides ist eine Rumpf-Geste
//    (derselbe Weg wie ✎: Hash-Sperre, Verlauf, ↶ der Werkbank, Branch-Sperre) — keine eigene Sicherung.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Ein LLM-Aufruf: Anweisung (System) + Prompt. Liefert Text und – falls gemeldet – Token-Zahlen.</summary>
public interface ILlmAnbieter
{
    string Beschreibung { get; }
    string? Warnung { get; }
    Task<LlmAntwort> FrageAsync(string anweisung, string prompt, CancellationToken ct);
}

public sealed record LlmAntwort(string Text, int? Eingabe, int? AusCache, int? CacheGeschrieben, int? Ausgabe, double? KostenSchaetzungUsd, string? Roh);

/// <summary>
/// Claude Code im Print-Modus (<c>claude -p</c>) — läuft über die Anmeldung des Nutzers (Abo), NIE über einen API-Key.
/// Bewusst nur die Parameter, die jede CLI-Version kennt (<c>-p</c>, <c>--output-format json</c>, <c>--model</c>): die feste
/// Anweisung reist im Prompt mit (stdin), statt über neuere Schalter wie <c>--append-system-prompt</c>.
/// </summary>
public sealed class ClaudeCliAnbieter(string? modell) : ILlmAnbieter
{
    /// <summary>Die CLI: <c>BRACTOR_CLAUDE</c> (voller Pfad, z. B. eine neuere Installation) oder <c>claude</c> aus dem PATH.</summary>
    private static string Cli => Environment.GetEnvironmentVariable("BRACTOR_CLAUDE") is { Length: > 0 } c ? c : "claude";
    public string Beschreibung => $"{Cli} -p (Claude Code, Anmeldung dieses Rechners){(modell != null ? ", Modell " + modell : "")}";
    public string? Warnung => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is { Length: > 0 }
        ? "ANTHROPIC_API_KEY ist gesetzt — claude -p würde über die API abrechnen. Variable entfernen, dann die Werkbank neu starten."
        : null;

    public async Task<LlmAntwort> FrageAsync(string anweisung, string prompt, CancellationToken ct)
    {
        if (Warnung != null) throw new InvalidOperationException(Warnung);
        // Leeres Arbeitsverzeichnis: kein Projekt-CLAUDE.md, keine Repo-Dateien — nur der gelieferte Kontext.
        var leer = Directory.CreateTempSubdirectory("bractor-llm-");
        try
        {
            var psi = new ProcessStartInfo(Cli)
            {
                WorkingDirectory = leer.FullName, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            };
            // Kein API-Key darf durchsickern (auch nicht aus einer Eltern-Umgebung) — abgerechnet wird nur über das Abo.
            psi.Environment.Remove("ANTHROPIC_API_KEY");
            psi.Environment.Remove("ANTHROPIC_AUTH_TOKEN");
            // Läuft die Werkbank selbst in einer Claude-Code-Sitzung, erbt der Kindprozess deren Umleitung/Sitzungsdaten —
            //   weg damit: der Aufruf nutzt allein die eigene Anmeldung (claude.ai-Abo). Ein Abo-Token bleibt erlaubt.
            psi.Environment.Remove("ANTHROPIC_BASE_URL");
            psi.Environment.Remove("CLAUDECODE");
            foreach (var name in psi.Environment.Keys.Where(n => n.StartsWith("CLAUDE_CODE_", StringComparison.Ordinal) && n != "CLAUDE_CODE_OAUTH_TOKEN").ToList())
                psi.Environment.Remove(name);
            foreach (var a in new[] { "-p", "--output-format", "json" }) psi.ArgumentList.Add(a);
            if (modell != null) { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(modell); }
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"{Cli} konnte nicht gestartet werden (im PATH? sonst BRACTOR_CLAUDE setzen).");
            using var abbruch = ct.Register(() => { try { p.Kill(true); } catch { /* schon beendet */ } });
            await p.StandardInput.WriteAsync("# ANWEISUNG\n" + anweisung.Trim() + "\n\n" + prompt);
            p.StandardInput.Close();
            var aus = p.StandardOutput.ReadToEndAsync(ct);
            var err = p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            var json = (await aus).Trim();
            if (p.ExitCode != 0 && (json.Length == 0 || json[0] != '{'))
                throw new InvalidOperationException($"claude -p beendet mit {p.ExitCode}: {((await err).Trim() + " " + json).Trim()}");
            var o = JsonNode.Parse(json)!;
            if (o["is_error"] is JsonValue fe && fe.TryGetValue<bool>(out var istFehler) && istFehler)
                throw new InvalidOperationException("claude -p meldet einen Fehler: " + (o["result"]?.ToString() ?? o["subtype"]?.ToString() ?? json)
                    + (json.Contains("login", StringComparison.OrdinalIgnoreCase) ? $" — einmal im Terminal `{Cli}` starten und /login (Abo) ausführen." : ""));
            var u = o["usage"];
            // Kostenfeld je nach CLI-Version: total_cost_usd (neu) bzw. cost_usd (alt) — beim Abo nur eine Schätzung.
            var kosten = (o["total_cost_usd"] ?? o["cost_usd"]) is JsonValue kv && kv.TryGetValue<double>(out var k) ? k : (double?)null;
            return new LlmAntwort(o["result"]?.GetValue<string>() ?? "", Int(u?["input_tokens"]), Int(u?["cache_read_input_tokens"]),
                Int(u?["cache_creation_input_tokens"]), Int(u?["output_tokens"]), kosten, json);
        }
        finally { try { leer.Delete(true); } catch { /* egal */ } }
    }

    private static int? Int(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
}

public sealed class LlmKonsole
{
    /// <summary>Die feste Anweisung — für alle Aufrufe gleich (Teil des cachebaren Präfixes).</summary>
    public const string Anweisung = """
        Du schreibst den Rumpf genau EINER C#-Methode in einem bestehenden Projekt. Der Kontext besteht aus dem
        Graph-Skelett der Domäne und dem Slot-Teil dieses Code-Blocks; am Ende stehen Auftrag und ggf. der aktuelle Rumpf
        mit Befund oder Anpassung.
        Antworte in GENAU einer von zwei Formen, ohne weiteren Text:
        1) Ein einziger ```csharp-Block mit den Anweisungen des Methodenrumpfs — ohne Signatur, ohne die äußeren
           geschweiften Klammern der Methode, ohne using-Direktiven.
        2) Eine Zeile: AUSSERHALB: braucht <Art> <Name> — <Grund>
           wenn der Auftrag mit dem, was im Rumpf erreichbar ist, nicht lösbar ist (z. B. neuer Ausgang, neues Feld,
           neue Verdrahtung).
        Benutze nur Typen und Member, die im Kontext stehen.
        """;

    /// <summary>Die feste Anweisung für eine getragene Funktion bzw. eine Kenntnis eines Clients DRAUSSEN — der Python-Rumpf in der Client-Implementierung.</summary>
    public const string AnweisungPython = """
        Du schreibst den Rumpf genau EINER Python-Methode einer Client-Implementierung: so führt ein Client draußen den Eingang einer
        getragenen Funktion aus bzw. nimmt ein Event zur Kenntnis. Der Kontext besteht aus dem Graph-Skelett und dem Slot-Teil (Signatur
        aus der generierten Basis, Ausgabe-Vertrag, Typen, die übrigen Methoden der Klasse); am Ende stehen Auftrag und ggf. der aktuelle Rumpf.
        Antworte in GENAU einer von zwei Formen, ohne weiteren Text:
        1) Ein einziger ```python-Block mit den Anweisungen des Methodenrumpfs — ohne def-Zeile, ohne Einzug der Klasse.
        2) Eine Zeile: AUSSERHALB: braucht <Art> <Name> — <Grund>
           wenn der Auftrag mit dem, was im Rumpf erreichbar ist, nicht lösbar ist (z. B. ein neuer Ausgang im Vertrag).
        Eine getragene Funktion liefert ihre Ausgaben mit `return <Ausgabe>Dto(...)` bzw. als Strom mit `yield <Ausgabe>Dto(...)` — NUR die
        im Ausgabe-Vertrag genannten. Eine Kenntnis liefert nichts. Benutze nur Namen, die im Kontext stehen (Felder in snake_case wie
        im DTO, die Parameter der Signatur, `self`).
        """;

    private readonly string _sln, _verz;
    private readonly Werkbank.Server.Arbeitsbereich _arbeit;
    private readonly Werkbank.Server.GestenDienst? _gesten;
    private readonly Func<ILlmAnbieter> _anbieter;

    /// <param name="gesten">Der Schreibweg: eine Übernahme ist eine Rumpf-Geste (Verlauf, ↶, Branch-Sperre); ohne (Kommandozeile) wird nichts geschrieben.</param>
    public LlmKonsole(string slnRoot, Werkbank.Server.Arbeitsbereich arbeitsbereich, Werkbank.Server.GestenDienst? gesten = null)
    {
        _sln = slnRoot; _arbeit = arbeitsbereich; _gesten = gesten;
        _verz = Path.Combine(slnRoot, ".llm-kontext");
        // Ausschließlich Claude Code über die Anmeldung dieses Rechners (Abo, keine API-Kosten); BRACTOR_LLM_MODELL wählt optional das Modell.
        _anbieter = () => new ClaudeCliAnbieter(Environment.GetEnvironmentVariable("BRACTOR_LLM_MODELL"));
    }

    // ── Index + Dateien aus GraphExtractor --kontexte ──

    private JsonObject? Index() => File.Exists(Path.Combine(_verz, "index.json"))
        ? JsonNode.Parse(File.ReadAllText(Path.Combine(_verz, "index.json")))!.AsObject() : null;

    // Der Editor kennt je Store-Funktion nur „store|Interface|Methode“; mehrere Implementierungen tragen „@Klasse“ → die erste.
    private JsonObject? Slot(string id)
    {
        var slots = Index()?["slots"]?.AsArray().OfType<JsonObject>().ToList();
        return slots?.FirstOrDefault(s => s["id"]?.GetValue<string>() == id)
            ?? slots?.FirstOrDefault(s => s["id"]?.GetValue<string>().StartsWith(id + "@", StringComparison.Ordinal) == true);
    }

    // Getragene Funktion bzw. Kenntnis eines Clients draußen: der Python-Rumpf in der Client-Implementierung (eigener Anker: Datei + Klasse + Methode).
    //   Die Slot-Art heißt im Protokoll mit dem Editor „zusage“.
    private static bool IstPython(JsonObject slot) => S(slot, "art") == "zusage";
    private static PythonAnker PAnker(JsonObject slot) => new(S(slot, "datei"), S(slot, "klasse"), S(slot, "methode"), SN(slot, "signatur"));
    private object Lese(JsonObject slot) => IstPython(slot) ? PythonSync.Lese(PAnker(slot), _sln) : CodeSync.LeseRumpf(Anker(slot), _sln);
    private string? BasisHash(JsonObject slot) => IstPython(slot) ? PythonSync.RumpfHash(PAnker(slot), _sln) : CodeSync.RumpfHash(Anker(slot), _sln);

    private static string S(JsonNode? n, string p) => n?[p]?.GetValue<string>() ?? "";
    private static string? SN(JsonNode? n, string p) => n?[p] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private MethodenAnker Anker(JsonObject slot) => new(S(slot, "datei"), S(slot, "klasse"), S(slot, "methode"), SN(slot, "parameterTyp"));

    public object Status()
    {
        var a = _anbieter();
        var idx = Path.Combine(_verz, "index.json");
        return new
        {
            anbieter = a.Beschreibung, warnung = a.Warnung,
            aktualisierungLaeuft = AktualisierungLaeuft,
            index = File.Exists(idx) ? new { stand = File.GetLastWriteTime(idx).ToString("yyyy-MM-dd HH:mm:ss"), slots = Index()!["slots"]!.AsArray().Count,
                tokenSkelett = Index()!["tokenSkelett"]?.GetValue<int>() } : null,
        };
    }

    public object Slots() => Index()?["slots"]?.AsArray().OfType<JsonObject>().Select(s => new
    {
        id = S(s, "id"), titel = S(s, "titel"), art = S(s, "art"), rumpf = S(s, "rumpf"), tokenSlot = s["tokenSlot"]?.GetValue<int>(),
        datei = S(s, "datei"), zeile = s["zeile"]?.GetValue<int>(), auftrag = SN(s, "auftrag"),
    }).ToList() ?? (object)new List<object>();

    public object SlotDetail(string id)
    {
        var slot = Slot(id);
        if (slot == null) return new { ok = false, grund = "Unbekannter Slot — Kontexte neu erzeugen?" };
        var teil = File.ReadAllText(Path.Combine(_verz, S(slot, "slotDatei")));
        return new
        {
            ok = true, slot, slotTeil = teil, aktuell = Lese(slot),
            tokenGesamt = (Index()!["tokenSkelett"]?.GetValue<int>() ?? 0) + (slot["tokenSlot"]?.GetValue<int>() ?? 0),
        };
    }

    // ── Prompt: stabil → veränderlich ──

    private string Prompt(JsonObject slot, string auftrag, string? rumpf, string? befund, string? anpassung)
    {
        var skelett = File.ReadAllText(Path.Combine(_verz, S(slot, "skelettDatei").Length > 0 ? S(slot, "skelettDatei") : "00-graph-skelett.txt"));
        // Der Auftrags-Abschnitt aus der Datei fliegt raus: der Auftrag steht bewusst am ENDE (veränderlicher Teil).
        var teil = Regex.Replace(File.ReadAllText(Path.Combine(_verz, S(slot, "slotDatei"))), @"## AUFTRAG \[L\]\n.*?\n(?=\n## )", "", RegexOptions.Singleline);
        var b = new StringBuilder();
        b.AppendLine(skelett).AppendLine().AppendLine(teil.Trim()).AppendLine();
        b.AppendLine("## AUFTRAG").AppendLine(auftrag.Trim());
        if (rumpf != null) b.AppendLine().AppendLine("## AKTUELLER RUMPF").AppendLine(IstPython(slot) ? "```python" : "```csharp").AppendLine(rumpf.Trim()).AppendLine("```");
        if (befund != null) b.AppendLine().AppendLine("## BEFUND (Prüfung des aktuellen Rumpfs) — korrigiere den Rumpf").AppendLine(befund.Trim());
        if (anpassung != null) b.AppendLine().AppendLine("## ANPASSUNG (Wunsch des Entwicklers) — ändere den aktuellen Rumpf").AppendLine(anpassung.Trim());
        return b.ToString();
    }

    // ── Füllen: bis zu N zustandslose Runden, jede geprüft ──

    public sealed record FuellAnfrage(string Id, string Auftrag, string? Rumpf, string? Anpassung, bool AutoReparatur = true, int MaxRunden = 3);

    public async Task<object> FuellenAsync(FuellAnfrage a, CancellationToken ct)
    {
        var slot = Slot(a.Id);
        if (slot == null) return new { ok = false, grund = "Unbekannter Slot — Kontexte neu erzeugen?" };
        if (string.IsNullOrWhiteSpace(a.Auftrag)) return new { ok = false, grund = "Kein Auftrag — ohne Auftrag wird kein Aufruf ausgelöst." };
        var anbieter = _anbieter();
        var basisHash = BasisHash(slot);
        var runden = new List<object>();
        // Der aktuelle Rumpf (Anpassung) ohne „// 🤖 Prompt:“-Zeile — die Aufträge stehen getrennt im Prompt.
        string? rumpf = a.Rumpf is null ? null : string.Join("\n", a.Rumpf.Replace("\r\n", "\n").Split('\n')
                .Where(z => !z.TrimStart().StartsWith("// 🤖 Prompt:", StringComparison.Ordinal))),
            befund = null, anpassung = a.Anpassung;
        for (var nr = 1; nr <= Math.Max(1, a.MaxRunden); nr++)
        {
            var prompt = Prompt(slot, a.Auftrag, rumpf, befund, anpassung);
            var art = anpassung != null ? "anpassung" : befund != null ? "reparatur" : "erzeugen";
            var uhr = Stopwatch.StartNew();
            LlmAntwort antwort;
            try { antwort = await anbieter.FrageAsync(IstPython(slot) ? AnweisungPython : Anweisung, prompt, ct); }
            catch (Exception ex) { runden.Add(new { nr, art, fehler = ex.Message + (ex.InnerException != null ? " — " + ex.InnerException.Message : "") }); break; }
            uhr.Stop();
            var (ergebnis, kandidat, ausserhalb) = IstPython(slot) ? ZerlegePython(antwort.Text) : Zerlege(antwort.Text);
            var befunde = kandidat != null ? await PruefeAsync(slot, kandidat, ct) : new List<string>();
            var runde = new
            {
                nr, art, dauerMs = uhr.ElapsedMilliseconds, promptZeichen = prompt.Length, promptTokenSchaetzung = (int)Math.Ceiling(prompt.Length / 3.3),
                token = new { eingabe = antwort.Eingabe, ausCache = antwort.AusCache, cacheGeschrieben = antwort.CacheGeschrieben, ausgabe = antwort.Ausgabe, kostenSchaetzungUsd = antwort.KostenSchaetzungUsd },
                ergebnis, rumpf = kandidat, ausserhalb, befunde, ok = ergebnis == "rumpf" && befunde.Count == 0,
                antwortRoh = kandidat == null && ausserhalb == null ? antwort.Text : null,
            };
            runden.Add(runde);
            Protokolliere(a.Id, runde, a.Auftrag);
            if (ergebnis != "rumpf" || befunde.Count == 0 || !a.AutoReparatur) break;
            rumpf = kandidat; befund = string.Join("\n", befunde); anpassung = null;   // nächste Runde: nur letzter Rumpf + Befund
        }
        return new { ok = true, anbieter = anbieter.Beschreibung, basisHash, runden };
    }

    /// <summary>Antwort → Rumpf | AUSSERHALB | unlesbar. Ein Codeblock wird herausgelöst; äußere Methodenklammern entfernt.</summary>
    private static (string Ergebnis, string? Rumpf, string? Ausserhalb) Zerlege(string text)
    {
        var t = text.Trim();
        var aus = Regex.Match(t, @"^AUSSERHALB:.*$", RegexOptions.Multiline);
        var block = Regex.Match(t, @"```(?:csharp|cs|c#)?\s*\n(.*?)```", RegexOptions.Singleline);
        if (!block.Success && aus.Success) return ("ausserhalb", null, aus.Value.Trim());
        var code = block.Success ? block.Groups[1].Value : t;
        code = code.Replace("\r\n", "\n").Trim('\n');
        if (code.TrimStart().StartsWith('{') && code.TrimEnd().EndsWith('}')
            && SyntaxFactory.ParseStatement(code.Trim()) is BlockSyntax bs && bs.Span.Length == code.Trim().Length && !bs.GetDiagnostics().Any())
            code = string.Join("\n", bs.Statements.Select(x => x.ToFullString())).Trim('\n');
        if (code.Trim().Length == 0) return ("unlesbar", null, null);
        return ("rumpf", Dedent(code), null);
    }

    /// <summary>Antwort → Python-Rumpf | AUSSERHALB | unlesbar (ein Codeblock, dedentet).</summary>
    private static (string Ergebnis, string? Rumpf, string? Ausserhalb) ZerlegePython(string text)
    {
        var t = text.Trim();
        var aus = Regex.Match(t, @"^AUSSERHALB:.*$", RegexOptions.Multiline);
        var block = Regex.Match(t, @"```(?:python|py)?\s*\n(.*?)```", RegexOptions.Singleline);
        if (!block.Success && aus.Success) return ("ausserhalb", null, aus.Value.Trim());
        var code = PythonQuelle.Dedent(block.Success ? block.Groups[1].Value : t);
        return code.Trim().Length == 0 ? ("unlesbar", null, null) : ("rumpf", code, null);
    }

    /// <summary>Python-Rumpf prüfen: der ganze Dateitext MIT dem Kandidaten durch Pythons Parser; eine Kenntnis (ohne Ausgabe) darf nichts liefern,
    /// eine getragene Funktion nur die Ausgaben ihrer Signatur.</summary>
    private List<string> PruefePython(JsonObject slot, string rumpf)
    {
        var (ok, grund, text) = PythonSync.MitRumpf(PAnker(slot), rumpf, null, _sln);
        if (!ok) return [grund];
        var befunde = PythonSync.Pruefe(text!);
        var ausg = slot["ausgaenge"]?.AsArray().Select(x => x!.GetValue<string>()).ToList() ?? [];
        if (ausg.Count == 0 && Regex.IsMatch(rumpf, @"^\s*yield\b", RegexOptions.Multiline)) befunde.Add("Kenntnis (ohne Ausgabe-Vertrag) darf nichts liefern — kein yield.");
        foreach (Match y in Regex.Matches(rumpf, @"(yield|return)\s+(\w+?)Dto\s*\("))
            if (!ausg.Contains(y.Groups[2].Value)) befunde.Add($"{y.Groups[1].Value} {y.Groups[2].Value} — nicht im Ausgabe-Vertrag ({(ausg.Count == 0 ? "leer" : string.Join(", ", ausg))}).");
        return befunde;
    }

    /// <summary>
    /// Der Dateitext mit dem Kandidaten am Ort des Slots, ohne zu schreiben — dieselbe Einsetzung wie die Rumpf-Geste
    /// (<see cref="GraphExtractor.Rumpf.MitRumpf"/>), damit geprüft wird, was später in der Datei steht.
    /// </summary>
    private (bool Ok, string Grund, string? Pfad, string? Text) MitKandidat(JsonObject slot, string rumpf)
    {
        var (ort, grund, _) = CodeSync.Ort(Anker(slot), _sln);
        if (ort is null) return (false, grund, null, null);
        var pfad = Path.GetFullPath(Path.Combine(_sln, ort.Datei));
        try { return (true, "", pfad, GraphExtractor.Rumpf.MitRumpf(File.ReadAllText(pfad), ort, rumpf, null)); }
        catch (InvalidOperationException e) { return (false, e.Message, null, null); }
    }

    // ── Prüfen: Syntax, dann probeweise bauen (warmer Workspace) — für jede C#-Methode am Slot; Python über den Parser ──

    public async Task<List<string>> PruefeAsync(JsonObject slot, string rumpf, CancellationToken ct = default)
    {
        if (IstPython(slot)) return PruefePython(slot, rumpf);
        var befunde = SyntaxFactory.ParseStatement("{\n" + rumpf + "\n}").GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"Syntax {d.Id} Zeile {d.Location.GetLineSpan().StartLinePosition.Line}: {d.GetMessage()}").ToList();
        if (befunde.Count > 0) return befunde;
        var (ok, grund, pfad, text) = MitKandidat(slot, rumpf);
        return ok ? (await _arbeit.PruefeProbeweiseAsync(pfad!, text!, ct)).ToList() : new List<string> { grund };
    }

    /// <summary>Prüfen per Slot-Id (Kommandozeile <c>--pruefe-rumpf</c>, Endpunkt <c>/api/llm/pruefen</c>) — schreibt nichts.</summary>
    public async Task<object> PruefePerIdAsync(string id, string rumpf, CancellationToken ct = default)
    {
        var slot = Slot(id);
        if (slot == null) return new { ok = false, grund = "Unbekannter Slot — Kontexte neu erzeugen." };
        var befunde = await PruefeAsync(slot, rumpf, ct);
        return new { ok = befunde.Count == 0, befunde };
    }

    // ── Ausführen: der EINE Durchlauf am 🤖-Knoten — füllen → prüfen → in die Datei → Generatoren ──

    /// <summary>
    /// Füllt den Slot (bis zu N geprüfte Runden) und schreibt einen GEPRÜFTEN Rumpf sofort in die echte Datei; danach laufen
    /// die Code-Generatoren (Build des Laufzeit-Projekts). Besteht der letzte Kandidat die Prüfung nicht, wird NICHT
    /// geschrieben — er kommt als Vorschlag zurück (manuell übernehmbar).
    /// </summary>
    public async Task<object> AusfuehrenAsync(FuellAnfrage a, CancellationToken ct)
    {
        var slot = Slot(a.Id);
        if (slot == null) return new { ok = false, unbekannt = true, grund = "Unbekannter Slot — der Block hat noch keine Methode in einer .cs." };
        var fuell = await FuellenAsync(a, ct);
        var f = JsonSerializer.SerializeToNode(fuell)!;
        if (f["ok"]?.GetValue<bool>() != true) return fuell;
        var ende = f["runden"]!.AsArray().LastOrDefault();
        var geprueft = ende?["ok"]?.GetValue<bool>() == true;
        var rumpf = ende?["rumpf"]?.GetValue<string>();
        if (!geprueft || rumpf == null)
            return new { ok = true, geschrieben = false, basisHash = f["basisHash"]?.GetValue<string>(), runden = f["runden"] };
        var ueb = JsonSerializer.SerializeToNode(await UebernehmenAsync(a.Id, rumpf, a.Auftrag, f["basisHash"]?.GetValue<string>(), bauen: true, ct))!;
        return new
        {
            ok = ueb["ok"]?.GetValue<bool>() == true, geschrieben = ueb["ok"]?.GetValue<bool>() == true,
            grund = ueb["grund"]?.GetValue<string>(), datei = ueb["datei"]?.GetValue<string>(), zeile = ueb["zeile"]?.GetValue<int>(),
            bau = ueb["bau"], runden = f["runden"],
        };
    }

    // ── Übernehmen / Bauen ──

    /// <summary>
    /// Einen Rumpf übernehmen = eine Rumpf-Geste (derselbe Weg wie ✎, CLAUDE.md W1–W12): C# über den Ort des Slots
    /// (<see cref="GraphExtractor.Rumpf"/>, mit Prompt-Zeile), Python über <see cref="PythonSync"/>; der Patch steht im Verlauf der Werkbank,
    /// ↶ nimmt ihn zurück, auf <c>main</c> ist er gesperrt. <paramref name="basisHash"/> sperrt gegen eine seit dem Füllen geänderte Datei.
    /// </summary>
    /// <param name="bauen">danach das Laufzeit-Projekt bauen — der echte Build, die Code-Generatoren laufen mit.</param>
    public async Task<object> UebernehmenAsync(string id, string rumpf, string? auftrag, string? basisHash, bool bauen = false, CancellationToken ct = default)
    {
        var slot = Slot(id);
        if (slot == null) return new { ok = false, grund = "Unbekannter Slot." };
        if (_gesten is null) return new { ok = false, grund = "Übernehmen nur im Werkbank-Server (Geste mit Verlauf)." };
        if (basisHash != null && BasisHash(slot) != basisHash)
            return new { ok = false, grund = "Die Datei wurde seit dem Füllen geändert (Hash passt nicht) — neu laden." };
        var titel = $"🤖 {id}";
        Werkbank.Server.GestenAntwort antwort;
        string datei;
        int zeile;
        if (IstPython(slot))
        {
            var anker = PAnker(slot);
            antwort = await _gesten.PythonRumpfAsync(titel, anker, rumpf, null, ct, prompt: auftrag);
            (datei, zeile) = (anker.Datei, PythonSync.Zeile(anker, _sln));
        }
        else
        {
            var (ort, grund, z) = CodeSync.Ort(Anker(slot), _sln);
            if (ort is null) return new { ok = false, grund };
            // Die Prompt-Zeile reist im Text; die Einsetzung setzt genau eine.
            var text = !string.IsNullOrWhiteSpace(auftrag)
                ? $"{GraphExtractor.Rumpf.PromptMarke} {auftrag.Replace("\r", " ").Replace("\n", " ").Trim()}\n{rumpf}" : rumpf;
            antwort = await _gesten.RumpfAsync(titel, ort, text, null, ct);
            (datei, zeile) = (ort.Datei, z);
        }
        if (!antwort.Ok) return new { ok = false, grund = antwort.Fehler };
        Protokolliere(id, new { aktion = "uebernommen", datei, zeile });
        return new { ok = true, datei, zeile, bau = bauen && !IstPython(slot) ? Bauen(id) : null,
            hinweis = "Kontexte sind jetzt veraltet — neu erzeugen, bevor der nächste Slot gefüllt wird." };
    }

    /// <summary>
    /// Die Code-Generatoren laufen lassen: das LAUFZEIT-Projekt bauen (vom Extractor abgeleitet: das Projekt mit der generierten
    /// Routing-Tabelle). Dessen Build zieht die ganze Kette — Domain-Generatoren, Codegen-Prepass (Proto/STJ), Projektions-DI,
    /// Infrastructure-Generatoren. Fehlt die Angabe (alte Kontexte), wenigstens das Projekt der Datei.
    /// </summary>
    public object Bauen(string id)
    {
        var laufzeit = Index()?["laufzeitProjekt"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(laufzeit)) return CodeSync.BaueProjekt(laufzeit, _sln);
        return Slot(id) is { } slot ? CodeSync.BaueProjektVon(S(slot, "datei"), _sln) : new { ok = false, grund = "Unbekannter Slot." };
    }

    /// <summary>GraphExtractor über den aktuellen Code: domain-model.json + alle Kontexte + index.json neu.</summary>
    private readonly SemaphoreSlim _aktualisierung = new(1, 1);
    public bool AktualisierungLaeuft => _aktualisierung.CurrentCount == 0;

    /// <summary>Beim Start: fehlen die Kontexte, erzeugt die Werkbank sie im Hintergrund (die GUI zeigt „wird erzeugt“).</summary>
    public void ErzeugeKontexteFallsFehlend()
    {
        if (!File.Exists(Path.Combine(_verz, "index.json"))) _ = Task.Run(Aktualisieren);
    }

    public object Aktualisieren()
    {
        _aktualisierung.Wait();   // nie zwei GraphExtractor-Läufe gleichzeitig (beide schreiben dieselben Dateien)
        try { return AktualisierenIntern(); }
        finally { _aktualisierung.Release(); }
    }

    private object AktualisierenIntern()
    {
        // Derselbe Prozess-Weg wie ⟳ (wartet auf das Prozessende, nicht auf die von MSBuild-Knoten geerbte Pipe).
        var r = Werkbank.Server.Prozesse.LaufeAsync("dotnet", ["run", "--project", "GraphExtractor", "--", "--kontexte", ".llm-kontext"], _sln,
            zeitlimit: TimeSpan.FromMinutes(5)).GetAwaiter().GetResult();
        if (r.Code == -1) return new { ok = false, grund = "Zeitüberschreitung (5 min)." };
        var meldung = r.Zeilen.Where(l => l.Contains('✅') || l.Contains('❌') || l.Contains("error")).Select(l => l.Trim()).Take(10).ToList();
        return new { ok = r.Ok, meldung, grund = r.Ok ? null : string.Join(" · ", meldung.Take(3)) };
    }

    private void Protokolliere(string id, object eintrag, string? auftrag = null)
    {
        Directory.CreateDirectory(_verz);
        File.AppendAllText(Path.Combine(_verz, "protokoll.jsonl"),
            JsonSerializer.Serialize(new { zeit = DateTime.Now.ToString("s"), id, auftrag, eintrag }) + "\n");
    }

    /// <summary>
    /// Das Protokoll für den Browser: neueste zuerst (höchstens <paramref name="max"/>), optional nur ein Slot, plus Summen
    /// über alle Runden (Aufrufe, Token gesamt/aus Cache/Ausgabe, Dauer). Kaputte Zeilen werden übersprungen.
    /// </summary>
    public object Protokoll(string? id, int max = 300)
    {
        var datei = Path.Combine(_verz, "protokoll.jsonl");
        var einträge = new List<JsonNode>();
        if (File.Exists(datei))
            foreach (var zeile in File.ReadLines(datei))
            {
                if (string.IsNullOrWhiteSpace(zeile)) continue;
                try { if (JsonNode.Parse(zeile) is { } n && (id == null || S(n, "id") == id)) einträge.Add(n); } catch { /* Zeile überspringen */ }
            }
        long L(JsonNode? n) => n is JsonValue v && v.TryGetValue<long>(out var x) ? x : 0;
        var runden = einträge.Where(e => e["eintrag"]?["nr"] != null).ToList();
        var summe = new
        {
            aufrufe = runden.Count,
            ok = runden.Count(e => e["eintrag"]?["ok"] is JsonValue v && v.TryGetValue<bool>(out var b) && b),
            eingabe = runden.Sum(e => L(e["eintrag"]?["token"]?["eingabe"])),
            ausCache = runden.Sum(e => L(e["eintrag"]?["token"]?["ausCache"])),
            cacheGeschrieben = runden.Sum(e => L(e["eintrag"]?["token"]?["cacheGeschrieben"])),
            ausgabe = runden.Sum(e => L(e["eintrag"]?["token"]?["ausgabe"])),
            promptSchaetzung = runden.Sum(e => L(e["eintrag"]?["promptTokenSchaetzung"])),
            dauerMs = runden.Sum(e => L(e["eintrag"]?["dauerMs"])),
            uebernommen = einträge.Count(e => S(e["eintrag"], "aktion") == "uebernommen"),
        };
        einträge.Reverse();
        return new { summe, eintraege = einträge.Take(max).ToList() };
    }

    private static string Dedent(string s)
    {
        var zeilen = s.Replace("\r\n", "\n").Split('\n');
        var min = zeilen.Where(z => z.Trim().Length > 0).Select(z => z.Length - z.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select(z => (z.Length >= min ? z[min..] : z.TrimStart()).TrimEnd())).Trim('\n');
    }
}
