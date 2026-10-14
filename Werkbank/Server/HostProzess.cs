using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Werkbank.Server;

/// <summary>Ein Boot-Fehler des Hosts, möglichst an einer Karte (<c>funktion:X</c>, <c>quelle:X</c>, <c>host:Abschnitt</c>).</summary>
public sealed record BootMeldung(string Text, string? Karte);

/// <summary>
/// Der Laufzeit-Host als Prozess (docs/konzept-werkbank.md §5 Schritt 5): <c>dotnet Host.Grpc.dll --umgebung &lt;name&gt;</c> aus dem
/// Build-Ausgang, <c>CQRS_BRANCH</c> = Branch der Werkbank. Starten ersetzt einen laufenden Host; bereit ist er, wenn <c>/health</c> auf
/// dem Einblick-Port antwortet. Boot-Fehler kommen als Zeile <c>CQRS-BOOT {json}</c> (bzw. als unbehandelte Ausnahme) zurück.
/// </summary>
public sealed class HostProzess(string slnRoot)
{
    private Process? _p;
    private string? _umgebung;
    private DateTimeOffset? _seit;
    private readonly ConcurrentQueue<string> _log = new();
    private readonly List<BootMeldung> _boot = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public bool Laeuft => _p is { HasExited: false };

    /// <summary>Seit wann der laufende Host läuft (ein neuer Start = ein neuer Lauf, die Spur wird neu gespiegelt).</summary>
    public DateTimeOffset? Seit => Laeuft ? _seit : null;

    public object Zustand() => new
    {
        laeuft = Laeuft, umgebung = _umgebung, pid = Laeuft ? _p!.Id : (int?)null, seit = _seit,
        exitCode = _p is { HasExited: true } p ? p.ExitCode : (int?)null, boot = Boot(),
    };

    public IReadOnlyList<BootMeldung> Boot() { lock (_boot) return _boot.ToList(); }

    public IReadOnlyList<string> Log(int max = 300) => _log.Reverse().Take(max).Reverse().ToList();

    private string? _ziel;

    /// <summary>
    /// Der Build-Ausgang des Hosts (<c>TargetPath</c>), gefragt beim Build-System statt geraten (Konfiguration, Zielframework, Ausgabeordner
    /// stehen im Projekt) — einmal je Werkbank-Lauf.
    /// </summary>
    private async Task<string?> ZielAsync(string host, CancellationToken ct)
    {
        if (_ziel != null) return _ziel;
        var r = await Prozesse.LaufeAsync("dotnet", ["msbuild", Path.Combine(host, host + ".csproj"), "-getProperty:TargetPath", "-nologo"], slnRoot,
            zeitlimit: TimeSpan.FromMinutes(1), ct: ct);
        var pfad = r.Ok ? r.Zeilen.Select(z => z.Trim()).LastOrDefault(z => z.EndsWith(".dll", StringComparison.Ordinal)) : null;
        return _ziel = pfad;
    }

    public async Task<(bool Ok, IReadOnlyList<BootMeldung> Boot)> StarteAsync(string umgebung, int einblickPort, string branch, CancellationToken ct)
    {
        await StoppeAsync();
        lock (_boot) _boot.Clear();
        _log.Clear();
        var host = Abstractions.Projektstruktur.ServerHost;
        var dll = await ZielAsync(host, ct);
        if (dll == null || !File.Exists(dll)) { lock (_boot) _boot.Add(new BootMeldung($"{host} ist nicht gebaut (⟳ baut ihn).", null)); return (false, Boot()); }
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(dll)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(dll);
        psi.ArgumentList.Add("--umgebung");
        psi.ArgumentList.Add(umgebung);
        psi.Environment["CQRS_BRANCH"] = branch;
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        void Nimm(string? z)
        {
            if (z == null) return;
            _log.Enqueue(z);
            while (_log.Count > 2000) _log.TryDequeue(out _);
            if (z.StartsWith("CQRS-BOOT ", StringComparison.Ordinal))
                try
                {
                    var j = JsonDocument.Parse(z["CQRS-BOOT ".Length..]).RootElement;
                    lock (_boot) _boot.Add(new BootMeldung(j.GetProperty("text").GetString() ?? z, j.TryGetProperty("karte", out var k) ? k.GetString() : null));
                }
                catch (JsonException) { lock (_boot) _boot.Add(new BootMeldung(z, null)); }
        }
        p.OutputDataReceived += (_, e) => Nimm(e.Data);
        p.ErrorDataReceived += (_, e) => Nimm(e.Data);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        _p = p;
        _umgebung = umgebung;
        _seit = DateTimeOffset.UtcNow;

        var bis = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < bis && !ct.IsCancellationRequested)
        {
            if (p.HasExited)
            {
                p.WaitForExit();   // Ausgabe-Puffer leeren — die CQRS-BOOT-Zeile kommt oft als letzte
                lock (_boot)
                    if (_boot.Count == 0)
                        _boot.Add(new BootMeldung(string.Join("\n", Log(40).Where(z => z.Contains("Exception", StringComparison.Ordinal) || z.Contains("Fehler", StringComparison.Ordinal)).Take(8))
                            is { Length: > 0 } t ? t : $"Host beendet (Exit {p.ExitCode})", null));
                return (false, Boot());
            }
            if (einblickPort > 0)
                try { using var r = await Http.GetAsync($"http://localhost:{einblickPort}/health", ct); return (true, Boot()); }
                catch (Exception) when (!ct.IsCancellationRequested) { /* noch nicht bereit */ }
            await Task.Delay(500, ct);
        }
        if (!p.HasExited && einblickPort > 0) lock (_boot) _boot.Add(new BootMeldung($"Host antwortet nach 90 s nicht auf /health (Port {einblickPort}).", "host:Transport"));
        return (einblickPort == 0 && !p.HasExited, Boot());
    }

    /// <summary>Hält den Host an: erst SIGTERM (geordnetes Herunterfahren), nach 15 s hart.</summary>
    public async Task StoppeAsync()
    {
        var p = _p;
        if (p is not { HasExited: false }) return;
        try { Process.Start("kill", ["-TERM", p.Id.ToString()])?.WaitForExit(2000); } catch { /* kein kill */ }
        var bis = DateTime.UtcNow.AddSeconds(15);
        while (!p.HasExited && DateTime.UtcNow < bis) await Task.Delay(200);
        if (!p.HasExited) try { p.Kill(entireProcessTree: true); } catch { /* schon weg */ }
    }
}
