using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimHost;

// ════════════════════════════════════════════════════════════════════════════
//  LLM-Konsole (docs/konzept-llm-minimalkontext.md): einen Code-Block füllen, prüfen, anpassen, übernehmen.
//
//  • Kontext = die Dateien aus `GraphExtractor --kontexte .llm-kontext` (Graph-Skelett + Slot-Teil je Code-Block).
//  • Jede Runde ist ein NEUER, zustandsloser Aufruf. Reihenfolge stabil → veränderlich:
//      Anweisung · Graph-Skelett · Slot-Teil · Auftrag · [aktueller Rumpf · Befund/Anpassung]
//    Nie ein Gesprächsverlauf — nur der letzte Rumpf reist mit.
//  • Prüfen: Syntax (alle Slot-Arten); Decide/Apply zusätzlich In-Memory-Compile mit den echten Generatoren
//    (dasselbe Kompilat wie /api/editor/compile); Store-Funktionen gegen das echte Projekt inkl. der Store-Regeln
//    CQRS066/067 (ProjektPruefung). Nur NEUE Fehler gegenüber dem Unveränderten zählen.
//  • Ausführen (Editor, ▶ am 🤖-Knoten): ein GEPRÜFTER Rumpf geht sofort in die echte Datei, danach laufen die Generatoren
//    (Build des Laufzeit-Projekts). Übernehmen (Konsole bzw. ungeprüfter Vorschlag) schreibt auf Klick. Beides mit
//    Hash-Sperre, Sicherung, Rückgängig.
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
        ? "ANTHROPIC_API_KEY ist gesetzt — claude -p würde über die API abrechnen. Variable entfernen, dann SimHost neu starten."
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
            // Läuft der SimHost selbst in einer Claude-Code-Sitzung, erbt der Kindprozess deren Umleitung/Sitzungsdaten —
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

    /// <summary>Die feste Anweisung für einen Fluss-Lambda (docs/konzept-editor-pipelines.md §14.4).</summary>
    public const string AnweisungFluss = """
        Du schreibst GENAU EINEN C#-Lambda-Ausdruck eines Pipeline-Flusses: er baut aus den Nachrichten am Draht (seinen Parametern)
        den Aufruf des Knotens (einen Auftrag bzw. ein Command). Der Kontext besteht aus dem Graph-Skelett und dem Slot-Teil; am Ende
        stehen Auftrag und ggf. der aktuelle Lambda mit Befund oder Anpassung.
        Antworte in GENAU einer von zwei Formen, ohne weiteren Text:
        1) Ein einziger ```csharp-Block mit NUR dem Lambda-Ausdruck — die Parameterliste genau wie in der SIGNATUR, kein Semikolon.
        2) Eine Zeile: AUSSERHALB: braucht <Art> <Name> — <Grund>
           wenn der Auftrag mit den Parametern nicht lösbar ist (z. B. ein weiterer Draht, ein neues Feld).
        Der Lambda ist rein und deterministisch: kein Guid.NewGuid(), keine Uhr, kein I/O. Bevorzuge die Zuordnungsform
        (param.Feld, Konstanten, new Wertobjekt(…)). Benutze nur Typen und Member, die im Kontext stehen.
        """;

    private readonly string _sln, _verz;
    private readonly ModellSimulation _sim;
    private readonly Func<ILlmAnbieter> _anbieter;
    private readonly object _sperre = new();
    private readonly Dictionary<string, CompileErgebnis> _basisFehler = new();
    private readonly ProjektPruefung _projekt;

    public LlmKonsole(string slnRoot, ModellSimulation sim)
    {
        _sln = slnRoot; _sim = sim;
        _projekt = new ProjektPruefung(slnRoot);
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

    /// <summary>Datei-Spiegel eines Slots (jede Art): aktueller Rumpf, Prompt, Hash.</summary>
    public object Rumpf(string id) => Slot(id) is { } slot ? Lese(slot) : new { ok = false, grund = "Unbekannter Slot." };

    // ── Fluss-Lambda: eigener Anker (Klasse + Anweisung + Lambda), der „Rumpf“ ist der ganze Lambda-Ausdruck ──
    private static bool IstFluss(JsonObject slot) => S(slot, "art") == "fluss";
    private static LambdaAnker LAnker(JsonObject slot) =>
        new(S(slot, "datei"), S(slot, "klasse"), slot["anweisung"]?.GetValue<int>() ?? -1, slot["lambda"]?.GetValue<int>() ?? -1);
    private object Lese(JsonObject slot) => IstFluss(slot) ? CodeSync.LeseLambda(LAnker(slot), _sln) : CodeSync.LeseRumpf(Anker(slot), _sln);
    private string? BasisHash(JsonObject slot) => IstFluss(slot) ? CodeSync.LambdaHash(LAnker(slot), _sln) : CodeSync.RumpfHash(Anker(slot), _sln);

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
        var modell = SimModell();
        var commands = S(slot, "art") is "decide" or "apply" && modell != null
            ? modell.Decider.Where(d => d.Aggregat == S(slot, "aggregat")).Select(d => new
            {
                name = d.Command,
                felder = modell.Records.FirstOrDefault(r => r.Name == d.Command)?.Felder.Select(f => new { name = f.Name, typ = f.Typ }).ToList(),
            }).ToList()
            : null;
        return new
        {
            ok = true, slot, slotTeil = teil, aktuell = Lese(slot),
            tokenGesamt = (Index()!["tokenSkelett"]?.GetValue<int>() ?? 0) + (slot["tokenSlot"]?.GetValue<int>() ?? 0),
            simulierbar = commands != null, commands,
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
        if (rumpf != null) b.AppendLine().AppendLine("## AKTUELLER RUMPF").AppendLine("```csharp").AppendLine(rumpf.Trim()).AppendLine("```");
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
            try { antwort = await anbieter.FrageAsync(IstFluss(slot) ? AnweisungFluss : Anweisung, prompt, ct); }
            catch (Exception ex) { runden.Add(new { nr, art, fehler = ex.Message + (ex.InnerException != null ? " — " + ex.InnerException.Message : "") }); break; }
            uhr.Stop();
            var (ergebnis, kandidat, ausserhalb) = IstFluss(slot) ? ZerlegeLambda(antwort.Text) : Zerlege(antwort.Text);
            var befunde = kandidat != null ? Pruefe(slot, kandidat) : new List<string>();
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

    /// <summary>Antwort → Lambda | AUSSERHALB | unlesbar (ein Codeblock mit genau einem Lambda-Ausdruck).</summary>
    private static (string Ergebnis, string? Rumpf, string? Ausserhalb) ZerlegeLambda(string text)
    {
        var t = text.Trim();
        var aus = Regex.Match(t, @"^AUSSERHALB:.*$", RegexOptions.Multiline);
        var block = Regex.Match(t, @"```(?:csharp|cs|c#)?\s*\n(.*?)```", RegexOptions.Singleline);
        if (!block.Success && aus.Success) return ("ausserhalb", null, aus.Value.Trim());
        var code = (block.Success ? block.Groups[1].Value : t).Replace("\r\n", "\n").Trim().TrimEnd(';').Trim();
        return code.Length == 0 ? ("unlesbar", null, null) : ("rumpf", code, null);
    }

    /// <summary>
    /// Fluss-Lambda prüfen: ein Lambda-Ausdruck mit derselben Stelligkeit; dann das Modell mit dem Kandidaten (als Bau-Ausdruck des
    /// Eingangs bzw. Liste des Je) in-memory übersetzt — dieselbe Übersetzung wie die Simulation (echte Generatoren).
    /// </summary>
    private List<string> PruefeLambda(JsonObject slot, string ausdruck)
    {
        if (SyntaxFactory.ParseExpression(ausdruck) is not LambdaExpressionSyntax neu || neu.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            return new List<string> { "Kein einzelner Lambda-Ausdruck (Syntax)." };
        var aktuell = JsonSerializer.SerializeToNode(CodeSync.LeseLambda(LAnker(slot), _sln))?["body"]?.GetValue<string>();
        if (aktuell != null && SyntaxFactory.ParseExpression(aktuell) is LambdaExpressionSyntax alt && Stelligkeit(alt) != Stelligkeit(neu))
            return new List<string> { $"Der Lambda hat {Stelligkeit(neu)} Parameter, der Knoten liefert {Stelligkeit(alt)} (Drähte)." };
        var modell = SimModell();
        if (modell == null) return new List<string> { "domain-model.json fehlt — Kontexte neu erzeugen." };
        var mit = MitLambda(modell, slot, ausdruck);
        if (mit is null) return new List<string> { $"Fluss {S(slot, "klasse")}.{S(slot, "knoten")} steht nicht im Modell — Kontexte neu erzeugen." };
        var basis = BasisKompilat(modell);
        var bekannt = basis.Fehler.Select(f => f.Code + "|" + f.Meldung).ToHashSet();
        return _sim.Kompiliere(mit).Fehler.Where(f => f.Schweregrad == "error" && !bekannt.Contains(f.Code + "|" + f.Meldung))
            .Select(f => $"{f.Code}: {f.Meldung}").Distinct().Take(20).ToList();
    }

    private static int Stelligkeit(LambdaExpressionSyntax l) => l switch
    {
        SimpleLambdaExpressionSyntax => 1,
        ParenthesizedLambdaExpressionSyntax p => p.ParameterList.Parameters.Count,
        _ => -1,
    };

    /// <summary>Das Modell mit dem Kandidaten im passenden Knoten: Lambda i = Eingang i (Haupt-Aufruf, dann je ∨), beim Je die Liste.</summary>
    private static EditorModell? MitLambda(EditorModell m, JsonObject slot, string ausdruck)
    {
        var fluss = m.Fluesse.FirstOrDefault(f => f.Name == S(slot, "klasse"));
        var knoten = fluss?.Knoten.FirstOrDefault(k => k.Name == S(slot, "knoten"));
        var i = slot["lambda"]?.GetValue<int>() ?? -1;
        if (fluss == null || knoten == null || i < 0) return null;
        FlussSchritt neu;
        if (knoten.Art == FlussArt.Je && i == 0) neu = knoten with { Liste = ausdruck };
        else if (i < knoten.Eingaenge.Count)
            neu = knoten with { Eingaenge = knoten.Eingaenge.Select((e, j) => j == i ? e with { Ausdruck = ausdruck } : e).ToList() };
        else return null;
        return m with { Fluesse = m.Fluesse.Select(f => f != fluss ? f : f with { Knoten = f.Knoten.Select(k => k == knoten ? neu : k).ToList() }).ToList() };
    }

    // ── Prüfen: Syntax für alle; Decide/Apply In-Memory-Compile mit den echten Generatoren; Store gegen das echte Projekt ──

    public List<string> Pruefe(JsonObject slot, string rumpf)
    {
        if (IstFluss(slot)) return PruefeLambda(slot, rumpf);
        var befunde = SyntaxFactory.ParseStatement("{\n" + rumpf + "\n}").GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"Syntax {d.Id} Zeile {d.Location.GetLineSpan().StartLinePosition.Line}: {d.GetMessage()}").ToList();
        if (befunde.Count > 0) return befunde;
        var art = S(slot, "art");
        // Store: das Editor-Modell trägt keine Store-Rümpfe → gegen das echte Projekt, mit den Store-Regeln CQRS066/067 —
        // so sieht die Korrekturrunde einen Schreibweg am Puffer vorbei, BEVOR er in die Datei geht.
        if (art == "store") return _projekt.Pruefe(Anker(slot), rumpf);
        if (art is not ("decide" or "apply")) return befunde;   // Leseseite/Pipeline: Compile erst beim Bauen nach dem Übernehmen
        var modell = SimModell();
        if (modell == null) return new List<string> { "domain-model.json fehlt — Kontexte neu erzeugen." };
        var basis = BasisKompilat(modell);
        var mit = MitRumpf(modell, slot, rumpf);
        if (mit is null) return new List<string> { $"{S(slot, "art")} {S(slot, "aggregat")}.{S(slot, "disc")} steht nicht im Modell — Kontexte neu erzeugen." };
        var neu = _sim.Kompiliere(mit);
        var bekannt = basis.Fehler.Select(f => f.Code + "|" + f.Meldung).ToHashSet();
        return neu.Fehler.Where(f => f.Schweregrad == "error" && !bekannt.Contains(f.Code + "|" + f.Meldung))
            .Select(f => $"{f.Code}: {f.Meldung}").Distinct().Take(20).ToList();
    }

    /// <summary>Prüfen per Slot-Id (Kommandozeile <c>--pruefe-rumpf</c>, Endpunkt <c>/api/llm/pruefen</c>) — schreibt nichts.</summary>
    public object PruefePerId(string id, string rumpf)
    {
        var slot = Slot(id);
        if (slot == null) return new { ok = false, grund = "Unbekannter Slot — Kontexte neu erzeugen." };
        var befunde = Pruefe(slot, rumpf);
        return new { ok = befunde.Count == 0, befunde };
    }

    private CompileErgebnis BasisKompilat(EditorModell modell)
    {
        var schluessel = modell.AlsJson().GetHashCode().ToString();
        lock (_sperre)
            if (_basisFehler.TryGetValue(schluessel, out var e)) return e;
        var erg = _sim.Kompiliere(modell);
        lock (_sperre) _basisFehler[schluessel] = erg;
        return erg;
    }

    private EditorModell? SimModell()
    {
        var p = Path.Combine(_sln, "domain-model.json");
        return File.Exists(p) ? EditorModell.AusJson(File.ReadAllText(p)) : null;
    }

    /// <summary>Das Modell mit dem Kandidaten-Rumpf im passenden Decider/Applier — null, wenn der Slot nicht im Modell steht
    /// (sonst würde still das unveränderte Modell geprüft und der Kandidat fälschlich als „geprüft“ gelten).</summary>
    private static EditorModell? MitRumpf(EditorModell m, JsonObject slot, string rumpf)
    {
        var agg = S(slot, "aggregat"); var disc = S(slot, "disc");
        if (S(slot, "art") == "decide")
            return m.Decider.Any(d => d.Aggregat == agg && d.Command == disc)
                ? m with { Decider = m.Decider.Select(d => d.Aggregat == agg && d.Command == disc ? d with { Rumpf = rumpf } : d).ToList() }
                : null;
        return m.Applier.Any(x => x.Aggregat == agg && x.Event == disc)
            ? m with { Applier = m.Applier.Select(x => x.Aggregat == agg && x.Event == disc ? x with { Rumpf = rumpf } : x).ToList() }
            : null;
    }

    // ── Simulation mit dem Kandidaten (Decide/Apply): dieselbe Laufzeit wie im Editor ──

    public object Simuliere(string id, string rumpf, string command, JsonElement werte, bool neu)
    {
        var slot = Slot(id);
        if (slot == null || S(slot, "art") is not ("decide" or "apply")) return new { ok = false, grund = "Simulation nur für Decide/Apply." };
        var modell = SimModell();
        if (modell == null) return new { ok = false, grund = "domain-model.json fehlt." };
        var session = "konsole:" + id;
        if (neu) _sim.Reset(session);
        var mit = MitRumpf(modell, slot, rumpf);
        if (mit is null) return new { ok = false, grund = "Slot steht nicht im Modell — Kontexte neu erzeugen." };
        return _sim.Schritt(mit, session, command, werte);
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
        var ueb = JsonSerializer.SerializeToNode(Uebernehmen(a.Id, rumpf, a.Auftrag, f["basisHash"]?.GetValue<string>(), bauen: true))!;
        return new
        {
            ok = ueb["ok"]?.GetValue<bool>() == true, geschrieben = ueb["ok"]?.GetValue<bool>() == true,
            grund = ueb["grund"]?.GetValue<string>(), datei = ueb["datei"]?.GetValue<string>(), zeile = ueb["zeile"]?.GetValue<int>(),
            bau = ueb["bau"], runden = f["runden"],
        };
    }

    // ── Übernehmen / Rückgängig / Bauen ──

    /// <param name="bauen">danach das Projekt bauen — der echte Build, die Code-Generatoren laufen mit.</param>
    public object Uebernehmen(string id, string rumpf, string? auftrag, string? basisHash, bool bauen = false)
    {
        var slot = Slot(id);
        if (slot == null) return new { ok = false, grund = "Unbekannter Slot." };
        if (IstFluss(slot)) return UebernehmeLambda(id, slot, rumpf, basisHash, bauen);
        var anker = Anker(slot);
        var (ok, grund, alt, neuHash, zeile) = CodeSync.SetzeRumpf(anker, rumpf, auftrag, basisHash, _sln);
        if (!ok) return new { ok, grund };
        var sicherung = Path.Combine(_verz, "sicherung");
        Directory.CreateDirectory(sicherung);
        var datei = Path.Combine(sicherung, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Regex.Replace(id, @"[^\w.@-]", "_")}.json");
        File.WriteAllText(datei, JsonSerializer.Serialize(new { id, anker, alterInhalt = alt, neuerHash = neuHash }));
        Protokolliere(id, new { aktion = "uebernommen", datei = anker.Datei, zeile });
        return new { ok = true, datei = anker.Datei, zeile, sicherung = Path.GetFileName(datei), bau = bauen ? Bauen(id) : null,
            hinweis = "Kontexte sind jetzt veraltet — neu erzeugen, bevor der nächste Slot gefüllt wird." };
    }

    private object UebernehmeLambda(string id, JsonObject slot, string ausdruck, string? basisHash, bool bauen)
    {
        var anker = LAnker(slot);
        var (ok, grund, alt, neuHash, zeile) = CodeSync.SetzeLambda(anker, ausdruck, basisHash, _sln);
        if (!ok) return new { ok, grund };
        var sicherung = Path.Combine(_verz, "sicherung");
        Directory.CreateDirectory(sicherung);
        var datei = Path.Combine(sicherung, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Regex.Replace(id, @"[^\w.@-]", "_")}.json");
        File.WriteAllText(datei, JsonSerializer.Serialize(new { id, lambdaAnker = anker, alterInhalt = alt, neuerHash = neuHash }));
        Protokolliere(id, new { aktion = "uebernommen", datei = anker.Datei, zeile });
        return new { ok = true, datei = anker.Datei, zeile, sicherung = Path.GetFileName(datei), bau = bauen ? Bauen(id) : null,
            hinweis = "Kontexte sind jetzt veraltet — neu erzeugen, bevor der nächste Slot gefüllt wird." };
    }

    public object Rueckgaengig(string id, bool bauen = false)
    {
        var sicherung = Path.Combine(_verz, "sicherung");
        var muster = Regex.Replace(id, @"[^\w.@-]", "_");
        var letzte = Directory.Exists(sicherung)
            ? Directory.GetFiles(sicherung, $"*-{muster}.json").OrderByDescending(f => f).FirstOrDefault() : null;
        if (letzte == null) return new { ok = false, grund = "Keine Sicherung für diesen Slot." };
        var o = JsonNode.Parse(File.ReadAllText(letzte))!;
        var (ok, grund) = o["lambdaAnker"] is { } la
            ? CodeSync.SetzeLambdaZurueck(la.Deserialize<LambdaAnker>()!, S(o, "alterInhalt"), S(o, "neuerHash"), _sln)
            : CodeSync.SetzeInhaltZurueck(o["anker"].Deserialize<MethodenAnker>()!, S(o, "alterInhalt"), S(o, "neuerHash"), _sln);
        if (ok) { File.Move(letzte, letzte + ".zurueckgenommen"); Protokolliere(id, new { aktion = "rueckgaengig" }); }
        return new { ok, grund, bau = ok && bauen ? Bauen(id) : null };
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

    /// <summary>Beim Start: fehlen die Kontexte, erzeugt SimHost sie im Hintergrund (die GUI zeigt „wird erzeugt“).</summary>
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
        var psi = new ProcessStartInfo("dotnet", "run --project GraphExtractor -- --kontexte .llm-kontext")
        { WorkingDirectory = _sln, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(TimeSpan.FromMinutes(5))) { try { p.Kill(true); } catch { } return new { ok = false, grund = "Zeitüberschreitung (5 min)." }; }
        var zeilen = (o.Result + "\n" + e.Result).Split('\n');
        lock (_sperre) _basisFehler.Clear();
        var meldung = zeilen.Where(l => l.Contains('✅') || l.Contains('❌') || l.Contains("error")).Select(l => l.Trim()).Take(10).ToList();
        return new { ok = p.ExitCode == 0, meldung, grund = p.ExitCode == 0 ? null : string.Join(" · ", meldung.Take(3)) };
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
