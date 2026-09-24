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
