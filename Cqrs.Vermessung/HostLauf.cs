using System.Diagnostics;
using System.Text;

namespace Cqrs.Vermessung;

/// <summary>
/// Ein echter Host als Prozess (<c>dotnet &lt;Host&gt;.dll --umgebung &lt;name&gt;</c>, Release-Build), wie die Werkbank ihn startet
/// (<c>Werkbank/Server/HostProzess.cs</c>): bereit, wenn <c>/health</c> antwortet; Ende per SIGTERM, im Störungslauf hart (SIGKILL).
/// Die Ausgabe geht in eine Log-Datei (nicht in die Messung).
/// </summary>
internal sealed class HostLauf : IAsyncDisposable
{
    private readonly string _dll;
    private readonly string _umgebung;
    private readonly int _einblickPort;
    private readonly string _logDatei;
    private Process? _prozess;
    private StreamWriter? _log;

    public HostLauf(string dll, string umgebung, int einblickPort, string logDatei)
    {
        _dll = dll;
        _umgebung = umgebung;
        _einblickPort = einblickPort;
        _logDatei = logDatei;
    }

    /// <summary>Alle gestarteten Prozesse (Hosts): endet der Treiber unerwartet (Strg-C, Abbruch), werden sie mitbeendet.</summary>
    public static readonly System.Collections.Concurrent.ConcurrentBag<Process> Kinder = new();

    static HostLauf()
    {
        void Aufraeumen() { foreach (var p in Kinder) try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { } }
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Aufraeumen();
        Console.CancelKeyPress += (_, _) => Aufraeumen();
    }

    public int Pid => _prozess?.Id ?? 0;
    public Process? Prozess => _prozess;

