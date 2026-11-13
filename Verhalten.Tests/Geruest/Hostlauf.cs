using System.Diagnostics;
using System.Text;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Ein echter Host als Prozess (<c>dotnet &lt;Host&gt;.dll --umgebung &lt;name&gt;</c>, wie Werkbank und Vermessung ihn starten): bereit,
/// wenn <c>/health</c> antwortet; Ende per SIGTERM. Die Ausgabe geht in eine Datei; scheitert der Start, steht ihr Ende in der Meldung.
/// Ports je Prozess über Umgebungsvariablen — mehrere Knoten teilen eine Umgebungs-Datei. Wie im Betrieb läuft neben jedem Host der Client, der auf
/// die Fakten seiner Anwendung reagiert (der ReaktionsWorker, Schritt 6: ein Decide gibt nur Fakten aus) — mit dem Stand der Umgebung, so macht er nach
/// einem Neustart des Hosts weiter, wo er stand —, und auf seinem Rechner der RECHNERDIENST (Host.Rechner als Prozess), der die Quellen der
/// App-Instanzen dieses Rechners trägt und über die Leitung dieses Hosts hineingibt.
/// </summary>
public sealed class Hostlauf : IAsyncDisposable
{
    private readonly Process _prozess;
    private readonly StreamWriter _log;
    private readonly string _logDatei;
    private readonly CancellationTokenSource _ende = new();
    private Task? _reaktionen;
    private Process? _rechnerdienst;

    private Hostlauf(Process prozess, StreamWriter log, string logDatei, int grpcPort, int einblickPort)
    {
        _prozess = prozess;
        _log = log;
        _logDatei = logDatei;
        GrpcPort = grpcPort;
        EinblickPort = einblickPort;
    }

    public int GrpcPort { get; }
    public int EinblickPort { get; }
    public string GrpcAdresse => $"http://localhost:{GrpcPort}";

    /// <summary>Der Rechnername des ersten Hosts — die Instanzen mit Zustand oder Quelle laufen auf ihm (Testumgebung).</summary>
    public const string Rechner1 = "rechner-1";

    /// <summary>Die Rechner des Pools der Testumgebung — so viele Hosts kann ein Test zugleich starten (jeder mit seinem Rechner).</summary>
    public static readonly string[] Rechnernamen = [Rechner1, "rechner-2", "rechner-3", "rechner-4"];

