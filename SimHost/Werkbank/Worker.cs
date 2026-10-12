using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;

namespace SimHost.Werkbank;

/// <summary>
/// Externe Funktions-Worker als Prozesse der Werkbank (docs/konzept-werkbank.md §6, Werkbank P5.5, Plan E10): „▶ Worker starten" an einer
/// Funktion mit Laufort <c>Extern</c> startet <c>Funktionen:&lt;F&gt;:Worker</c> der gewählten Umgebung — <c>Python Skript Argumente…</c> mit
/// <c>CQRS_GRPC_HOST</c>/<c>CQRS_GRPC_PORT</c>/<c>CQRS_AKTEUR_TOKEN</c>. Der Worker meldet sich wie jeder externe Ausführer am gRPC-Port an und
/// holt Aufträge beim Vermittler; die Werkbank sammelt nur seine Ausgabe.
/// </summary>
public sealed class Worker(string slnRoot)
{
    private sealed record Lauf(Process Prozess, ConcurrentQueue<string> Log, DateTimeOffset Seit, string Umgebung);

    private readonly ConcurrentDictionary<string, Lauf> _laeufe = new(StringComparer.Ordinal);

    /// <summary>Je Funktion der Umgebung: Laufort, ob ein Worker konfiguriert ist und ob er läuft.</summary>
    public IReadOnlyList<object> Liste(UmgebungKonfig u) => u.Funktionen.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
    {
        _laeufe.TryGetValue(f.Key, out var l);
        var laeuft = l is { Prozess.HasExited: false };
        return (object)new
        {
            funktion = f.Key,
            laufort = f.Value.Laufort.ToString(),
            worker = f.Value.Worker is { Skript.Length: > 0 } w ? $"{Path.GetFileName(w.Skript)} {string.Join(' ', w.Argumente)}".Trim() : null,
            laeuft,
            pid = laeuft ? l!.Prozess.Id : (int?)null,
            seit = l?.Seit,
            exitCode = l is { Prozess.HasExited: true } ? l.Prozess.ExitCode : (int?)null,
            zuletzt = l?.Log.LastOrDefault(),
        };
    }).ToList();

    public IReadOnlyList<string> Log(string funktion, int max = 200)
        => _laeufe.TryGetValue(funktion, out var l) ? l.Log.Reverse().Take(max).Reverse().ToList() : [];

    public async Task<object> StarteAsync(string funktion, UmgebungKonfig u, bool hostLaeuft)
    {
        if (!u.Funktionen.TryGetValue(funktion, out var k)) throw new InvalidOperationException($"{funktion} ist in {u.Umgebung.Name} nicht gebunden.");
        if (k.Laufort != FunktionsLaufort.Extern)
            throw new InvalidOperationException($"Laufort von {funktion} in {u.Umgebung.Name} ist {k.Laufort} — erst auf Extern stellen (🖥 Funktionen) und ⟳.");
        if (k.Worker is not { Skript.Length: > 0 } w) throw new InvalidOperationException($"Funktionen:{funktion}:Worker fehlt in {u.Umgebung.Name} (Skript, Argumente, Token).");
        if (!hostLaeuft) throw new InvalidOperationException("Der Host läuft nicht — ⟳ startet ihn; der Worker meldet sich an seinem gRPC-Port an.");
        await StoppeAsync(funktion);

        var python = w.Python is { Length: > 0 } p ? p
            : Environment.GetEnvironmentVariable("CQRS_PYTHON") is { Length: > 0 } cp ? cp
            : File.Exists("/opt/miniconda3/envs/gpu_env/bin/python") ? "/opt/miniconda3/envs/gpu_env/bin/python" : "python3";
        var skript = Path.IsPathRooted(w.Skript) ? w.Skript : Path.Combine(slnRoot, w.Skript);
        if (!File.Exists(skript)) throw new FileNotFoundException($"Worker-Skript fehlt: {skript}");
        var psi = new ProcessStartInfo(python) { WorkingDirectory = Path.GetDirectoryName(skript)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(skript);
        foreach (var a in w.Argumente) psi.ArgumentList.Add(a);
        psi.Environment["CQRS_GRPC_HOST"] = "127.0.0.1";
        psi.Environment["CQRS_GRPC_PORT"] = u.Transport.GrpcPort.ToString();
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        if (w.Token.Length > 0) psi.Environment["CQRS_AKTEUR_TOKEN"] = w.Token;

        var log = new ConcurrentQueue<string>();
        void Nimm(string? z)
        {
            if (z == null) return;
            log.Enqueue(z);
            while (log.Count > 1000) log.TryDequeue(out _);
        }
        var prozess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        prozess.OutputDataReceived += (_, e) => Nimm(e.Data);
        prozess.ErrorDataReceived += (_, e) => Nimm(e.Data);
        prozess.Start();
        prozess.BeginOutputReadLine();
        prozess.BeginErrorReadLine();
        _laeufe[funktion] = new Lauf(prozess, log, DateTimeOffset.UtcNow, u.Umgebung.Name);
        Nimm($"$ {python} {skript} {string.Join(' ', w.Argumente)}  (gRPC 127.0.0.1:{u.Transport.GrpcPort}{(w.Token.Length > 0 ? ", Token " + w.Token : "")})");

        // Kurz warten: ein Skript, das sofort scheitert (Import, Argumente), soll als Fehler zurückkommen, nicht als „läuft".
        await Task.Delay(1500);
        if (prozess.HasExited)
            return new { ok = false, funktion, fehler = $"Worker beendet (Exit {prozess.ExitCode})", log = Log(funktion, 15) };
        return new { ok = true, funktion, pid = prozess.Id };
    }

    public async Task StoppeAsync(string funktion)
    {
        if (!_laeufe.TryRemove(funktion, out var l) || l.Prozess.HasExited) return;
        try { l.Prozess.Kill(entireProcessTree: true); } catch { /* schon weg */ }
        await l.Prozess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { });
    }

    public Task StoppeAlleAsync() => Task.WhenAll(_laeufe.Keys.ToList().Select(StoppeAsync));
}
