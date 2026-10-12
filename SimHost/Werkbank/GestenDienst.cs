using System.Diagnostics;
using DomainEditor;
using GraphExtractor;

namespace SimHost.Werkbank;

/// <summary>Eine gemessene Geste (Werkbank P7.2, §16): Schreiben (Patch rechnen + speichern), Lesen (warmer Workspace), Befunde, gesamt.</summary>
public sealed record GestenMessung(DateTimeOffset Zeit, string Art, string Titel, int Dateien, long SchreibenMs, long LesenMs, long BefundeMs, long GesamtMs);

/// <summary>Die Antwort auf eine Geste: was geschrieben wurde, was nicht (mit Grund), Compiler-Befunde der berührten Dateien, Zeit.</summary>
public sealed record GestenAntwort(bool Ok, string? Fehler, string Titel, IReadOnlyList<string> Dateien, IReadOnlyList<string> Meldungen,
    IReadOnlyList<Diagnose> Befunde, long Ms);

/// <summary>
/// Geste → Patch → Workspace → Board (docs/plan-werkbank-umsetzung.md P7.2). Jede Geste ist ein Patch am Code: Karten ändern/anlegen/löschen
/// über den Schreiber (<see cref="GraphExtractor.Gesten"/>), Umbenennen über den Roslyn-Renamer, ↶ über den <see cref="Verlauf"/>. Danach liest
/// der warme Workspace neu; die Antwort trägt die Compiler-Befunde der berührten Dateien. Eine Geste nach der anderen; nie auf <c>main</c>.
/// </summary>
public sealed class GestenDienst(string slnRoot, Arbeitsbereich arbeitsbereich, Verlauf verlauf, Func<string> branch, Action<string, object> melde)
{
    private readonly SemaphoreSlim _eine = new(1, 1);
    private readonly List<GestenMessung> _messungen = new();
    /// <summary>Compiler-Befunde je Datei, die eine Geste berührt hat — bis zum nächsten ⟳ (dann gilt der echte Bau).</summary>
    private readonly Dictionary<string, IReadOnlyList<Diagnose>> _befunde = new(StringComparer.Ordinal);

    public Verlauf Verlauf => verlauf;

    /// <summary>Die Befunde aus Gesten (je berührter Datei der jüngste Stand).</summary>
    public (IReadOnlySet<string> Dateien, IReadOnlyList<Diagnose> Befunde) Befunde()
    {
        lock (_befunde) return (_befunde.Keys.ToHashSet(StringComparer.Ordinal), _befunde.Values.SelectMany(x => x).ToList());
    }

    /// <summary>⟳ hat gebaut — dessen Befunde gelten jetzt für alle Dateien.</summary>
    public void BefundeLeeren() { lock (_befunde) _befunde.Clear(); }

    public object Messungen()
    {
        List<GestenMessung> l;
        lock (_messungen) l = _messungen.ToList();
        long P(IEnumerable<long> xs, double q) { var s = xs.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[(int)Math.Min(s.Count - 1, Math.Floor(q * s.Count))]; }
        return new
        {
            anzahl = l.Count,
            p50 = P(l.Where(m => m.Art != "umbenennen").Select(m => m.GesamtMs), 0.5),
            p95 = P(l.Where(m => m.Art != "umbenennen").Select(m => m.GesamtMs), 0.95),
            umbenennenP50 = P(l.Where(m => m.Art == "umbenennen").Select(m => m.GesamtMs), 0.5),
            letzte = l.TakeLast(30).Reverse(),
        };
    }

    private string? Gesperrt() => branch() is "main" or "master"
        ? $"Die Werkbank schreibt nicht auf „{branch()}“ — einen eigenen Branch bzw. Worktree anlegen."
        : null;

    /// <summary>Karten geändert/angelegt/entfernt.</summary>
    public Task<GestenAntwort> AendernAsync(string titel, KartenDelta delta, CancellationToken ct) => AusfuehrenAsync("aendern", titel, async () =>
    {
        var vor = await arbeitsbereich.LiesAsync(ct);
        var puffer = new Dateipuffer(slnRoot);
        var bericht = GraphExtractor.Gesten.Schreibe(vor.Board, delta, puffer);
        var meldungen = bericht.Schreiber.Uebersprungen.Where(u => !u.Contains("(unangetastet)", StringComparison.Ordinal))
            .Concat(bericht.NichtGeloescht).ToList();
        puffer.Speichern();
        return (bericht.Patch, meldungen);
    }, ct);

    /// <summary>Das Symbol am Ort der Karte umbenennen — samt aller Verweise (auch in Lambdas).</summary>
    public Task<GestenAntwort> UmbenennenAsync(string titel, string datei, int start, string neu, CancellationToken ct) => AusfuehrenAsync("umbenennen", titel, async () =>
    {
        var stand = await arbeitsbereich.StandAsync(ct);
        var (patch, alt) = await Umbenennen.Async(stand, Path.GetFullPath(Path.Combine(slnRoot, datei)), start, neu, slnRoot, ct);
        foreach (var p in patch) File.WriteAllText(Path.Combine(slnRoot, p.Datei), p.Nachher!);
        return (patch, patch.Count == 0 ? [$"„{alt}“ heißt schon so"] : new List<string>());
    }, ct);

