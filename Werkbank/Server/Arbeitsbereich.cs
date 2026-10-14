using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Nodes;
using GraphExtractor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Werkbank.Server;

/// <summary>Eine Messung des Lesens: kalt (ganze Solution) oder warm (nach Datei-Änderungen, nur Domänen-Compilations).</summary>
public sealed record LeseMessung(DateTimeOffset Zeit, string Art, int Dateien, long UebersetzenMs, long LesenMs, IReadOnlyDictionary<string, long> Phasen);

/// <summary>
/// Der warme Roslyn-Workspace der Werkbank (Werkbank P7.1, docs/konzept-werkbank.md §8.2): die Solution wird EINMAL geladen (MSBuildWorkspace, kalt:
/// ganze Lage samt Laufzeit und Hosts), danach hält ein Dateiwächter den Stand aktuell (<see cref="Solution.WithDocumentText"/> — nur im
/// Speicher, der Workspace schreibt nie auf die Platte). Neu gelesen wird nur aus den Compilations unterhalb der Laufzeit (E7): Roslyn übersetzt
/// dabei nur die geänderten Projekte und ihre Abhängigen; das Routing kommt aus den Decide-Signaturen, die Composition Root aus dem kalten Lesen.
/// Ändert sich eine Projektdatei (<c>.csproj</c>, <c>.props</c>, <c>.sln</c>), wird beim nächsten Lesen kalt neu geladen.
/// <para>Das gelesene Board ist die Quelle für <c>/api/editor/model</c>, die Karten-Zuordnung der Befunde und ⟳ „Neu lesen“; es wird zusätzlich
/// nach <c>domain-model.json</c> geschrieben (Spur, Durchspielen und LLM-Konsole lesen die Datei).</para>
/// </summary>
public sealed class Arbeitsbereich : IDisposable
{
    private readonly string _root;
    private readonly Action<string, object>? _melde;
    private readonly SemaphoreSlim _sperre = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _geaendert = new(StringComparer.Ordinal);
    private readonly List<LeseMessung> _messungen = new();
    private volatile bool _kaltNeu;
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private Projektlage? _kalt;
    private CompositionRoot? _zusammensetzung;
    private Lesung? _letzte;
    private string? _boardJson;
    private FileSystemWatcher? _waechter;
    private Task? _laden;

    /// <param name="melde">Fortschritt nach außen (SSE der Werkbank): <c>gelesen</c> nach jedem Lesen.</param>
    public Arbeitsbereich(string slnRoot, Action<string, object>? melde = null)
    {
        _root = slnRoot;
        _melde = melde;
    }

    /// <summary>Der Fehler des letzten kalten Ladens (null = geladen bzw. lädt noch).</summary>
    public string? Fehler { get; private set; }
    public bool Geladen => _letzte != null;
    /// <summary>Das zuletzt gelesene Board (JSON) — ohne neu zu lesen; null, solange nichts gelesen ist.</summary>
    public string? BoardJson => _boardJson;
    /// <summary>Das zuletzt gelesene Board als Knoten (nur lesen).</summary>
    public JsonObject? Board => _letzte?.Board;

    private (Lesung Zu, Sicht Sicht)? _sicht;
    private readonly object _sichtSperre = new();
    /// <summary>
    /// Die Ableitungen des zuletzt gelesenen Boards (Werkbank P7.3) für Anfragen, die das Board nicht mitschickt (⊕ passt, Kette) — eine Instanz
    /// je Lesung, nur unter <paramref name="arbeit"/>s Sperre benutzt (die Sicht merkt sich Zwischenergebnisse).
    /// </summary>
    public T MitSicht<T>(Func<Sicht, T> arbeit)
    {
        lock (_sichtSperre)
        {
            var l = _letzte ?? throw new InvalidOperationException("Noch nichts gelesen.");
            if (_sicht is not { } s || !ReferenceEquals(s.Zu, l)) _sicht = s = (l, Sicht.Aus(l.Board));
            return arbeit(s.Sicht);
        }
    }

    public IReadOnlyList<LeseMessung> Messungen { get { lock (_messungen) return _messungen.ToList(); } }

    /// <summary>Kaltes Laden im Hintergrund anstoßen (einmal; danach hält der Wächter den Stand).</summary>
    public Task StarteAsync() { lock (_messungen) return _laden ??= Task.Run(LadeKaltAsync); }

