using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimHost;

/// <summary>Anker eines füllbaren Rumpfs — aus dem Modell (das der Extractor aus dem Code liest), nicht aus einer Konvention.</summary>
/// <param name="Kind">"decider" | "applier" (nur für Meldungen).</param>
/// <param name="Namespace">Aggregat-Namespace (nur für Meldungen).</param>
/// <param name="Disc">Diskriminator: Typ des ersten Parameters (Command bzw. Event) — identifiziert die Methode.</param>
/// <param name="Datei">Die echte Quelldatei relativ zur Solution (aus dem Modell); null = noch nicht geschrieben.</param>
public sealed record CodeAnker(string Kind, string Namespace, string Disc, string? Datei);

/// <summary>Anker für ALLE Slot-Arten (LLM-Konsole): Datei relativ zur Solution, Klasse, Methode, Typ des 1. Parameters (null = parameterlos).</summary>
public sealed record MethodenAnker(string Datei, string Klasse, string Methode, string? ParameterTyp);

/// <summary>
/// Die SYNCHRONISATIONS-NAHT zwischen Board (Browser) und echter <c>.cs</c>-Datei. Der Browser schreibt
/// bewusst NUR eine Sache: die <c>// 🤖 Prompt:</c>-Kommentarzeile im Methoden-Rumpf; echten Code editiert
/// der Mensch im richtigen Editor. Die Datei ist die Wahrheit; der Anker wird aus dem Graphen abgeleitet
/// (Typ + Methode), NICHT als Marker in der Datei abgelegt — Roslyn findet die Methode über ihre Signatur.
///
/// Heute nur die Schreibseite (Decider/Applier) — genau das, was der Scaffolder als füllbare Rümpfe
/// (throw-Platzhalter) erzeugt. Leseseite-Rümpfe folgen, sobald der Scaffolder sie in Dateien schreibt.
/// </summary>
public static class CodeSync
{
    private const string PromptMarke = "// 🤖 Prompt:";

    private sealed record Treffer(bool Ok, string Pfad, string Text, MethodDeclarationSyntax? M, int Zeile, string Grund);

    // ── Resolver: (Kind, Namespace, Disc) → Datei + Methode. Der eine Dreh- und Angelpunkt. ──
    private static Treffer Aufloesen(CodeAnker a, string slnRoot)
    {
        if (string.IsNullOrWhiteSpace(a.Datei))
            return new(false, "", "", null, 0, $"{a.Disc}: noch keine Datei — erst „C# schreiben“.");
        var pfad = Path.GetFullPath(Path.Combine(slnRoot, a.Datei));
        if (!pfad.StartsWith(Path.GetFullPath(slnRoot), StringComparison.Ordinal))
            return new(false, pfad, "", null, 0, "Pfad liegt außerhalb der Solution.");
        if (!File.Exists(pfad))
            return new(false, pfad, "", null, 0, $"Datei fehlt: {a.Datei} — erst „C# schreiben“.");

        var text = File.ReadAllText(pfad);
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        // Die Methode über ihren ersten Parameter-Typ — kein Methodenname nötig (Command/Event sind eindeutig).
        var m = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(x =>
            x.ParameterList.Parameters.Count > 0 &&
            x.ParameterList.Parameters[0].Type?.ToString().Split('.').Last() == a.Disc);
        if (m?.Body is null)
            return new(false, pfad, text, null, 0, $"Methode mit Parameter {a.Disc} nicht gefunden in {a.Datei}.");

        var zeile = m.Body.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line + 2; // erste Rumpfzeile
        return new(true, pfad, text, m, zeile, "");
    }

    // ── Lesen (Datei → Browser): der Spiegel. Liefert Rumpf-Text, Prompt und Hash (für die Sperre). ──
    public static object Lese(CodeAnker a, string slnRoot)
    {
        var t = Aufloesen(a, slnRoot);
        if (!t.Ok) return new { ok = false, grund = t.Grund, pfad = Rel(t.Pfad, slnRoot) };
        var inner = Inner(t);
        return new { ok = true, pfad = Rel(t.Pfad, slnRoot), zeile = t.Zeile, body = Dedent(inner), prompt = LiesPrompt(inner), hash = Hash(inner) };
    }

