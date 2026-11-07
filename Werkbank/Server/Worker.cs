using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Python-Clients als Prozesse der Werkbank (CLAUDE.md W6, docs/06 §6.5): „▶ Worker starten" startet den <c>Worker</c> eines Clients der Umgebung
/// (<c>Clients:&lt;Name&gt;:Worker</c>) — <c>Python Skript Argumente…</c> mit <c>CQRS_GRPC_HOST</c>/<c>CQRS_GRPC_PORT</c>/<c>CQRS_AKTEUR_TOKEN</c>. Er meldet
/// sich wie jeder Client am gRPC-Port an und nimmt die Arbeit der Akteure an, die er verkörpert; die Werkbank sammelt nur seine Ausgabe.
/// </summary>
public sealed class Worker(string slnRoot)
{
    private sealed record Lauf(Process Prozess, ConcurrentQueue<string> Log, DateTimeOffset Seit, string Umgebung);

    private readonly ConcurrentDictionary<string, Lauf> _laeufe = new(StringComparer.Ordinal);

    /// <summary>Die Clients der Umgebung mit einem Worker-Prozess (Schlüssel = Client-Name).</summary>
    public static IEnumerable<KeyValuePair<string, ClientKonfig>> Clients(UmgebungKonfig u) => u.Clients.Where(c => c.Value.Worker is not null);

    /// <summary>Je Client mit Worker: was gestartet würde und ob er läuft („funktion“ nennt den Client).</summary>
    public IReadOnlyList<object> Liste(UmgebungKonfig u) => Clients(u).OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
    {
        _laeufe.TryGetValue(f.Key, out var l);
        var laeuft = l is { Prozess.HasExited: false };
        return (object)new
        {
            funktion = f.Key,
            laufort = "Client",
            worker = f.Value.Worker is { Skript.Length: > 0 } w ? $"{Path.GetFileName(w.Skript)} {string.Join(' ', w.Argumente)}".Trim()
                : f.Value.Worker is { Projekt.Length: > 0 } p ? $"dotnet {p.Projekt} {string.Join(' ', p.Argumente)}".Trim() : null,
            laeuft,
            pid = laeuft ? l!.Prozess.Id : (int?)null,
            seit = l?.Seit,
            exitCode = l is { Prozess.HasExited: true } ? l.Prozess.ExitCode : (int?)null,
            zuletzt = l?.Log.LastOrDefault(),
        };
    }).ToList();

    public IReadOnlyList<string> Log(string funktion, int max = 200)
        => _laeufe.TryGetValue(funktion, out var l) ? l.Log.Reverse().Take(max).Reverse().ToList() : [];

    /// <param name="funktion">Der Name des Clients.</param>
    /// <param name="standardToken">Ohne Token in der Umgebung: ein Token, dessen Akteure der Client verkörpert, oder null.</param>
    public async Task<object> StarteAsync(string funktion, UmgebungKonfig u, bool hostLaeuft, Func<string, string?>? standardToken = null)
    {
        if (!u.Clients.TryGetValue(funktion, out var c))
            throw new InvalidOperationException($"{funktion} ist in {u.Umgebung.Name} kein Client (Abschnitt Clients).");
        // Der Worker des Clients: Skript, Argumente, Token (sonst das Token des Clients).
        var w = c.Worker is { Skript.Length: > 0 } or { Projekt.Length: > 0 } ? c.Worker
            : throw new InvalidOperationException($"{funktion} in {u.Umgebung.Name}: am Client fehlt Worker:Skript bzw. Worker:Projekt.");
        if (w.Token.Length == 0 && c.Token.Length > 0) w = new WorkerKonfig { Skript = w.Skript, Projekt = w.Projekt, Argumente = w.Argumente, Python = w.Python, Token = c.Token };
        if (w.Token.Length == 0 && standardToken?.Invoke(funktion) is { } t) w = new WorkerKonfig { Skript = w.Skript, Projekt = w.Projekt, Argumente = w.Argumente, Python = w.Python, Token = t };
        if (!hostLaeuft) throw new InvalidOperationException("Der Host läuft nicht — ⟳ startet ihn; der Worker meldet sich an seinem gRPC-Port an.");
        await StoppeAsync(funktion);

        ProcessStartInfo psi;
        if (w.Projekt.Length > 0)
        {
            // Ein .NET-Worker: dotnet run --project <Projekt> -- <Argumente> (gebaut wird mit, wie ⟳ den Host baut).
            var projekt = Path.IsPathRooted(w.Projekt) ? w.Projekt : Path.Combine(slnRoot, w.Projekt);
            if (!Directory.Exists(projekt) && !File.Exists(projekt)) throw new FileNotFoundException($"Worker-Projekt fehlt: {projekt}");
            psi = new ProcessStartInfo("dotnet") { WorkingDirectory = slnRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { "run", "--project", projekt, "--" }.Concat(w.Argumente)) psi.ArgumentList.Add(a);
            psi.Environment["CQRS_REAKTION_STAND"] = Path.Combine(Path.GetTempPath(), "cqrs-reaktionen", u.Umgebung.Name, funktion);
        }
        else
        {
            var python = u.Python(w);
            var skript = Path.IsPathRooted(w.Skript) ? w.Skript : Path.Combine(slnRoot, w.Skript);
            if (!File.Exists(skript)) throw new FileNotFoundException($"Worker-Skript fehlt: {skript}");
            psi = new ProcessStartInfo(python) { WorkingDirectory = Path.GetDirectoryName(skript)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add(skript);
            foreach (var a in w.Argumente) psi.ArgumentList.Add(a);
        }
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
        Nimm($"$ {psi.FileName} {string.Join(' ', psi.ArgumentList)}  (gRPC 127.0.0.1:{u.Transport.GrpcPort}{(w.Token.Length > 0 ? ", Token " + w.Token : "")})");

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