    public async Task StarteAsync(TimeSpan? frist = null)
    {
        if (!File.Exists(_dll)) throw new FileNotFoundException($"Host nicht gebaut: {_dll} (scripts/vermessung-e2e.sh baut Release).");
        var psi = new ProcessStartInfo("dotnet", $"\"{_dll}\" --umgebung {_umgebung}")
        {
            WorkingDirectory = Path.GetDirectoryName(_dll)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        Directory.CreateDirectory(Path.GetDirectoryName(_logDatei)!);
        _log ??= new StreamWriter(_logDatei, append: true, Encoding.UTF8) { AutoFlush = true };   // ein Neustart schreibt weiter in dieselbe Datei
        await _log.WriteLineAsync($"===== Start {DateTimeOffset.Now:O} {_dll} --umgebung {_umgebung}");
        _prozess = Process.Start(psi)!;
        Kinder.Add(_prozess);
        var log = _log;
        _prozess.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
        _prozess.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine("ERR " + e.Data); };
        _prozess.BeginOutputReadLine();
        _prozess.BeginErrorReadLine();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var sw = Stopwatch.StartNew();
        var grenze = frist ?? TimeSpan.FromSeconds(120);
        while (sw.Elapsed < grenze)
        {
            if (_prozess.HasExited) throw new InvalidOperationException($"Host {_umgebung} beendet beim Start (Code {_prozess.ExitCode}) — siehe {_logDatei}");
            try
            {
                var r = await http.GetAsync($"http://localhost:{_einblickPort}/health");
                if (r.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(250);
        }
        throw new TimeoutException($"Host {_umgebung} nicht bereit nach {grenze.TotalSeconds:0} s — siehe {_logDatei}");
    }

    /// <summary>Hart beenden (Störungslauf): kein Herunterfahren, kein Snapshot, offene Signale verloren.</summary>
    public void Toete()
    {
        if (_prozess is { HasExited: false }) _prozess.Kill(entireProcessTree: true);
        _prozess?.WaitForExit(10_000);
        _log?.WriteLine($"===== SIGKILL {DateTimeOffset.Now:O}");
    }

    public async Task StoppeAsync()
    {
        if (_prozess is { HasExited: false })
        {
            Werkzeuge.Laufe("kill", $"-TERM {_prozess.Id}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await _prozess.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { _prozess.Kill(entireProcessTree: true); }
        }
        _log?.WriteLine($"===== Ende {DateTimeOffset.Now:O}");
    }

    public async ValueTask DisposeAsync()
    {
        await StoppeAsync();
        _log?.Dispose();
    }
}

/// <summary>
/// Schreibt RSS und CPU der beobachteten Prozesse jede Sekunde mit (<c>host.csv</c>) und behält die Proben für die Auswertung je Fenster.
/// CPU in Prozent EINES Kerns (Δ Prozessorzeit / Δ Wanduhr).
/// </summary>
internal sealed class Ressourcen : IAsyncDisposable
{
    public sealed record Probe(DateTimeOffset Zeit, string Rolle, int Pid, double RssMb, double CpuProzent);

    private readonly StreamWriter _csv;
    private readonly List<Probe> _proben = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _schleife;
    private readonly Dictionary<string, Func<Process?>> _prozesse = new();
    private readonly Dictionary<int, (DateTimeOffset Zeit, TimeSpan Cpu)> _vorher = new();
    private string _abschnitt = "";

    public Ressourcen(string csvDatei)
    {
        _csv = new StreamWriter(csvDatei, append: false, Encoding.UTF8) { AutoFlush = true };
        _csv.WriteLine("zeit_utc,abschnitt,rolle,pid,rss_mb,cpu_prozent");
        _schleife = Task.Run(SchleifeAsync);
    }

    public void Beobachte(string rolle, Func<Process?> prozess) { lock (_prozesse) _prozesse[rolle] = prozess; }
    public void Vergiss(string rolle) { lock (_prozesse) _prozesse.Remove(rolle); }
    public void Abschnitt(string name) => _abschnitt = name;

    private async Task SchleifeAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            List<(string, Func<Process?>)> liste;
            lock (_prozesse) liste = _prozesse.Select(kv => (kv.Key, kv.Value)).ToList();
            var jetzt = DateTimeOffset.UtcNow;
            foreach (var (rolle, f) in liste)
            {
                try
                {
                    var p = f();
                    if (p == null || p.HasExited) continue;
                    p.Refresh();
                    var cpu = p.TotalProcessorTime;
                    var rss = p.WorkingSet64 / 1048576.0;
                    double prozent = double.NaN;
                    if (_vorher.TryGetValue(p.Id, out var v) && (jetzt - v.Zeit).TotalSeconds > 0.2)
                        prozent = (cpu - v.Cpu).TotalSeconds / (jetzt - v.Zeit).TotalSeconds * 100;
                    _vorher[p.Id] = (jetzt, cpu);
                    if (double.IsNaN(prozent)) continue;
                    var probe = new Probe(jetzt, rolle, p.Id, rss, prozent);
                    lock (_proben) _proben.Add(probe);
                    _csv.WriteLine(FormattableString.Invariant($"{jetzt:O},{_abschnitt},{rolle},{p.Id},{rss:F1},{prozent:F1}"));
                }
                catch { /* Prozess gerade beendet */ }
            }
            try { await Task.Delay(1000, _cts.Token); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Mittlere CPU und höchster RSS einer Rolle im Fenster.</summary>
    public (double CpuProzent, double RssMb) Fenster(string rolle, DateTimeOffset von, DateTimeOffset bis)
    {
        List<Probe> p;
        lock (_proben) p = _proben.Where(x => x.Rolle == rolle && x.Zeit >= von && x.Zeit <= bis).ToList();
        return p.Count == 0 ? (double.NaN, double.NaN) : (p.Average(x => x.CpuProzent), p.Max(x => x.RssMb));
    }

    /// <summary>
    /// Speicher am Fensterende (letzte Probe) und der Höchststand desselben Prozesses seit seinem Start — der Maßstab der Passivierung (PF-5):
    /// nach den Blöcken soll der RSS deutlich unter den Höchststand fallen.
    /// </summary>
    public (double EndeMb, double HoechstMb) Speicher(string rolle, DateTimeOffset von, DateTimeOffset bis)
    {
        lock (_proben)
        {
            var letzte = _proben.LastOrDefault(x => x.Rolle == rolle && x.Zeit >= von && x.Zeit <= bis);
            if (letzte is null) return (double.NaN, double.NaN);
            return (letzte.RssMb, _proben.Where(x => x.Pid == letzte.Pid && x.Zeit <= bis).Max(x => x.RssMb));
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _schleife; } catch { }
        _csv.Dispose();
    }
}
