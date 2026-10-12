using System.Diagnostics;

namespace SimHost.Werkbank;

/// <summary>Ein externer Prozess mit gesammelter Ausgabe (stdout + stderr, in Ankunftsreihenfolge).</summary>
public static class Prozesse
{
    public sealed record Ergebnis(int Code, IReadOnlyList<string> Zeilen, TimeSpan Dauer)
    {
        public bool Ok => Code == 0;
    }

    public static async Task<Ergebnis> LaufeAsync(string datei, IEnumerable<string> argumente, string verzeichnis,
        IDictionary<string, string>? umgebung = null, TimeSpan? zeitlimit = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(datei) { WorkingDirectory = verzeichnis, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in argumente) psi.ArgumentList.Add(a);
        foreach (var (k, v) in umgebung ?? new Dictionary<string, string>()) psi.Environment[k] = v;
        var zeilen = new List<string>();
        var sw = Stopwatch.StartNew();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (zeilen) zeilen.Add(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (zeilen) zeilen.Add(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(zeitlimit ?? TimeSpan.FromMinutes(10));
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* schon weg */ }
            lock (zeilen) zeilen.Add($"⏱ abgebrochen nach {sw.Elapsed:mm\\:ss}");
            return new Ergebnis(-1, zeilen, sw.Elapsed);
        }
        p.WaitForExit();   // Ausgabe-Puffer leeren
        return new Ergebnis(p.ExitCode, zeilen, sw.Elapsed);
    }
}