    // ── Schreiben (Browser → Datei): NUR die Prompt-Kommentarzeile, alles andere bleibt exakt erhalten. ──
    public static object SetzePrompt(CodeAnker a, string? prompt, string? baseHash, string slnRoot)
    {
        var t = Aufloesen(a, slnRoot);
        if (!t.Ok) return new { ok = false, grund = t.Grund };
        var inner = Inner(t);
        if (baseHash is not null && Hash(inner) != baseHash)                 // optimistische Sperre: Datei extern geändert
            return new { ok = false, grund = "stale", hash = Hash(inner), body = Dedent(inner), prompt = LiesPrompt(inner) };

        var neuInner = MitPrompt(inner, prompt, t.M!);
        var body = t.M!.Body!;
        var start = body.OpenBraceToken.Span.End;
        var end = body.CloseBraceToken.Span.Start;
        var neuText = t.Text[..start] + neuInner + t.Text[end..];
        File.WriteAllText(t.Pfad, neuText);
        return new { ok = true, hash = Hash(neuInner), body = Dedent(neuInner), prompt = LiesPrompt(neuInner) };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    //  ALLE Slot-Arten (LLM-Konsole): Anker = Datei + Klasse + Methode (+ Typ des 1. Parameters).
    //  Schreibt den KOMPLETTEN Rumpf (mit „// 🤖 Prompt:“-Zeile) — nur auf ausdrückliches Übernehmen.
    // ════════════════════════════════════════════════════════════════════════════════════════

    private sealed record MethodenTreffer(bool Ok, string Pfad, string Text, MethodDeclarationSyntax? M, string Grund);

    private static MethodenTreffer Finde(MethodenAnker a, string slnRoot)
    {
        var pfad = Path.GetFullPath(Path.Combine(slnRoot, a.Datei));
        if (!pfad.StartsWith(Path.GetFullPath(slnRoot), StringComparison.Ordinal)) return new(false, pfad, "", null, "Pfad liegt außerhalb der Solution.");
        if (!File.Exists(pfad)) return new(false, pfad, "", null, $"Datei fehlt: {a.Datei}");
        var text = File.ReadAllText(pfad);
        var kandidaten = CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == a.Methode
                        && m.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text == a.Klasse
                        && (a.ParameterTyp == null
                            ? m.ParameterList.Parameters.Count == 0
                            : m.ParameterList.Parameters.Count > 0 && Basisname(m.ParameterList.Parameters[0].Type?.ToString()) == a.ParameterTyp))
            .ToList();
        if (kandidaten.Count != 1)
            return new(false, pfad, text, null, kandidaten.Count == 0 ? $"{a.Klasse}.{a.Methode}({a.ParameterTyp}) nicht gefunden in {a.Datei}." : $"{a.Klasse}.{a.Methode}({a.ParameterTyp}) ist mehrdeutig in {a.Datei}.");
        var treffer = kandidaten[0];
        if (treffer.Body is null && treffer.ExpressionBody is null) return new(false, pfad, text, null, "Methode ohne Rumpf.");
        return new(true, pfad, text, treffer, "");
    }

    private static string Basisname(string? typ) => (typ ?? "").Split('<')[0].Split('.').Last().TrimEnd('?').Trim();

    /// <summary>Der Rumpf als Text: bei Block der Inhalt zwischen den Klammern, bei Ausdruck „=&gt; …;“.</summary>
    private static string RumpfInhalt(MethodenTreffer t) => t.M!.Body is { } b
        ? t.Text[b.OpenBraceToken.Span.End..b.CloseBraceToken.Span.Start].Replace("\r\n", "\n")
        : "=> " + t.M.ExpressionBody!.Expression + ";";

    /// <summary>Aktueller Rumpf (dedentet, OHNE Prompt-Zeile) + Prompt + Hash — für Anzeige und optimistische Sperre.</summary>
    public static object LeseRumpf(MethodenAnker a, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        if (!t.Ok) return new { ok = false, grund = t.Grund };
        var inner = RumpfInhalt(t);
        var ohnePrompt = string.Join("\n", inner.Split('\n').Where(l => !l.TrimStart().StartsWith(PromptMarke)));
        return new { ok = true, pfad = Rel(t.Pfad, slnRoot), zeile = t.M!.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            body = Dedent(ohnePrompt), prompt = LiesPrompt(inner), hash = Hash(inner) };
    }

