using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Externe Funktions-Worker als Prozesse der Werkbank (CLAUDE.md W6, docs/06 §6.5, Werkbank P5.5): „▶ Worker starten" an einem
/// Knoten mit Laufort <c>Extern</c> startet dessen <c>Worker</c> (Instanz-Datei der Umgebung, Schlüssel <c>instanz/knoten</c>) — <c>Python Skript Argumente…</c> mit
/// <c>CQRS_GRPC_HOST</c>/<c>CQRS_GRPC_PORT</c>/<c>CQRS_AKTEUR_TOKEN</c>. Der Worker meldet sich wie jeder externe Ausführer am gRPC-Port an und
/// holt Aufträge beim Vermittler; die Werkbank sammelt nur seine Ausgabe.
/// </summary>
public sealed class Worker(string slnRoot)
{
    private sealed record Lauf(Process Prozess, ConcurrentQueue<string> Log, DateTimeOffset Seit, string Umgebung);

    private readonly ConcurrentDictionary<string, Lauf> _laeufe = new(StringComparer.Ordinal);

    /// <summary>Die Knoten der Instanzen einer Umgebung (Schlüssel <c>instanz/knoten</c>) mit ihrer Konfig.</summary>
    public static IEnumerable<KeyValuePair<string, KnotenKonfig>> Knoten(UmgebungKonfig u) =>
        u.Graphen.SelectMany(i => i.Value.Knoten.Select(k => new KeyValuePair<string, KnotenKonfig>(i.Key + "/" + k.Key, k.Value)));

    /// <summary>Je Knoten der Umgebung: Laufort, ob ein Worker konfiguriert ist und ob er läuft.</summary>
    public IReadOnlyList<object> Liste(UmgebungKonfig u) => Knoten(u).OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
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

    /// <summary>Der generische Funktions-Worker (docs/09 §9.5) — der Standard, wenn die Umgebung keinen eigenen nennt.</summary>
    public static readonly string GenerischesSkript = Abstractions.Projektstruktur.PythonWorker(Abstractions.Projektstruktur.StandardWurzel) + "/funktions_worker.py";

    /// <param name="standardToken">Ohne Token in der Umgebung: ein Token, dessen Akteur die Funktion anbieten darf (IDarf&lt;F&gt;), oder null.</param>
    public async Task<object> StarteAsync(string funktion, UmgebungKonfig u, bool hostLaeuft, Func<string, string?>? standardToken = null)
    {
        if (Knoten(u).FirstOrDefault(x => x.Key == funktion).Value is not { } k)
            throw new InvalidOperationException($"{funktion} ist in {u.Umgebung.Name} kein Knoten einer Instanz.");
        if (k.Laufort != FunktionsLaufort.Extern)
            throw new InvalidOperationException($"Laufort von {funktion} in {u.Umgebung.Name} ist {k.Laufort} — erst auf Extern stellen (Instanz-Datei) und ⟳.");
        // Der Worker des Knotens (Instanz-Datei): Skript, Argumente (der generische Worker nennt die Funktion mit --funktionen), Token.
        var w = k.Worker is { Skript.Length: > 0 } eigen ? eigen
            : throw new InvalidOperationException($"{funktion} in {u.Umgebung.Name}: am Knoten fehlt Worker:Skript (der generische Worker: {GenerischesSkript} "
                + "mit --funktionen <Funktion>).");
        if (w.Token.Length == 0 && standardToken?.Invoke(funktion) is { } t) w = new WorkerKonfig { Skript = w.Skript, Argumente = w.Argumente, Python = w.Python, Token = t };
        if (!hostLaeuft) throw new InvalidOperationException("Der Host läuft nicht — ⟳ startet ihn; der Worker meldet sich an seinem gRPC-Port an.");
        await StoppeAsync(funktion);

        var python = u.Python(w);
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