    /// <summary>
    /// Das aktuelle Board: wartet auf das kalte Laden, übernimmt geänderte Dateien in den Workspace und liest warm neu, wenn sich etwas geändert
    /// hat — sonst das zuletzt Gelesene.
    /// </summary>
    public async Task<Lesung> LiesAsync(CancellationToken ct = default)
    {
        await StarteAsync();
        if (_kaltNeu)
        {
            lock (_messungen) { _kaltNeu = false; _laden = Task.Run(LadeKaltAsync); }
            await _laden!;
        }
        if (_letzte == null) throw new InvalidOperationException(Fehler ?? "Der Workspace ist nicht geladen.");

        await _sperre.WaitAsync(ct);
        try
        {
            var pfade = _geaendert.Keys.ToList();
            foreach (var p in pfade) _geaendert.TryRemove(p, out _);
            if (pfade.Count == 0) return _letzte;

            var uhr = Stopwatch.StartNew();
            var stand = _solution!;
            foreach (var p in pfade.Where(p => p.EndsWith(".cs", StringComparison.Ordinal))) stand = await UebernimmAsync(stand, p);
            // Nur Dateien außerhalb der Projekte — außer Python (die Rümpfe der Client-Implementierungen gehören zum Board, P7.4) und den
            //   Umgebungs-Dateien (Werte je Umgebung, z. B. der Webhook einer Quelle, konzept-werkbank §12.3 E12): sie lesen das Board neu.
            if (ReferenceEquals(stand, _solution) && !pfade.Any(p => p.EndsWith(".py", StringComparison.Ordinal) || IstUmgebung(p))) return _letzte;
            var lage = await _kalt!.NurDomänenAsync(stand, ct);
            var lesung = await Lesen.LiesAsync(stand, lage, _root, _zusammensetzung, uhr.Elapsed);
            _solution = stand;
            Merke(lesung, "warm", pfade.Count);
            return lesung;
        }
        finally { _sperre.Release(); }
    }

    /// <summary>Selbst geschriebene Dateien sofort übernehmen (der Wächter meldet sie erst später) und warm neu lesen.</summary>
    public Task<Lesung> UebernimmAsync(IEnumerable<string> relativePfade, CancellationToken ct = default)
    {
        foreach (var rel in relativePfade) _geaendert[Path.GetFullPath(Path.Combine(_root, rel))] = 0;
        return LiesAsync(ct);
    }

    /// <summary>Der aktuelle Solution-Stand (geänderte Dateien übernommen) — für Roslyn-Refactorings wie den Renamer.</summary>
    public async Task<Solution> StandAsync(CancellationToken ct = default)
    {
        await LiesAsync(ct);
        return _solution!;
    }

