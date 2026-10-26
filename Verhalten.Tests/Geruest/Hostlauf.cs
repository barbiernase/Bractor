using System.Diagnostics;
using System.Text;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Ein echter Host als Prozess (<c>dotnet &lt;Host&gt;.dll --umgebung &lt;name&gt;</c>, wie Werkbank und Vermessung ihn starten): bereit,
/// wenn <c>/health</c> antwortet; Ende per SIGTERM. Die Ausgabe geht in eine Datei; scheitert der Start, steht ihr Ende in der Meldung.
/// Ports je Prozess über Umgebungsvariablen — mehrere Knoten teilen eine Umgebungs-Datei.
/// </summary>
public sealed class Hostlauf : IAsyncDisposable
{
    private readonly Process _prozess;
    private readonly StreamWriter _log;
    private readonly string _logDatei;

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

    public static async Task<Hostlauf> StarteAsync(Testumgebung u, string rolle = "host")
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
                if ((await http.GetAsync($"http://localhost:{einblick}/health")).IsSuccessStatusCode) return lauf;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(250);
        }
        await lauf.DisposeAsync();
        throw new TimeoutException($"{rolle} nach 120 s nicht bereit:\n{lauf.LogEnde()}");
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

    public async ValueTask DisposeAsync()
    {
        if (!_prozess.HasExited)
        {
            using (var kill = Process.Start("kill", $"-TERM {_prozess.Id}")) kill?.WaitForExit(5_000);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await _prozess.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { _prozess.Kill(entireProcessTree: true); }
        }
        _prozess.Dispose();
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