    /// <summary>
    /// Rumpf-Edit (✎ am Rumpf-Port, Werkbank P7.4): genau den Rumpf einer Methode bzw. einen Lambda an seinem Ort ersetzen — der Rest der Datei
    /// bleibt Wort für Wort. <paramref name="alt"/> = der Rumpf, wie ihn der Editor zeigte (Sperre gegen eine inzwischen geänderte Datei).
    /// </summary>
    public Task<GestenAntwort> RumpfAsync(string titel, RumpfOrt ort, string text, string? alt, CancellationToken ct) => AusfuehrenAsync("rumpf", titel, () =>
    {
        var puffer = new Dateipuffer(slnRoot);
        if (!puffer.Innerhalb(ort.Datei)) throw new InvalidOperationException("Pfad liegt außerhalb der Solution.");
        Rumpf.Ersetze(puffer, ort, text, alt);
        puffer.Speichern();
        return Task.FromResult((puffer.Patch(), new List<string>()));
    }, ct);

    /// <summary>Rumpf-Edit eines Python-Rumpfs (Zusage/Kenntnis einer Client-Implementierung) — über <see cref="PythonSync"/>.</summary>
    public Task<GestenAntwort> PythonRumpfAsync(string titel, PythonAnker anker, string text, string? alt, CancellationToken ct) => AusfuehrenAsync("rumpf", titel, () =>
    {
        var puffer = new Dateipuffer(slnRoot);
        if (!puffer.Innerhalb(anker.Datei)) throw new InvalidOperationException("Pfad liegt außerhalb der Solution.");
        var vorher = puffer.Lies(anker.Datei) ?? throw new InvalidOperationException($"Datei fehlt: {anker.Datei}");
        static string Norm(string? x) => System.Text.RegularExpressions.Regex.Replace(string.Join("\n", (x ?? "").Split('\n')
            .Where(z => !z.TrimStart().StartsWith(PythonSync.PromptMarke, StringComparison.Ordinal))), @"\s+", " ").Trim();
        if (alt != null && Norm(PythonQuelle.Rumpf(vorher, anker.Klasse, anker.Methode)) != Norm(alt))
            throw new InvalidOperationException($"Der Rumpf in {anker.Datei} wurde inzwischen geändert — neu laden.");
        var (ok, grund, neu) = PythonSync.MitRumpf(anker, text, null, slnRoot);
        if (!ok) throw new InvalidOperationException(grund);
        puffer.Setze(anker.Datei, neu!);
        puffer.Speichern();
        return Task.FromResult((puffer.Patch(), new List<string>()));
    }, ct);

    /// <summary>↶ — den letzten Patch zurücknehmen (Geste oder LLM-Übernahme).</summary>
    public async Task<GestenAntwort> RueckgaengigAsync(CancellationToken ct)
    {
        await _eine.WaitAsync(ct);
        try
        {
            var uhr = Stopwatch.StartNew();
            var (eintrag, fehler) = verlauf.NimmZurueck();
            if (eintrag == null) return new GestenAntwort(false, fehler, "↶", [], [], [], uhr.ElapsedMilliseconds);
            var dateien = eintrag.Patch.Select(p => p.Datei).ToList();
            await arbeitsbereich.UebernimmAsync(dateien, ct);
            var befunde = await Merke(dateien, ct);
            melde("geste", new { art = "rueckgaengig", titel = eintrag.Titel, dateien });
            return new GestenAntwort(true, null, "↶ " + eintrag.Titel, dateien, [], befunde, uhr.ElapsedMilliseconds);
        }
        finally { _eine.Release(); }
    }

    private async Task<GestenAntwort> AusfuehrenAsync(string art, string titel, Func<Task<(IReadOnlyList<DateiPatch> Patch, List<string> Meldungen)>> schreiben,
        CancellationToken ct)
    {
        if (Gesperrt() is { } gesperrt) return new GestenAntwort(false, gesperrt, titel, [], [], [], 0);
        await _eine.WaitAsync(ct);
        try
        {
            var uhr = Stopwatch.StartNew();
            IReadOnlyList<DateiPatch> patch;
            List<string> meldungen;
            try { (patch, meldungen) = await schreiben(); }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                return new GestenAntwort(false, e.Message, titel, [], [], [], uhr.ElapsedMilliseconds);
            }
            var schreibenMs = uhr.ElapsedMilliseconds;
            var dateien = patch.Select(p => p.Datei).ToList();
            verlauf.Merke(art, titel, patch);
            await arbeitsbereich.UebernimmAsync(dateien, ct);
            var lesenMs = uhr.ElapsedMilliseconds - schreibenMs;
            var befunde = await Merke(dateien, ct);
            var gesamt = uhr.ElapsedMilliseconds;
            var m = new GestenMessung(DateTimeOffset.Now, art, titel, dateien.Count, schreibenMs, lesenMs, gesamt - schreibenMs - lesenMs, gesamt);
            lock (_messungen) { _messungen.Add(m); if (_messungen.Count > 500) _messungen.RemoveAt(0); }
            Console.WriteLine($"[Geste] {art} „{titel}“: {dateien.Count} Datei(en), Schreiben {m.SchreibenMs} ms, Lesen {m.LesenMs} ms, Befunde {m.BefundeMs} ms, gesamt {gesamt} ms");
            melde("geste", new { art, titel, dateien, ms = gesamt });
            return new GestenAntwort(true, null, titel, dateien, meldungen, befunde, gesamt);
        }
        finally { _eine.Release(); }
    }

    private async Task<IReadOnlyList<Diagnose>> Merke(IReadOnlyList<string> dateien, CancellationToken ct)
    {
        var cs = dateien.Where(d => d.EndsWith(".cs", StringComparison.Ordinal)).ToList();
        var befunde = await arbeitsbereich.BefundeAsync(cs, ct);
        lock (_befunde)
            foreach (var d in cs) _befunde[d] = befunde.Where(b => b.Datei == d).ToList();
        return befunde;
    }
}