    /// <summary>
    /// Compiler-Befunde (Fehler) NUR der genannten Dateien aus dem warmen Workspace — billig, gebunden wird nur diese Datei. So steht nach
    /// einer Geste ein gebrochener Rumpf sofort an seiner Karte (Unfertiges ist Code, der nicht baut); ⟳ ersetzt sie durch den echten Bau.
    /// </summary>
    public async Task<IReadOnlyList<Diagnose>> BefundeAsync(IEnumerable<string> relativePfade, CancellationToken ct = default)
    {
        var lesung = await LiesAsync(ct);
        var aus = new List<Diagnose>();
        foreach (var rel in relativePfade.Distinct())
        {
            var voll = Path.GetFullPath(Path.Combine(_root, rel));
            foreach (var comp in lesung.Lage.Compilations)
            {
                var baum = comp.SyntaxTrees.FirstOrDefault(t => t.FilePath == voll);
                if (baum == null) continue;
                var model = comp.GetSemanticModel(baum);
                var projekt = lesung.Lage.Analyse.First(a => ReferenceEquals(a.Compilation, comp)).Projekt;
                var regeln = Regeln(projekt);
                var diags = model.GetDiagnostics(cancellationToken: ct).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                if (!regeln.IsEmpty)   // die eigenen Regeln (CQRS…) auf dieser Datei — Fehler und Warnungen
                    diags.AddRange((await comp.WithAnalyzers(regeln, projekt.AnalyzerOptions).GetAnalyzerSemanticDiagnosticsAsync(model, null, ct))
                        .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning));
                foreach (var d in diags)
                {
                    var z = d.Location.GetLineSpan().StartLinePosition;
                    var (typ, member) = Diagnosen.Ort(voll, z.Line + 1);
                    aus.Add(new Diagnose(rel, z.Line + 1, z.Character + 1, d.Id, d.GetMessage(), d.Severity == DiagnosticSeverity.Error ? "error" : "warning", typ, member));
                }
                break;
            }
        }
        return Karten.Zuordnen(aus, lesung.Board);
    }

    /// <summary>
    /// Die eigenen Regeln (Analyzer mit CQRS-Diagnosen) über den ganzen Code-Stand (Werkbank P7.4, „Prüfen = Bauen“): je Domänen-Projekt seine
    /// Analyzer auf seiner warmen Compilation, dazu die Gesamtbild-Regeln der Laufzeit (sie brauchen alle Domänen-Projekte) auf einer leeren
    /// Compilation, die die Domänen-Compilations referenziert — so wie die Laufzeit sie sieht, ohne sie zu übersetzen (E7). Unabhängig vom
    /// inkrementellen Bauen: ein unverändertes Projekt druckt beim Bauen keine Warnungen mehr, hier stehen sie immer.
    /// </summary>
    public async Task<IReadOnlyList<Diagnose>> RegelBefundeAsync(CancellationToken ct = default)
    {
        var lesung = await LiesAsync(ct);
        var aus = new List<Diagnose>();
        var domaenen = lesung.Lage.Analyse.Where(a => lesung.Lage.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")).ToList();
        foreach (var (projekt, comp) in domaenen)
        {
            var regeln = Regeln(projekt);
            if (regeln.IsEmpty) continue;
            foreach (var d in await comp.WithAnalyzers(regeln, projekt.AnalyzerOptions).GetAnalyzerDiagnosticsAsync(ct))
                if (d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning && d.Location.IsInSource)
                {
                    var voll = d.Location.SourceTree!.FilePath;
                    var z = d.Location.GetLineSpan().StartLinePosition;
                    var (typ, member) = Diagnosen.Ort(voll, z.Line + 1);
                    aus.Add(new Diagnose(Path.GetRelativePath(_root, voll).Replace('\\', '/'), z.Line + 1, z.Character + 1, d.Id, d.GetMessage(),
                        d.Severity == DiagnosticSeverity.Error ? "error" : "warning", typ, member));
                }
        }
        // Gesamtbild: die Analyzer der Laufzeit auf einer leeren Compilation über den Domänen-Compilations.
        if (_solution?.GetProject(_kalt!.Laufzeit.Id) is { } laufzeit && Regeln(laufzeit) is { IsEmpty: false } gesamt && domaenen.Count > 0)
        {
            var refs = domaenen.SelectMany(a => a.Compilation.References).Concat(domaenen.Select(a => (MetadataReference)a.Compilation.ToMetadataReference()))
                .GroupBy(r => r.Display ?? r.ToString()).Select(g => g.First()).ToList();
            var bild = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Gesamtbild", [], refs,
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            foreach (var d in await bild.WithAnalyzers(gesamt, laufzeit.AnalyzerOptions).GetAnalyzerDiagnosticsAsync(ct))
                if (d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                {
                    var text = d.GetMessage();
                    var typ = text.StartsWith('\'') && text.IndexOf('\'', 1) is var e and > 1 ? text[1..e] : null;
                    aus.Add(new Diagnose("", 0, 0, d.Id, text, d.Severity == DiagnosticSeverity.Error ? "error" : "warning", typ, null));
                }
        }
        return Karten.Zuordnen(aus, lesung.Board);
    }

    /// <summary>Die Analyzer eines Projekts, die eigene Regeln (CQRS…) melden — fremde (SDK, xUnit …) laufen hier nicht.</summary>
    private static ImmutableArray<DiagnosticAnalyzer> Regeln(Project p) => p.AnalyzerReferences.SelectMany(r => r.GetAnalyzers(p.Language))
        .Where(a => a.SupportedDiagnostics.Any(d => d.Id.StartsWith("CQRS", StringComparison.Ordinal))).ToImmutableArray();

    /// <summary>
    /// Prüfen = Bauen, ohne zu schreiben (Werkbank P7.4, konzept-werkbank A.5): die Datei <paramref name="pfad"/> probeweise mit <paramref name="neuText"/> in den
    /// warmen Stand setzen, ihr Projekt übersetzen (mit seinen echten Generatoren) und die Befunde der Datei holen — Compiler UND die Analyzer
    /// des Projekts (CQRS050, CQRS066/067, CQRS073 ff. …). Es zählen nur NEUE Fehler gegenüber dem unveränderten Stand; der Fork wird verworfen.
    /// </summary>
    public async Task<IReadOnlyList<string>> PruefeProbeweiseAsync(string pfad, string neuText, CancellationToken ct = default)
    {
        var stand = await StandAsync(ct);
        var voll = Path.GetFullPath(pfad);
        var docId = stand.GetDocumentIdsWithFilePath(voll).FirstOrDefault();
        if (docId == null) return [$"{Path.GetRelativePath(_root, voll)} gehört zu keinem Projekt der Solution."];
        var vorher = await FehlerAsync(stand.GetDocument(docId)!, ct);
        var nachher = await FehlerAsync(stand.WithDocumentText(docId, SourceText.From(neuText)).GetDocument(docId)!, ct);
        return nachher.Where(f => !vorher.Contains(f)).Distinct().Take(20).ToList();
    }

    /// <summary>Fehler einer Datei: Compiler (gebunden wird nur sie) und die Analyzer ihres Projekts auf ihrem Syntaxbaum — ohne Zeilen, damit
    /// derselbe Fehler vorher und nachher gleich zählt.</summary>
    private static async Task<HashSet<string>> FehlerAsync(Document doc, CancellationToken ct)
    {
        var comp = (await doc.Project.GetCompilationAsync(ct))!;
        var baum = (await doc.GetSyntaxTreeAsync(ct))!;
        var model = comp.GetSemanticModel(baum);
        var diags = model.GetDiagnostics(cancellationToken: ct).AsEnumerable();
        var analyzer = Regeln(doc.Project);
        if (!analyzer.IsEmpty)
        {
            var mit = comp.WithAnalyzers(analyzer, doc.Project.AnalyzerOptions);
            diags = diags.Concat(await mit.GetAnalyzerSemanticDiagnosticsAsync(model, null, ct)).Concat(await mit.GetAnalyzerSyntaxDiagnosticsAsync(baum, ct));
        }
        return diags.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Id}: {d.GetMessage()}").ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Die Lese-Befunde des zuletzt gelesenen Boards als Befunde an der Karte (wie Compiler-Befunde; Schwere „warning“).</summary>
    public IReadOnlyList<Diagnose> Befunde() => _letzte == null ? [] : _letzte.Befunde.Select(b => new Diagnose(
        (string?)b["datei"] ?? "", (int?)b["zeile"] ?? 0, 0, (string?)b["code"] ?? "LESEN", (string?)b["text"] ?? "", "warning", null, null,
        (string?)b["karte"])).ToList();

    public object Zustand() => new
    {
        geladen = Geladen, laedt = _laden is { IsCompleted: false }, fehler = Fehler, offen = _geaendert.Count, kaltNeu = _kaltNeu,
        routing = _letzte?.Routing.Source,
        karten = _letzte == null ? 0 : _letzte.Board.Where(kv => kv.Value is JsonArray && !kv.Key.StartsWith("lese", StringComparison.Ordinal) && kv.Key != "diagnosen")
            .Sum(kv => ((JsonArray)kv.Value!).Count),
        befunde = _letzte?.Befunde.Count() ?? 0,
        messungen = Messungen.TakeLast(20),
    };

    private async Task LadeKaltAsync()
    {
        await _sperre.WaitAsync();
        try
        {
            Fehler = null;
            var uhr = Stopwatch.StartNew();
            var sln = Directory.GetFiles(_root, "*.sln").FirstOrDefault() ?? throw new FileNotFoundException($"Keine .sln in {_root}.");
            _workspace?.Dispose();
            _geaendert.Clear();
            _workspace = MSBuildWorkspace.Create();
            var solution = await _workspace.OpenSolutionAsync(sln);
            var lage = await Projektlage.ErmittleAsync(solution, z => Console.WriteLine("[Arbeitsbereich]" + z));
            var lesung = await Lesen.LiesAsync(solution, lage, _root, uebersetzen: uhr.Elapsed);
            (_solution, _kalt, _zusammensetzung) = (solution, lage, lesung.CompositionRoot);
            Merke(lesung, "kalt", 0);
            Beobachte();
            Console.WriteLine($"[Arbeitsbereich] kalt geladen in {uhr.Elapsed.TotalSeconds:0.0} s ({lesung.Routing.Source}).");
        }
        catch (Exception e)
        {
            Fehler = e.Message;
            Console.WriteLine($"[Arbeitsbereich] ✗ {e.Message}");
        }
        finally { _sperre.Release(); }
    }

    private void Merke(Lesung lesung, string art, int dateien)
    {
        _letzte = lesung;
        var json = lesung.BoardJson;
        if (json != _boardJson)
        {
            _boardJson = json;
            try { File.WriteAllText(Path.Combine(_root, "domain-model.json"), json); }
            catch (IOException e) { Console.WriteLine($"[Arbeitsbereich] domain-model.json nicht geschrieben: {e.Message}"); }
        }
        var m = new LeseMessung(DateTimeOffset.Now, art, dateien, (long)lesung.Uebersetzen.TotalMilliseconds, (long)lesung.Extrahieren.TotalMilliseconds,
            lesung.Phasen.ToDictionary(p => p.Phase, p => (long)p.Dauer.TotalMilliseconds));
        lock (_messungen)
        {
            _messungen.Add(m);
            if (_messungen.Count > 200) _messungen.RemoveAt(0);
        }
        Console.WriteLine($"[Arbeitsbereich] {art}: {dateien} Datei(en), Übersetzen {m.UebersetzenMs} ms, Lesen {m.LesenMs} ms");
        _melde?.Invoke("gelesen", new { art, dateien, uebersetzenMs = m.UebersetzenMs, lesenMs = m.LesenMs, befunde = lesung.Befunde.Count() });
    }

    /// <summary>Eine geänderte Datei in den Stand übernehmen: Text ersetzen, neu anlegen (SDK-Projekte nehmen alles unter ihrem Ordner) oder entfernen.</summary>
    private static async Task<Solution> UebernimmAsync(Solution stand, string pfad)
    {
        var ids = stand.GetDocumentIdsWithFilePath(pfad);
        if (!File.Exists(pfad))
        {
            foreach (var id in ids) stand = stand.RemoveDocument(id);
            return stand;
        }
        SourceText text;
        try { using var s = File.OpenRead(pfad); text = SourceText.From(s); }
        catch (IOException) { return stand; }   // wird gerade geschrieben — der Wächter meldet das Ende noch einmal
        if (ids.Length > 0)
        {
            // Gleicher Inhalt (z. B. ein Build berührt die Datei nur) → kein neuer Stand, nichts wird neu übersetzt.
            foreach (var id in ids)
                if (stand.GetDocument(id) is { } d && !(await d.GetTextAsync()).ContentEquals(text))
                    stand = stand.WithDocumentText(id, text, PreservationMode.PreserveIdentity);
            return stand;
        }
        var projekt = stand.Projects
            .Where(p => p.FilePath != null && pfad.StartsWith(Path.GetDirectoryName(p.FilePath)! + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(p => p.FilePath!.Length).FirstOrDefault();
        if (projekt == null) return stand;
        var ordner = Path.GetRelativePath(Path.GetDirectoryName(projekt.FilePath)!, Path.GetDirectoryName(pfad)!)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Where(x => x != ".").ToArray();
        return stand.AddDocument(DocumentId.CreateNewId(projekt.Id), Path.GetFileName(pfad), text, ordner, pfad);
    }

    private void Beobachte()
    {
        if (_waechter != null) return;
        _waechter = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        void Melde(string? pfad)
        {
            if (pfad == null || Ignoriert(pfad)) return;
            if (pfad.EndsWith(".cs", StringComparison.Ordinal) || pfad.EndsWith(".py", StringComparison.Ordinal) || IstUmgebung(pfad)) _geaendert[pfad] = 0;
            else if (pfad.EndsWith(".csproj", StringComparison.Ordinal) || pfad.EndsWith(".props", StringComparison.Ordinal)
                     || pfad.EndsWith(".targets", StringComparison.Ordinal) || pfad.EndsWith(".sln", StringComparison.Ordinal)) _kaltNeu = true;
        }
        _waechter.Changed += (_, e) => Melde(e.FullPath);
        _waechter.Created += (_, e) => Melde(e.FullPath);
        _waechter.Deleted += (_, e) => Melde(e.FullPath);
        _waechter.Renamed += (_, e) => { Melde(e.OldFullPath); Melde(e.FullPath); };
        _waechter.EnableRaisingEvents = true;
    }

    /// <summary>Eine Umgebungs-Datei (<c>umgebungen/umgebung.&lt;name&gt;.json</c>) — kein Workspace-Dokument, aber Teil des Boards.</summary>
    private bool IstUmgebung(string pfad) =>
        pfad.EndsWith(".json", StringComparison.Ordinal)
        && Path.GetFullPath(Path.GetDirectoryName(pfad) ?? "") == Path.GetFullPath(Path.Combine(_root, Abstractions.Projektstruktur.UmgebungsOrdner));

    /// <summary>Build-Ausgaben, Git und Werkzeug-Ordner gehören nicht zum Code.</summary>
    private static bool Ignoriert(string pfad)
    {
        var s = Path.DirectorySeparatorChar;
        return pfad.Contains($"{s}bin{s}", StringComparison.Ordinal) || pfad.Contains($"{s}obj{s}", StringComparison.Ordinal)
            || pfad.Contains($"{s}.git{s}", StringComparison.Ordinal) || pfad.Contains($"{s}node_modules{s}", StringComparison.Ordinal)
            || pfad.Contains($"{s}.werkbank{s}", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _waechter?.Dispose();
        _workspace?.Dispose();
    }
}