    public static string? RumpfHash(MethodenAnker a, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        return t.Ok ? Hash(RumpfInhalt(t)) : null;
    }

    /// <summary>
    /// Den Rumpf ersetzen: <paramref name="rumpf"/> = Anweisungen OHNE äußere Klammern (dedentet); davor die Prompt-Zeile.
    /// Ein Ausdrucks-Rumpf („=&gt; …;“) wird zum Block. Sperre: <paramref name="baseHash"/> muss zum Dateistand passen.
    /// Liefert den alten Rumpf-Inhalt zurück (für Rückgängig).
    /// </summary>
    public static (bool Ok, string Grund, string? AlterInhalt, string? NeuerHash, int Zeile) SetzeRumpf(
        MethodenAnker a, string rumpf, string? prompt, string? baseHash, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        if (!t.Ok) return (false, t.Grund, null, null, 0);
        var alt = RumpfInhalt(t);
        if (baseHash != null && Hash(alt) != baseHash) return (false, "Die Datei wurde seit dem Füllen geändert (Hash passt nicht) — neu laden.", null, null, 0);

        var nl = t.Text.Contains("\r\n") ? "\r\n" : "\n";
        var m = t.M!;
        var methodenSpalte = m.GetLocation().GetLineSpan().StartLinePosition.Character;
        var einzug = new string(' ', methodenSpalte + 4);
        var zeilen = Dedent(rumpf.Replace("\r\n", "\n")).Split('\n').ToList();
        if (!string.IsNullOrWhiteSpace(prompt)) zeilen.Insert(0, PromptMarke + " " + EinZeile(prompt!));
        var inhalt = nl + string.Join(nl, zeilen.Select(z => z.Trim().Length == 0 ? "" : einzug + z)) + nl + new string(' ', methodenSpalte);

        string neuText;
        if (m.Body is { } b)
            neuText = t.Text[..b.OpenBraceToken.Span.End] + inhalt + t.Text[b.CloseBraceToken.Span.Start..];
        else
        {
            // „=> …;“ → „{ … }“ auf eigener Zeile
            var start = m.ExpressionBody!.Span.Start;
            var ende = m.SemicolonToken.Span.End;
            var vorher = t.Text[..start].TrimEnd(' ');
            neuText = vorher + nl + new string(' ', methodenSpalte) + "{" + inhalt + "}" + t.Text[ende..];
        }
        File.WriteAllText(t.Pfad, neuText);
        var neu = Finde(a, slnRoot);
        return (true, "", alt, neu.Ok ? Hash(RumpfInhalt(neu)) : null, m.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    /// <summary>Rückgängig: den gesicherten alten Rumpf-Inhalt wörtlich zurückschreiben (nur wenn seitdem unverändert).</summary>
    public static (bool Ok, string Grund) SetzeInhaltZurueck(MethodenAnker a, string alterInhalt, string erwarteterHash, string slnRoot)
    {
        var t = Finde(a, slnRoot);
        if (!t.Ok) return (false, t.Grund);
        if (Hash(RumpfInhalt(t)) != erwarteterHash) return (false, "Die Datei wurde nach dem Übernehmen weiter geändert — Rückgängig verweigert.");
        var nl = t.Text.Contains("\r\n") ? "\r\n" : "\n";
        var b = t.M!.Body!;   // nach dem Übernehmen ist es immer ein Block
        string neuText;
        if (alterInhalt.StartsWith("=> "))
        {
            // war Ausdrucks-Rumpf: Block wieder durch „=> …;“ ersetzen
            var vorher = t.Text[..b.Span.Start].TrimEnd();
            neuText = vorher + " " + alterInhalt + t.Text[b.Span.End..];
        }
        else
            neuText = t.Text[..b.OpenBraceToken.Span.End] + alterInhalt.Replace("\n", nl) + t.Text[b.CloseBraceToken.Span.Start..];
        File.WriteAllText(t.Pfad, neuText);
        return (true, "");
    }

    /// <summary>Nur das Projekt bauen, zu dem die Datei gehört (nächste .csproj aufwärts) — schneller als die ganze Solution.</summary>
    public static object BaueProjektVon(string relDatei, string slnRoot)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(slnRoot, relDatei)));
        string? proj = null;
        while (dir != null && dir.StartsWith(slnRoot, StringComparison.Ordinal) && proj == null)
        {
            proj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
            dir = Path.GetDirectoryName(dir);
        }
        if (proj == null) return new { ok = false, projekt = (string?)null, fehler = new List<string> { "Kein .csproj über der Datei gefunden." } };
        var psi = new ProcessStartInfo("dotnet", $"build \"{proj}\" -v q --nologo")
        {
            WorkingDirectory = slnRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        var fehler = (o.Result + "\n" + e.Result).Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Contains(": error")).Select(l => l.Trim()).Distinct().Take(50).ToList();
        return new { ok = p.ExitCode == 0, projekt = Rel(proj, slnRoot), fehler };
    }

    // ── Öffnen im Standard-Editor (VS Code mit Sprung zur Rumpfzeile; sonst OS-Standard). ──
    public static object Oeffne(CodeAnker a, string slnRoot)
    {
        var t = Aufloesen(a, slnRoot);
        if (!t.Ok) return new { ok = false, grund = t.Grund };
        var rel = Rel(t.Pfad, slnRoot);

        // 1) CLI-Launcher (mit Sprung-zur-Zeile): CQRS_EDITOR (Name ODER voller Pfad) → Rider → VS Code.
        //    Vollpfad selbst aufgelöst (wie `which`) → unabhängig von .NETs PATH-Semantik.
        foreach (var ed in new[] { Environment.GetEnvironmentVariable("CQRS_EDITOR"), "rider", "code" }
                     .Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var exe = Path.IsPathRooted(ed) ? (File.Exists(ed) ? ed : null) : ImPfad(ed);
            if (exe is not null && Versuche(EditorPsi(exe, ed, t.Pfad, t.Zeile)))
                return new { ok = true, editor = ed, zeilensprung = true, pfad = rel };
        }

        // 2) macOS: App per NAMEN starten (kein PATH-Launcher nötig; ohne Zeilensprung). Rider bevorzugt.
        foreach (var app in new[] { "Rider", "JetBrains Rider", "Rider EAP" })
            if (MacAppOeffnen(app, t.Pfad))
                return new { ok = true, editor = app, zeilensprung = false, pfad = rel };

        // 3) OS-Standard (registrierte App für .cs) — letzter Ausweg.
        if (Versuche(new ProcessStartInfo(t.Pfad) { UseShellExecute = true }))
            return new { ok = true, editor = "os-standard", zeilensprung = false, pfad = rel };
        return new { ok = false, grund = "Kein Editor gefunden — Rider öffnen scheiterte; 'rider'-Launcher ins PATH legen oder CQRS_EDITOR setzen." };
    }

    // Editor-spezifische Sprung-zur-Zeile-Syntax. exe = voller Pfad; style = Name/Pfad zur Stil-Erkennung (Basisname).
    private static ProcessStartInfo EditorPsi(string exe, string style, string pfad, int zeile)
    {
        var n = Path.GetFileNameWithoutExtension(style).ToLowerInvariant();
        var args = n is "rider" or "idea" or "webstorm" or "pycharm" ? $"--line {zeile} \"{pfad}\""   // JetBrains
                 : n is "code" or "code-insiders" or "codium" ? $"-g \"{pfad}\":{zeile}"               // VS Code
                 : $"\"{pfad}\"";
        return new ProcessStartInfo(exe, args) { UseShellExecute = false };
    }

    // Voller Pfad eines Kommandos über PATH (Ersatz für `which`, unabhängig von .NET-Verhalten).
    private static string? ImPfad(string cmd)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try { var p = Path.Combine(dir, cmd); if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    private static bool Versuche(ProcessStartInfo psi)
    {
        try { return Process.Start(psi) != null; } catch { return false; }
    }

    // macOS `open -a <App> <pfad>`: startet die App per Namen. Exit-Code 0 = App existierte & öffnete.
    private static bool MacAppOeffnen(string app, string pfad)
    {
        try
        {
            var psi = new ProcessStartInfo("open", $"-a \"{app}\" \"{pfad}\"") { RedirectStandardError = true, UseShellExecute = false };
            var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(4000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch { return false; }
    }

    // ── Bauen: die Generatoren + Proto-Prepass laufen bei dotnet build automatisch mit (siehe ProtoRepo.csproj). ──
    public static object Baue(string slnRoot)
    {
        var psi = new ProcessStartInfo("dotnet", "build -v q --nologo")
        {
            WorkingDirectory = slnRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var aus = p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd();
        p.WaitForExit();
        var fehler = aus.Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Contains(": error"))
            .Select(l => l.Trim()).Distinct().Take(50).ToList();
        return new { ok = p.ExitCode == 0, fehler };
    }

    // ── Neu einlesen (Code → Board): den GraphExtractor laufen lassen — er schreibt domain-model.json,
    //    knowledge-graph.* und editor.html aus dem AKTUELLEN Code. Der Browser merged danach (Code = Wahrheit). ──
    public static object Extrahiere(string slnRoot)
    {
        var psi = new ProcessStartInfo("dotnet", "run --project GraphExtractor")
        {
            WorkingDirectory = slnRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* bereits beendet */ }
            return new { ok = false, grund = "GraphExtractor: Zeitüberschreitung (5 min)." };
        }
        var aus = (stdout.Result + "\n" + stderr.Result).Replace("\r\n", "\n").Split('\n');
        return new
        {
            ok = p.ExitCode == 0,
            grund = p.ExitCode == 0 ? "" : string.Join("\n", aus.Where(l => l.Contains("error") || l.Contains("❌")).Take(10)),
            zusammenfassung = aus.Where(l => l.Contains("Aggregate") || l.Contains("Diagnosen")).Select(l => l.Trim()).ToList(),
        };
    }

    // ── Bausteine ─────────────────────────────────────────────────────────────────────────────
    private static string Inner(Treffer t)
    {
        var body = t.M!.Body!;
        var start = body.OpenBraceToken.Span.End;
        var end = body.CloseBraceToken.Span.Start;
        return t.Text[start..end].Replace("\r\n", "\n");
    }

    // Prompt surgisch setzen: bestehende Prompt-Zeile(n) entfernen, neue als erste Rumpfzeile — Rest UNANGETASTET.
    private static string MitPrompt(string inner, string? prompt, MethodDeclarationSyntax m)
    {
        var zeilen = inner.Split('\n').Where(l => !l.TrimStart().StartsWith(PromptMarke)).ToList();
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            var idx = zeilen.FindIndex(l => l.Trim().Length > 0);
            var einzug = idx >= 0 ? Einzug(zeilen[idx]) : new string(' ', OffeneSpalte(m) + 4);
            var zeile = einzug + PromptMarke + " " + EinZeile(prompt!);
            zeilen.Insert(idx >= 0 ? idx : Math.Min(1, zeilen.Count), zeile);
        }
        return string.Join("\n", zeilen);
    }

    private static string? LiesPrompt(string inner)
    {
        var l = inner.Split('\n').FirstOrDefault(x => x.TrimStart().StartsWith(PromptMarke));
        return l?.TrimStart()[PromptMarke.Length..].Trim();
    }

    private static int OffeneSpalte(MethodDeclarationSyntax m) =>
        m.Body!.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Character;

    private static string Einzug(string zeile) => zeile[..(zeile.Length - zeile.TrimStart().Length)];
    private static string EinZeile(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();
    private static string Rel(string pfad, string root) => Path.GetRelativePath(root, pfad).Replace('\\', '/');

    // Gemeinsamen Mindest-Einzug abziehen → hübsche Vorschau im Board.
    private static string Dedent(string s)
    {
        var zeilen = s.Split('\n');
        var min = zeilen.Where(z => z.Trim().Length > 0).Select(z => z.Length - z.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select(z => z.Length >= min ? z[min..] : z)).Trim('\n');
    }

    // Hash über den INHALT (dedentet, getrimmt) → stabil gegen reine Whitespace-Unterschiede.
    private static string Hash(string inner)
    {
        var norm = Dedent(inner).Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(norm)))[..12];
    }
}
