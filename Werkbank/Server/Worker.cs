using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// App-Instanzen als Prozesse der Werkbank (CLAUDE.md W6, docs/10 E18): „▶ starten“ startet eine Instanz der Umgebung mit ihrem <c>Start</c>
/// (<c>apps/&lt;instanz&gt;.json</c>: Python-Skript oder .NET-Projekt) — nur auf einem Rechner, der diese Maschine ist (Adresse <c>localhost</c> bzw.
/// <c>127.0.0.1</c>). Sie bekommt <c>CQRS_UMGEBUNG</c>, <c>CQRS_APP</c>, <c>CQRS_RECHNER</c>, <c>CQRS_BRANCH</c>, <c>CQRS_GRPC_HOST</c>/<c>CQRS_GRPC_PORT</c>
/// und <c>CQRS_AKTEUR_TOKEN</c>, meldet sich wie jede App am gRPC-Port an; die Werkbank sammelt nur ihre Ausgabe.
/// </summary>
public sealed class Worker(string slnRoot, Func<string> branch)
{
    private sealed record Lauf(Process Prozess, ConcurrentQueue<string> Log, DateTimeOffset Seit, string Umgebung);

    private readonly ConcurrentDictionary<string, Lauf> _laeufe = new(StringComparer.Ordinal);

    /// <summary>Läuft der Rechner auf dieser Maschine (nur dort startet die Werkbank eine App)?</summary>
    public static bool Lokal(UmgebungKonfig u, string rechner) => u.Adresse(rechner) is "localhost" or "127.0.0.1" or "::1";

