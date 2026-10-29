using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace Cqrs.Vermessung;

/// <summary>Kleine Helfer ohne Bezug zur Messung: Repo, Git, Prozesse, Rechner.</summary>
internal static class Werkzeuge
{
    /// <summary>Die Wurzel des Repos (das Verzeichnis mit der .sln), gesucht vom Programm aufwärts.</summary>
    public static string RepoWurzel()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (d.GetFiles("*.sln").Length > 0) return d.FullName;
        throw new InvalidOperationException("Repo-Wurzel (*.sln) nicht gefunden.");
    }

    public static string Laufe(string datei, string argumente, string? verzeichnis = null)
    {
        var psi = new ProcessStartInfo(datei, argumente)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = verzeichnis ?? Environment.CurrentDirectory,
        };
        using var p = Process.Start(psi)!;
        var aus = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return aus.Trim();
    }

    public static string Git(string wurzel, string argumente) => Laufe("git", argumente, wurzel);

    public static string Rechner()
    {
        var kerne = Environment.ProcessorCount;
        var modell = OperatingSystem.IsMacOS() ? Laufe("sysctl", "-n machdep.cpu.brand_string") : ProcFeld("/proc/cpuinfo", "model name");
        var bytes = OperatingSystem.IsMacOS()
            ? long.TryParse(Laufe("sysctl", "-n hw.memsize"), out var b) ? b : 0
            : long.TryParse(ProcFeld("/proc/meminfo", "MemTotal").Split(' ')[0], out var kb) ? kb * 1024 : 0;
        var speicher = bytes > 0 ? $"{bytes / (1L << 30)} GiB" : "";
        return $"{Environment.MachineName}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {modell}, {kerne} Kerne, {speicher}";
    }

    /// <summary>Der Wert der ersten Zeile <c>schluessel: wert</c> einer Datei unter /proc (Linux), sonst leer.</summary>
    private static string ProcFeld(string datei, string schluessel)
        => File.Exists(datei)
            ? File.ReadLines(datei).FirstOrDefault(z => z.StartsWith(schluessel))?.Split(':', 2)[1].Trim() ?? ""
            : "";

    /// <summary>
    /// Fremdlast: Lastdurchschnitt und die fünf CPU-stärksten Prozesse <b>jetzt</b> — unter Linux über eine Sekunde aus <c>/proc/&lt;pid&gt;/stat</c>
    /// gemessen. <c>ps -o pcpu</c> nennt den Durchschnitt über die ganze Lebenszeit eines Prozesses: ein kurz zuvor beendeter Build stand dort mit
    /// „390 % dotnet“, obwohl nichts mehr lief. Ohne /proc (macOS) bleibt es bei <c>ps</c>.
    /// </summary>
    public static string Fremdlast()
    {
        var last = OperatingSystem.IsMacOS() ? Laufe("sysctl", "-n vm.loadavg") : File.Exists("/proc/loadavg") ? File.ReadAllText("/proc/loadavg") : "";
        string top;
        if (Directory.Exists("/proc/self"))
        {
            var vorher = CpuTicks();
            Thread.Sleep(1000);
            var nachher = CpuTicks();
            top = string.Join("; ", nachher
                .Select(kv => (kv.Value.Name, Prozent: vorher.TryGetValue(kv.Key, out var v) ? (kv.Value.Ticks - v.Ticks) / 100.0 * 100 : 0))
                .Where(x => x.Prozent > 0).OrderByDescending(x => x.Prozent).Take(5).Select(x => $"{x.Prozent:F0} {x.Name}"));
        }
        else
            top = Laufe("sh", "-c \"ps -Ao pcpu,comm -r | head -6 | tail -5\"").Replace('\n', ';');
        return $"Last {last.Trim('{', '}', ' ')} · CPU jetzt: {System.Text.RegularExpressions.Regex.Replace(top, @"\s+", " ")}";
    }

    /// <summary>Je Prozess Name und verbrauchte CPU-Zeit (utime + stime, in Ticks zu 1/100 s) aus <c>/proc/&lt;pid&gt;/stat</c>.</summary>
    private static Dictionary<int, (string Name, long Ticks)> CpuTicks()
    {
        var aus = new Dictionary<int, (string, long)>();
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;
            try
            {
                var stat = File.ReadAllText(Path.Combine(dir, "stat"));
                var auf = stat.IndexOf('(');
                var zu = stat.LastIndexOf(')');
                var felder = stat[(zu + 2)..].Split(' ');   // ab Feld 3 (state); utime = Feld 14, stime = Feld 15
                aus[pid] = (stat[(auf + 1)..zu], long.Parse(felder[11]) + long.Parse(felder[12]));
            }
            catch (IOException) { }   // der Prozess endete gerade
            catch (UnauthorizedAccessException) { }
        }
        return aus;
    }
}

/// <summary>Perzentile und Median.</summary>
internal static class Statistik
{
    public static double Perzentil(IReadOnlyList<double> werte, double p)
    {
        if (werte.Count == 0) return double.NaN;
        var s = werte.OrderBy(x => x).ToArray();
        var rang = p / 100.0 * (s.Length - 1);
        var u = (int)Math.Floor(rang);
        var o = (int)Math.Ceiling(rang);
        if (u == o || s[u] == s[o]) return s[u];
        if (double.IsInfinity(s[o])) return s[o];   // ein nicht sichtbarer Vorgang (∞) bleibt ∞ statt NaN
        return s[u] + (s[o] - s[u]) * (rang - u);
    }

    public static double Median(IEnumerable<double> werte)
    {
        var w = werte.Where(x => !double.IsNaN(x)).ToList();
        return w.Count == 0 ? double.NaN : Perzentil(w, 50);
    }
}
