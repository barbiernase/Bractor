using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using GraphExtractor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace SimHost.Werkbank;

/// <summary>Eine Messung des Lesens: kalt (ganze Solution) oder warm (nach Datei-Änderungen, nur Domänen-Compilations).</summary>
public sealed record LeseMessung(DateTimeOffset Zeit, string Art, int Dateien, long UebersetzenMs, long LesenMs, IReadOnlyDictionary<string, long> Phasen);

/// <summary>
/// Der warme Roslyn-Workspace der Werkbank (docs/plan-werkbank-umsetzung.md P7.1): die Solution wird EINMAL geladen (MSBuildWorkspace, kalt:
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
            foreach (var p in pfade) stand = await UebernimmAsync(stand, p);
            if (ReferenceEquals(stand, _solution)) return _letzte;   // nur Dateien außerhalb der Projekte
            var lage = await _kalt!.NurDomänenAsync(stand, ct);
            var lesung = await Lesen.LiesAsync(stand, lage, _root, _zusammensetzung, uhr.Elapsed);
            _solution = stand;
            Merke(lesung, "warm", pfade.Count);
            return lesung;
        }
        finally { _sperre.Release(); }
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
            if (pfad.EndsWith(".cs", StringComparison.Ordinal)) _geaendert[pfad] = 0;
            else if (pfad.EndsWith(".csproj", StringComparison.Ordinal) || pfad.EndsWith(".props", StringComparison.Ordinal)
                     || pfad.EndsWith(".targets", StringComparison.Ordinal) || pfad.EndsWith(".sln", StringComparison.Ordinal)) _kaltNeu = true;
        }
        _waechter.Changed += (_, e) => Melde(e.FullPath);
        _waechter.Created += (_, e) => Melde(e.FullPath);
        _waechter.Deleted += (_, e) => Melde(e.FullPath);
        _waechter.Renamed += (_, e) => { Melde(e.OldFullPath); Melde(e.FullPath); };
        _waechter.EnableRaisingEvents = true;
    }

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