    /// <summary>Startet einen Host; <paramref name="rechner"/> ist sein Rechner im Pool (<c>CQRS_RECHNER</c>; ein Neustart derselben Maschine trägt denselben).</summary>
    public static async Task<Hostlauf> StarteAsync(Testumgebung u, string rolle = "host", string rechner = Rechner1, bool mitReaktionen = true)
    {
        var dll = HostDll(u.Anwendung == Anwendung.Beispiel ? "Host.Grpc" : "Host.Pruefdomaene");
        var grpc = Testumgebung.FreierPort();
        var einblick = Testumgebung.FreierPort();
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\" --umgebung {u.Name}")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        psi.Environment["CQRS_UMGEBUNGEN"] = u.Verzeichnis;
        psi.Environment["Transport__GrpcPort"] = grpc.ToString();
        psi.Environment["Transport__EinblickPort"] = einblick.ToString();
        psi.Environment[Abstractions.Rechnerregeln.Variable] = rechner;
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        var logDatei = Path.Combine(u.Verzeichnis, rolle + ".log");
        var log = new StreamWriter(logDatei, append: true, Encoding.UTF8) { AutoFlush = true };
        var prozess = Process.Start(psi)!;
        prozess.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
        prozess.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine("ERR " + e.Data); };
        prozess.BeginOutputReadLine();
        prozess.BeginErrorReadLine();
        var lauf = new Hostlauf(prozess, log, logDatei, grpc, einblick);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(120))
        {
            if (prozess.HasExited) throw new InvalidOperationException($"{rolle} beendet beim Start (Code {prozess.ExitCode}):\n{lauf.LogEnde()}");
            try
            {
                if ((await http.GetAsync($"http://localhost:{einblick}/health")).IsSuccessStatusCode)
                {
                    if (mitReaktionen) lauf._reaktionen = Reaktionen(u, lauf.GrpcAdresse, lauf._ende.Token);
                    if (mitReaktionen && u.Anwendung == Anwendung.Beispiel) lauf._rechnerdienst = StarteRechnerdienst(u, rechner, grpc, log);
                    return lauf;
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(250);
        }
        await lauf.DisposeAsync();
        throw new TimeoutException($"{rolle} nach 120 s nicht bereit:\n{lauf.LogEnde()}");
    }

    /// <summary>
    /// Der Rechnerdienst dieses Rechners (Host.Rechner, <c>--umgebung</c>, <c>CQRS_RECHNER</c>): er verbindet sich mit dem gRPC-Port dieses Hosts (der erste
    /// Rechner des Backends ist localhost, der Port kommt wie beim Host aus <c>Transport__GrpcPort</c>). Ohne Quellen auf dem Rechner endet er gleich.
    /// </summary>
    private static Process StarteRechnerdienst(Testumgebung u, string rechner, int grpc, StreamWriter log)
    {
        var dll = HostDll("Host.Rechner");
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\" --umgebung {u.Name}")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        psi.Environment["CQRS_UMGEBUNGEN"] = u.Verzeichnis;
        psi.Environment["Transport__GrpcPort"] = grpc.ToString();
        psi.Environment[Abstractions.Rechnerregeln.Variable] = rechner;
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        var p = Process.Start(psi)!;
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine("[rechnerdienst] " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine("[rechnerdienst] ERR " + e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private void StoppeRechnerdienst()
    {
        if (_rechnerdienst is not { } p) return;
        try { if (!p.HasExited) { p.Kill(entireProcessTree: true); p.WaitForExit(10_000); } } catch (InvalidOperationException) { }
        p.Dispose();
        _rechnerdienst = null;
    }

    /// <summary>Der ReaktionsWorker der Anwendung an diesem Host (die Prüfdomäne läuft ohne Tor: kein Token).</summary>
    private static Task Reaktionen(Testumgebung u, string adresse, CancellationToken ct) => u.Anwendung == Anwendung.Beispiel
        ? Domain.Client.Worker.Reaktionslauf.LaufeAsync(adresse, Testumgebung.TokenReaktionsWorker, u.Reaktionsstand, "verhalten", ct, TimeSpan.FromMilliseconds(200))
        : Pruefdomaene.Client.Worker.Reaktionslauf.LaufeAsync(adresse, null, u.Reaktionsstand, "verhalten", ct, TimeSpan.FromMilliseconds(200));

    private async Task StoppeReaktionenAsync()
    {
        await _ende.CancelAsync();
        if (_reaktionen != null) try { await _reaktionen; } catch { /* beendet */ }
    }

    /// <summary>Die letzten Zeilen der Host-Ausgabe — für Meldungen, wenn etwas nicht eintritt.</summary>
    public string LogEnde(int zeilen = 60)
    {
        try
        {
            lock (_log)
            {
                using var f = new FileStream(_logDatei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var r = new StreamReader(f);
                var alle = r.ReadToEnd().Split('\n');
                return string.Join('\n', alle.TakeLast(zeilen));
            }
        }
        catch (IOException e) { return $"(Log nicht lesbar: {e.Message})"; }
    }

    /// <summary>
    /// Hartes Ende (SIGKILL): kein Herunterfahren, kein Abmelden im Cluster — Aktivierungen und Weckrufe im Flug gehen verloren, die anderen Knoten
    /// merken es erst, wenn Consul den Knoten austrägt.
    /// </summary>
    public void Toete()
    {
        _ende.Cancel();   // der Worker dieses Hosts endet mit ihm; der nächste Host bekommt seinen eigenen, ab dem Stand der Umgebung
        StoppeRechnerdienst();   // die Maschine fällt mit allem, was auf ihr läuft
        if (_prozess.HasExited) return;
        _prozess.Kill(entireProcessTree: true);
        _prozess.WaitForExit(10_000);
    }

    public async ValueTask DisposeAsync()
    {
        await StoppeReaktionenAsync();
        StoppeRechnerdienst();
        if (!_prozess.HasExited)
        {
            using (var kill = Process.Start("kill", $"-TERM {_prozess.Id}")) kill?.WaitForExit(5_000);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await _prozess.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { _prozess.Kill(entireProcessTree: true); }
        }
        _prozess.Dispose();
        _ende.Dispose();
        lock (_log) _log.Dispose();
    }

    /// <summary>Die gebaute Host-DLL in derselben Konfiguration wie dieser Test (Debug/Release); gebaut wird sie über die Projekt-Referenz.</summary>
    private static string HostDll(string projekt)
    {
        var basis = AppContext.BaseDirectory;
        var konfig = basis.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
        var dll = Path.Combine(Wurzel(), projekt, "bin", konfig, "net9.0", projekt + ".dll");
        if (!File.Exists(dll)) throw new FileNotFoundException($"Host nicht gebaut: {dll}");
        return dll;
    }

    public static string Wurzel()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "CqrsSolution.sln"))) return d.FullName;
        throw new DirectoryNotFoundException("CqrsSolution.sln nicht gefunden");
    }
}
