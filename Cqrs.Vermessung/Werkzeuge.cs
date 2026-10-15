using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace Cqrs.Vermessung;

/// <summary>Kleine Helfer ohne Bezug zur Messung: Repo, Git, Prozesse, Redis, Rechner.</summary>
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

    /// <summary>Leert eine Redis-Datenbank (RESP über TCP, ohne Client-Bibliothek).</summary>
    public static void LeereRedis(string endpunkt, int datenbank)
    {
        var (host, port) = (endpunkt.Split(':')[0], int.Parse(endpunkt.Split(':')[1]));
        using var c = new TcpClient(host, port);
        var s = c.GetStream();
        var befehl = Encoding.ASCII.GetBytes($"SELECT {datenbank}\r\nFLUSHDB\r\n");
        s.Write(befehl);
        s.ReadTimeout = 5000;
        var antwort = new StringBuilder();
        var puffer = new byte[256];
        while (antwort.ToString().Split("+OK").Length < 3)
        {
            var n = s.Read(puffer, 0, puffer.Length);
            if (n <= 0) break;
            antwort.Append(Encoding.ASCII.GetString(puffer, 0, n));
            if (antwort.ToString().Contains('-')) throw new InvalidOperationException($"Redis {endpunkt}: {antwort}");
        }
    }

    public static string Rechner()
    {
        var kerne = Environment.ProcessorCount;
        var modell = OperatingSystem.IsMacOS() ? Laufe("sysctl", "-n machdep.cpu.brand_string") : "";
        var speicher = OperatingSystem.IsMacOS() && long.TryParse(Laufe("sysctl", "-n hw.memsize"), out var b) ? $"{b / (1L << 30)} GiB" : "";
        return $"{Environment.MachineName}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {modell}, {kerne} Kerne, {speicher}";
    }

    /// <summary>Fremdlast: Lastdurchschnitt und die fünf CPU-stärksten fremden Prozesse.</summary>
    public static string Fremdlast()
    {
        var last = OperatingSystem.IsMacOS() ? Laufe("sysctl", "-n vm.loadavg") : File.Exists("/proc/loadavg") ? File.ReadAllText("/proc/loadavg") : "";
        var top = Laufe("sh", "-c \"ps -Ao pcpu,comm -r | head -6 | tail -5\"").Replace('\n', ';');
        return $"Last {last.Trim('{', '}', ' ')} · CPU-stärkste: {System.Text.RegularExpressions.Regex.Replace(top, @"\s+", " ")}";
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
