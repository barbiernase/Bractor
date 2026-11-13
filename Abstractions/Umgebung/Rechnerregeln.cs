using System.Text.RegularExpressions;

namespace Abstractions;

/// <summary>Ein Befund zu Rechnern und Backend: die Karte, an der er steht, und was nicht stimmt.</summary>
public sealed record Rechnerbefund(string Karte, string Text);

/// <summary>
/// RECHNER UND BACKEND (docs/06 §6.5): eine Umgebung nennt ihren Pool von Rechnern (Name → Adresse) und welche davon das Backend bilden. Alles mit Ort
/// nennt einen Rechner aus dem Pool; jeder Prozess kennt seinen eigenen (<see cref="NameAus"/>) und nimmt sich, was ihm zugewiesen ist. EINE Quelle der
/// Regeln für den Start des Hosts (Boot-Fehler) und die Sicht der Werkbank (Befunde an den Karten).
/// </summary>
public static partial class Rechnerregeln
{
    /// <summary>Der Rechner, wenn nichts anderes gesagt ist — und der eine Rechner einer Umgebung ohne Abschnitt <c>Rechner</c>.</summary>
    public const string Standard = "localhost";

    /// <summary>Der Rechner dieses Prozesses als Variable.</summary>
    public const string Variable = "CQRS_RECHNER";

    /// <summary>Der Rechner dieses Prozesses als Argument (vor der Variablen).</summary>
    public const string Argument = "--rechner";

    /// <summary>Die Karte des Backends (Befunde, Gesten).</summary>
    public const string BackendKarte = "backend";

    /// <summary>Die Karte eines Rechners.</summary>
    public static string Karte(string rechner) => "rechner:" + rechner;

    /// <summary>Die Karte einer Instanz (wie ihr Boot-Fehler).</summary>
    public static string InstanzKarte(string instanz) => "instanz:" + instanz;

    /// <summary>Die Karte einer App-Instanz (wie ihr Boot-Fehler).</summary>
    public static string AppKarte(string instanz) => "appinstanz:" + instanz;

    /// <summary>Der Rechner dieses Prozesses: <c>--rechner &lt;name&gt;</c>, <c>--rechner=&lt;name&gt;</c>, sonst <c>CQRS_RECHNER</c>, sonst <see cref="Standard"/>.</summary>
    public static string NameAus(string[] args)
    {
        var i = Array.IndexOf(args, Argument);
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        var g = args.FirstOrDefault(a => a.StartsWith(Argument + "=", StringComparison.Ordinal));
        return g?[(Argument.Length + 1)..] ?? (Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } v ? v : Standard);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex NamensMuster();

    /// <summary>Ist <paramref name="name"/> ein Name für einen Rechner (Buchstaben, Ziffern, Punkt, Bindestrich, Unterstrich)?</summary>
    public static bool IstName(string? name) => name is { Length: > 0 } && NamensMuster().IsMatch(name);

    /// <summary>
    /// Was an Pool, Backend und den Rechnern der Instanzen nicht stimmt: jeder Rechner hat einen Namen und eine Adresse, das Backend nennt mindestens einen
    /// Rechner, jeden einmal und nur aus dem Pool; eine Instanz eines Graphen nennt keinen oder einen Rechner des Backends; eine App-Instanz nennt einen
    /// Rechner des Pools, und ihr Port ist auf ihrem Rechner frei (keine zweite App, auf einem Rechner des Backends nicht die Ports des Hosts).
    /// </summary>
    /// <param name="pool">Name → Adresse.</param>
    /// <param name="backend">Die Rechner des Backends in ihrer Reihenfolge.</param>
    /// <param name="instanzen">Je Instanz eines Graphen ihr Rechner (null = keiner).</param>
    /// <param name="apps">Je Instanz einer App ihr Rechner und Port (0 = keiner).</param>
    /// <param name="hostPorts">Die Ports des Hosts auf jedem Rechner des Backends (gRPC, Einblick; 0 = aus).</param>
    public static IReadOnlyList<Rechnerbefund> Befunde(IReadOnlyDictionary<string, string> pool, IReadOnlyList<string> backend,
        IEnumerable<(string Instanz, string? Rechner)> instanzen, IEnumerable<(string Instanz, string Rechner, int Port)>? apps = null,
        IReadOnlyList<int>? hostPorts = null)
    {
        var aus = new List<Rechnerbefund>();
        if (pool.Count == 0) aus.Add(new(BackendKarte, "kein Rechner im Pool — mindestens einer (\"Rechner\": { \"localhost\": { \"Adresse\": \"localhost\" } })"));
        foreach (var (name, adresse) in pool)
        {
            if (!IstName(name)) aus.Add(new(Karte(name), $"„{name}“ ist kein Name für einen Rechner (Buchstaben, Ziffern, . - _)"));
            if (string.IsNullOrWhiteSpace(adresse)) aus.Add(new(Karte(name), $"Rechner {name}: keine Adresse (IP oder Hostname)"));
            else if (adresse.Contains("://", StringComparison.Ordinal) || adresse.Any(char.IsWhiteSpace))
                aus.Add(new(Karte(name), $"Rechner {name}: „{adresse}“ ist keine Adresse — nur IP oder Hostname, ohne Schema und Port"));
        }
        if (backend.Count == 0) aus.Add(new(BackendKarte, "das Backend nennt keinen Rechner — mindestens einen aus dem Pool"));
        foreach (var r in backend.GroupBy(r => r, StringComparer.Ordinal).Where(g => g.Count() > 1))
            aus.Add(new(BackendKarte, $"das Backend nennt {r.Key} {r.Count()}-mal"));
        foreach (var r in backend.Distinct(StringComparer.Ordinal).Where(r => !pool.ContainsKey(r)))
            aus.Add(new(BackendKarte, $"das Backend nennt {r} — diesen Rechner gibt es im Pool nicht"));
        var imBackend = backend.ToHashSet(StringComparer.Ordinal);
        foreach (var (instanz, rechner) in instanzen)
            if (rechner is not null && !imBackend.Contains(rechner))
                aus.Add(new(InstanzKarte(instanz), pool.ContainsKey(rechner)
                    ? $"Instanz {instanz} nennt {rechner} — der Rechner gehört nicht zum Backend ({string.Join(", ", backend)})"
                    : $"Instanz {instanz} nennt {rechner} — diesen Rechner gibt es im Pool nicht"));
        var belegt = new Dictionary<(string, int), string>();
        foreach (var (instanz, rechner, port) in (apps ?? []).OrderBy(a => a.Instanz, StringComparer.Ordinal))
        {
            if (!pool.ContainsKey(rechner))
            {
                aus.Add(new(AppKarte(instanz), $"App-Instanz {instanz} nennt {rechner} — diesen Rechner gibt es im Pool nicht"));
                continue;
            }
            if (port < 0 || port > 65535) aus.Add(new(AppKarte(instanz), $"App-Instanz {instanz}: Port {port} ist kein Port (1–65535, 0 = keiner)"));
            if (port <= 0) continue;
            if (imBackend.Contains(rechner) && (hostPorts ?? []).Contains(port))
                aus.Add(new(AppKarte(instanz), $"App-Instanz {instanz}: Port {port} auf {rechner} belegt der Host (gRPC bzw. Einblick)"));
            if (belegt.TryGetValue((rechner, port), out var schon))
                aus.Add(new(AppKarte(instanz), $"App-Instanz {instanz}: Port {port} auf {rechner} hat schon {schon} — je Rechner ein Port je App"));
            else belegt[(rechner, port)] = instanz;
        }
        return aus;
    }
}