    /// <summary>Je App-Instanz: App, Rechner, Port, was gestartet würde, ob sie von hier startet und ob sie läuft.</summary>
    public IReadOnlyList<object> Liste(UmgebungKonfig u) => u.Apps.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a =>
    {
        _laeufe.TryGetValue(a.Key, out var l);
        var laeuft = l is { Prozess.HasExited: false };
        var s = a.Value.Start;
        return (object)new
        {
            instanz = a.Key,
            app = a.Value.App,
            rechner = a.Value.Rechner,
            port = a.Value.Port,
            lokal = Lokal(u, a.Value.Rechner),
            start = s is { Skript.Length: > 0 } ? $"{Path.GetFileName(s.Skript)} {string.Join(' ', s.Argumente)}".Trim()
                : s is { Projekt.Length: > 0 } ? $"dotnet {s.Projekt} {string.Join(' ', s.Argumente)}".Trim() : null,
            laeuft,
            pid = laeuft ? l!.Prozess.Id : (int?)null,
            seit = l?.Seit,
            exitCode = l is { Prozess.HasExited: true } ? l.Prozess.ExitCode : (int?)null,
            zuletzt = l?.Log.LastOrDefault(),
        };
    }).ToList();

    public IReadOnlyList<string> Log(string instanz, int max = 200)
        => _laeufe.TryGetValue(instanz, out var l) ? l.Log.Reverse().Take(max).Reverse().ToList() : [];

    /// <param name="instanz">Der Name der App-Instanz (Datei <c>apps/&lt;instanz&gt;.json</c>).</param>
    public async Task<object> StarteAsync(string instanz, UmgebungKonfig u, bool hostLaeuft)
    {
        if (!u.Apps.TryGetValue(instanz, out var a))
            throw new InvalidOperationException($"{instanz} ist in {u.Umgebung.Name} keine App-Instanz (apps/{instanz}.json).");
        var s = a.Start is { Skript.Length: > 0 } or { Projekt.Length: > 0 } ? a.Start
            : throw new InvalidOperationException($"{instanz} in {u.Umgebung.Name}: Start:Skript bzw. Start:Projekt fehlt — die Werkbank weiß nicht, wie sie sie startet.");
        if (!Lokal(u, a.Rechner))
            throw new InvalidOperationException($"{instanz} läuft auf {a.Rechner} ({u.Adresse(a.Rechner)}) — die Werkbank startet nur Apps auf dieser Maschine.");
        if (!hostLaeuft) throw new InvalidOperationException("Der Host läuft nicht — ⟳ startet ihn; die App meldet sich an seinem gRPC-Port an.");
        await StoppeAsync(instanz);

        ProcessStartInfo psi;
        if (s.Projekt.Length > 0)
        {
            // Ein .NET-Projekt: dotnet run --project <Projekt> -- <Argumente> (gebaut wird mit, wie ⟳ den Host baut).
            var projekt = Path.IsPathRooted(s.Projekt) ? s.Projekt : Path.Combine(slnRoot, s.Projekt);
            if (!Directory.Exists(projekt) && !File.Exists(projekt)) throw new FileNotFoundException($"Projekt der App fehlt: {projekt}");
            psi = new ProcessStartInfo("dotnet") { WorkingDirectory = slnRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var x in new[] { "run", "--project", projekt, "--" }.Concat(s.Argumente)) psi.ArgumentList.Add(x);
        }
        else
        {
            var python = u.Python(s);
            var skript = Path.IsPathRooted(s.Skript) ? s.Skript : Path.Combine(slnRoot, s.Skript);
            if (!File.Exists(skript)) throw new FileNotFoundException($"Skript der App fehlt: {skript}");
            psi = new ProcessStartInfo(python) { WorkingDirectory = Path.GetDirectoryName(skript)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add(skript);
            foreach (var x in s.Argumente) psi.ArgumentList.Add(x);
        }
        // Wer sie ist (Umgebung, Instanz, Rechner) — eine App mit Oberfläche liest daraus Port, Token und Dateien; der Stand der Reaktionen je Instanz.
        psi.Environment["CQRS_UMGEBUNG"] = u.Umgebung.Name;
        psi.Environment[AppInstanzwahl.Variable] = instanz;
        psi.Environment[Rechnerregeln.Variable] = a.Rechner;
        psi.Environment["CQRS_BRANCH"] = branch();
        psi.Environment["CQRS_REAKTION_STAND"] = Path.Combine(Path.GetTempPath(), "cqrs-reaktionen", u.Umgebung.Name, instanz);
        psi.Environment["CQRS_GRPC_HOST"] = "127.0.0.1";
        psi.Environment["CQRS_GRPC_PORT"] = u.Transport.GrpcPort.ToString();
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        if (a.Token.Length > 0) psi.Environment["CQRS_AKTEUR_TOKEN"] = a.Token;

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
        _laeufe[instanz] = new Lauf(prozess, log, DateTimeOffset.UtcNow, u.Umgebung.Name);
        Nimm($"$ {psi.FileName} {string.Join(' ', psi.ArgumentList)}  ({a.App} als {instanz} · gRPC 127.0.0.1:{u.Transport.GrpcPort}"
            + (a.Token.Length > 0 ? ", Token " + a.Token : "") + (a.Url(u) is { } url ? ", " + url : "") + ")");

        // Kurz warten: ein Prozess, der sofort scheitert (Import, Argumente), soll als Fehler zurückkommen, nicht als „läuft".
        await Task.Delay(1500);
        if (prozess.HasExited)
            return new { ok = false, instanz, fehler = $"{instanz} beendet (Exit {prozess.ExitCode})", log = Log(instanz, 15) };
        return new { ok = true, instanz, pid = prozess.Id };
    }

    public async Task StoppeAsync(string instanz)
    {
        if (!_laeufe.TryRemove(instanz, out var l) || l.Prozess.HasExited) return;
        try { l.Prozess.Kill(entireProcessTree: true); } catch { /* schon weg */ }
        await l.Prozess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { });
    }

    public Task StoppeAlleAsync() => Task.WhenAll(_laeufe.Keys.ToList().Select(StoppeAsync));
}
