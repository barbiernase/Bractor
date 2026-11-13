using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Die RECHNERDIENSTE der Werkbank (docs/10 E18, W6): ⟳ startet nach dem Host je Rechner des Pools, der auf dieser Maschine liegt (Adresse
/// <c>localhost</c> bzw. <c>127.0.0.1</c>) und App-Instanzen trägt, einen Rechnerdienst (<c>Host.Rechner --umgebung &lt;u&gt; --rechner &lt;r&gt;</c>) — er
/// lässt die Quellen dieser Instanzen laufen und gibt über die Leitung hinein. Ohne Quellen auf dem Rechner endet er gleich von selbst. Die Werkbank
/// sammelt nur seine Ausgabe (Reiter „▶ Apps“).
/// </summary>
public sealed class Rechnerdienste(string slnRoot)
{
    private sealed record Lauf(Process Prozess, ConcurrentQueue<string> Log, DateTimeOffset Seit);

    private readonly ConcurrentDictionary<string, Lauf> _laeufe = new(StringComparer.Ordinal);
    private string? _ziel;

    /// <summary>Der Schlüssel eines Rechnerdiensts in der Liste der Apps (neben den App-Instanzen).</summary>
    public static string Schluessel(string rechner) => "rechnerdienst:" + rechner;

    /// <summary>Die Rechner dieser Maschine, die App-Instanzen tragen — je einer ein Rechnerdienst.</summary>
    public static IEnumerable<string> Lokale(UmgebungKonfig u) =>
        u.Apps.Values.Select(a => a.Rechner).Distinct(StringComparer.Ordinal).Where(r => Worker.Lokal(u, r)).Order(StringComparer.Ordinal);

    /// <summary>Je Rechnerdienst: Rechner, ob er läuft, sein Ende und die letzte Zeile.</summary>
    public IReadOnlyList<object> Liste() => _laeufe.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => (object)new
    {
        instanz = l.Key, app = "Rechnerdienst", rechner = l.Key[(l.Key.IndexOf(':') + 1)..], port = 0, lokal = true, start = Projektstruktur.RechnerHost,
        laeuft = !l.Value.Prozess.HasExited, pid = l.Value.Prozess.HasExited ? (int?)null : l.Value.Prozess.Id, seit = l.Value.Seit,
        exitCode = l.Value.Prozess.HasExited ? l.Value.Prozess.ExitCode : (int?)null, zuletzt = l.Value.Log.LastOrDefault(), dienst = true,
    }).ToList();

    public bool Kennt(string schluessel) => _laeufe.ContainsKey(schluessel);

    public IReadOnlyList<string> Log(string schluessel, int max = 200)
        => _laeufe.TryGetValue(schluessel, out var l) ? l.Log.Reverse().Take(max).Reverse().ToList() : [];

    /// <summary>Startet die Rechnerdienste der Umgebung neu (nach dem Host); gibt zurück, was gestartet wurde.</summary>
    public async Task<IReadOnlyList<string>> StarteAsync(UmgebungKonfig u, string branch, CancellationToken ct)
    {
        await StoppeAlleAsync();
        var rechner = Lokale(u).ToList();
        if (rechner.Count == 0) return [];
        var dll = await ZielAsync(ct);
        if (dll is null || !File.Exists(dll)) return [$"{Projektstruktur.RechnerHost} ist nicht gebaut — keine Quellen"];
        var aus = new List<string>();
        foreach (var r in rechner)
        {
            var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(dll)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { dll, "--umgebung", u.Umgebung.Name, Rechnerregeln.Argument, r }) psi.ArgumentList.Add(a);
            psi.Environment["CQRS_BRANCH"] = branch;
            psi.Environment["CQRS_UMGEBUNGEN"] = Path.Combine(slnRoot, Projektstruktur.UmgebungsOrdner);
            var log = new ConcurrentQueue<string>();
            void Nimm(string? z)
            {
                if (z == null) return;
                log.Enqueue(z);
                while (log.Count > 1000) log.TryDequeue(out _);
            }
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => Nimm(e.Data);
            p.ErrorDataReceived += (_, e) => Nimm(e.Data);
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _laeufe[Schluessel(r)] = new Lauf(p, log, DateTimeOffset.UtcNow);
            aus.Add($"Rechnerdienst {r} gestartet (PID {p.Id})");
        }
        return aus;
    }

    public async Task StoppeAlleAsync()
    {
        foreach (var (k, l) in _laeufe.ToList())
        {
            _laeufe.TryRemove(k, out _);
            if (l.Prozess.HasExited) continue;
            try { Process.Start("kill", ["-TERM", l.Prozess.Id.ToString()])?.WaitForExit(2000); } catch { /* kein kill */ }
            var bis = DateTime.UtcNow.AddSeconds(5);
            while (!l.Prozess.HasExited && DateTime.UtcNow < bis) await Task.Delay(100);
            if (!l.Prozess.HasExited) try { l.Prozess.Kill(entireProcessTree: true); } catch { /* schon weg */ }
        }
    }

    /// <summary>Der Build-Ausgang des Rechnerdiensts, gefragt beim Build-System (wie beim Host).</summary>
    private async Task<string?> ZielAsync(CancellationToken ct)
    {
        if (_ziel != null) return _ziel;
        var host = Projektstruktur.RechnerHost;
        var r = await Prozesse.LaufeAsync("dotnet", ["msbuild", Path.Combine(host, host + ".csproj"), "-getProperty:TargetPath", "-nologo"], slnRoot,
            zeitlimit: TimeSpan.FromMinutes(1), ct: ct);
        return _ziel = r.Ok ? r.Zeilen.Select(z => z.Trim()).LastOrDefault(z => z.EndsWith(".dll", StringComparison.Ordinal)) : null;
    }
}
