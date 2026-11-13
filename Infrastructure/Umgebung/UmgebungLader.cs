using System.Text.Json;
using System.Text.RegularExpressions;
using Abstractions;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Umgebung;

/// <summary>
/// Lädt die Umgebung (docs/06 §6.5, W7: eine Umgebung = ein Ordner): <c>umgebungen/&lt;name&gt;/umgebung.json</c>, je Instanz eines Graphen
/// <c>graphen/&lt;instanz&gt;.json</c> und je Instanz einer App <c>apps/&lt;instanz&gt;.json</c>; Name aus <c>--umgebung &lt;name&gt;</c> bzw. <c>CQRS_UMGEBUNG</c>. Umgebungsvariablen und
/// Kommandozeile überschreiben wie gewohnt; <c>${env:NAME}</c> und <c>${branch}</c> werden ersetzt. Ein unbekannter Abschnitt oder ein unbekanntes Feld
/// ist ein Start-Fehler — die Bindung ist generiert, nicht reflektiert.
/// </summary>
public static partial class UmgebungLader
{
    public const string Argument = "--umgebung";
    public const string Variable = "CQRS_UMGEBUNG";

    /// <summary>Name aus den Argumenten oder der Variablen; null = keine Umgebung angegeben.</summary>
    public static string? NameAus(string[] args)
    {
        var i = Array.IndexOf(args, Argument);
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        var g = args.FirstOrDefault(a => a.StartsWith(Argument + "=", StringComparison.Ordinal));
        return g?[(Argument.Length + 1)..] ?? Environment.GetEnvironmentVariable(Variable);
    }

    /// <summary>Die Datei der Umgebung (<c>&lt;name&gt;/umgebung.json</c>): unter <c>CQRS_UMGEBUNGEN</c>, neben dem Programm, oder aufwärts vom Arbeitsverzeichnis.</summary>
    public static string Datei(string name)
    {
        var datei = Path.Combine(name, Abstractions.Projektstruktur.UmgebungsDatei);
        var kandidaten = new List<string>();
        if (Environment.GetEnvironmentVariable("CQRS_UMGEBUNGEN") is { Length: > 0 } verz) kandidaten.Add(Path.Combine(verz, datei));
        kandidaten.Add(Path.Combine(AppContext.BaseDirectory, Abstractions.Projektstruktur.UmgebungsOrdner, datei));
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            kandidaten.Add(Path.Combine(d.FullName, Abstractions.Projektstruktur.UmgebungsOrdner, datei));
        return kandidaten.FirstOrDefault(File.Exists)
               ?? throw new FileNotFoundException($"Umgebung '{name}' nicht gefunden (gesucht: {string.Join(", ", kandidaten.Take(3))} …).");
    }

    /// <summary>
    /// Hängt die Umgebungs-Datei an die Konfiguration (Umgebungsvariablen und Argumente bleiben vorn), ersetzt Platzhalter, prüft die
    /// Abschnitte und bindet die Umgebung. <paramref name="branch"/> ersetzt <c>${branch}</c> (Default: <c>CQRS_BRANCH</c> bzw. „main").
    /// </summary>
    public static UmgebungKonfig Lade(IConfigurationManager konfig, string name, string[] args, string? branch = null)
        => LadeDatei(konfig, Datei(name), name, args, branch);

    /// <summary>
    /// Wie <see cref="Lade"/>, aber mit fester Datei — die Werkbank löst die Umgebungen ihrer Solution damit genauso auf wie der Host
    /// (dieselben Platzhalter, fehlende Variable = Fehler, strenge Bindung; per Link in die Werkbank übersetzt, docs/13 WB-1).
    /// </summary>
    public static UmgebungKonfig LadeDatei(IConfigurationManager konfig, string datei, string name, string[] args, string? branch = null)
    {
        PruefeAbschnitte(datei);
        konfig.AddJsonFile(datei, optional: false, reloadOnChange: false);
        var ordner = Path.GetDirectoryName(datei)!;
        konfig.AddInMemoryCollection(Instanzen(Path.Combine(ordner, Abstractions.Projektstruktur.GraphenOrdner)));
        konfig.AddInMemoryCollection(Instanzen(Path.Combine(ordner, Abstractions.Projektstruktur.AppsOrdner), UmgebungsAbschnitte.Apps));
        konfig.AddEnvironmentVariables();
        konfig.AddCommandLine(args);
        Ersetze(konfig, branch ?? Environment.GetEnvironmentVariable("CQRS_BRANCH") ?? "main");
        var u = Binde(konfig);
        u.DieserRechner = Rechnerregeln.NameAus(args);
        if (string.IsNullOrWhiteSpace(u.Umgebung.Name)) u.Umgebung.Name = name;
        if (u.Umgebung.Name != name)
            throw new InvalidOperationException($"Umgebung: die Datei {Path.GetFileName(datei)} heißt innen '{u.Umgebung.Name}'.");
        return u;
    }

    /// <summary>
    /// Die Instanz-Dateien eines Ordners als Konfig-Schlüssel unter <c>&lt;abschnitt&gt;:&lt;instanz&gt;</c> (Name der Instanz = Dateiname; Graphen bzw.
    /// Apps) — sie gehen durch dieselbe Ersetzung der Platzhalter wie die Umgebung.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Instanzen(string ordner, string abschnitt = UmgebungsAbschnitte.Graphen)
    {
        if (!Directory.Exists(ordner)) yield break;
        foreach (var datei in Directory.GetFiles(ordner, "*.json").OrderBy(d => d, StringComparer.Ordinal))
        {
            var instanz = Path.GetFileNameWithoutExtension(datei);
            var werte = new ConfigurationBuilder().AddJsonFile(datei, optional: false, reloadOnChange: false).Build();
            foreach (var (key, wert) in werte.AsEnumerable().Where(kv => kv.Value != null))
                yield return new($"{abschnitt}:{instanz}:{key}", wert);
        }
    }

    /// <summary>Nur die bekannten Abschnitte (<see cref="UmgebungsAbschnitte"/>) dürfen in einer Umgebungs-Datei stehen.</summary>
    public static void PruefeAbschnitte(string datei)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(datei), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var fremd = doc.RootElement.EnumerateObject().Select(p => p.Name).Where(n => !UmgebungsAbschnitte.Alle.Contains(n)).ToList();
        if (fremd.Count > 0)
            throw new InvalidOperationException($"Umgebung {Path.GetFileName(datei)}: unbekannte Abschnitte {string.Join(", ", fremd)} "
                + $"(erlaubt: {string.Join(", ", UmgebungsAbschnitte.Alle)}).");
    }

    /// <summary>Bindet die Abschnitte der Umgebung streng (unbekannte Felder = Fehler).</summary>
    public static UmgebungKonfig Binde(IConfiguration konfig)
    {
        static T Abschnitt<T>(IConfiguration k, string name) where T : new()
            => k.GetSection(name).Get<T>(o => o.ErrorOnUnknownConfiguration = true) ?? new T();
        var u = new UmgebungKonfig
        {
            Umgebung = Abschnitt<UmgebungKopf>(konfig, UmgebungsAbschnitte.Umgebung),
            EventStore = Abschnitt<EventStoreKonfig>(konfig, UmgebungsAbschnitte.EventStore),
            Cluster = Abschnitt<ClusterKonfig>(konfig, UmgebungsAbschnitte.Cluster),
            Schreibpfad = Abschnitt<SchreibpfadKonfig>(konfig, UmgebungsAbschnitte.Schreibpfad),
            Lebenszyklus = Abschnitt<LebenszyklusKonfig>(konfig, UmgebungsAbschnitte.Lebenszyklus),
            Konsum = Abschnitt<KonsumKonfig>(konfig, UmgebungsAbschnitte.Konsum),
            Transport = Abschnitt<TransportKonfig>(konfig, UmgebungsAbschnitte.Transport),
        };
        // Der Pool und das Backend (docs/06 §6.5): ohne Abschnitt nur localhost — eine Umgebung, die nichts nennt, läuft so wie bisher auf einem Rechner.
        if (konfig.GetSection(UmgebungsAbschnitte.Rechner).GetChildren().ToList() is { Count: > 0 } pool)
        {
            u.Rechner = new Dictionary<string, RechnerKonfig>(StringComparer.Ordinal);
            foreach (var r in pool) u.Rechner[r.Key] = r.Get<RechnerKonfig>(o => o.ErrorOnUnknownConfiguration = true) ?? new RechnerKonfig();
        }
        if (konfig.GetSection(UmgebungsAbschnitte.Backend) is { } backend && backend.Exists())
        {
            // Von Hand: der Binder hängte die Liste an den Standard an, statt ihn zu ersetzen.
            if (backend.GetChildren().FirstOrDefault(k => k.Key != nameof(BackendKonfig.Rechner)) is { } fremd)
                throw new InvalidOperationException($"Umgebung: {UmgebungsAbschnitte.Backend}:{fremd.Key} ist kein Feld (bekannt: {nameof(BackendKonfig.Rechner)}).");
            u.Backend = new BackendKonfig
            {
                Rechner = backend.GetSection(nameof(BackendKonfig.Rechner)).GetChildren().OrderBy(k => int.TryParse(k.Key, out var n) ? n : int.MaxValue)
                    .Select(k => k.Value ?? "").ToArray(),
            };
        }
        foreach (var g in konfig.GetSection(UmgebungsAbschnitte.Graphen).GetChildren())
            u.Graphen[g.Key] = Instanz(g);
        foreach (var a in konfig.GetSection(UmgebungsAbschnitte.Apps).GetChildren())
            u.Apps[a.Key] = App(a);
        return u;
    }

    /// <summary>
    /// Bindet eine App-Instanz streng: <see cref="AppInstanzKonfig.Felder"/>, <c>Start</c> und <c>Dateien</c> über den Binder, je Quelle von Hand
    /// (<see cref="Quelle"/>) — ihre Werte sind die Felder der Konfig-Records ihrer Quellart, geprüft beim Start gegen die App.
    /// </summary>
    public static AppInstanzKonfig App(IConfigurationSection a)
    {
        if (a.GetChildren().FirstOrDefault(k => !AppInstanzKonfig.Felder.Contains(k.Key)) is { } fremd)
            throw new InvalidOperationException($"Umgebung: App-Instanz {a.Key}: „{fremd.Key}“ ist kein Feld (bekannt: {string.Join(", ", AppInstanzKonfig.Felder)}).");
        var ohne = new ConfigurationBuilder().AddInMemoryCollection(a.AsEnumerable(makePathsRelative: true)
            .Where(kv => kv.Value != null && kv.Key.Split(':')[0] != nameof(AppInstanzKonfig.Quellen))).Build();
        var k = ohne.Get<AppInstanzKonfig>(o => o.ErrorOnUnknownConfiguration = true) ?? new AppInstanzKonfig();
        if (string.IsNullOrWhiteSpace(k.Rechner)) k.Rechner = Rechnerregeln.Standard;
        foreach (var q in a.GetSection(nameof(AppInstanzKonfig.Quellen)).GetChildren()) k.Quellen[q.Key] = Quelle(a.Key, q);
        return k;
    }

    /// <summary>Bindet eine Quelle einer App-Instanz: <c>Technik</c> — jeder andere Schlüssel ein Wert.</summary>
    public static QuelleKonfig Quelle(string instanz, IConfigurationSection q)
    {
        var k = new QuelleKonfig();
        foreach (var t in q.GetSection(nameof(QuelleKonfig.Technik)).GetChildren()) k.Technik[t.Key] = t.Value ?? "";
        foreach (var (key, wert) in q.AsEnumerable(makePathsRelative: true).Where(kv => kv.Value != null && !QuelleKonfig.Rahmenfelder.Contains(kv.Key.Split(':')[0])))
            k.Werte[key] = wert;
        return k;
    }

    /// <summary>
    /// Bindet eine Instanz: <c>Graph</c>, <c>Rechner</c>, <c>Straenge</c> und jeder andere Schlüssel ein Knoten — seine Rahmenfelder streng
    /// (<see cref="KnotenKonfig.Rahmenfelder"/>), alles andere als Werte seiner Konfig-Records (geprüft beim Start gegen den Graphen).
    /// </summary>
    public static GraphInstanzKonfig Instanz(IConfigurationSection g)
    {
        var i = new GraphInstanzKonfig
        {
            Graph = g[nameof(GraphInstanzKonfig.Graph)] ?? "", Rechner = g[nameof(GraphInstanzKonfig.Rechner)],
        };
        if (g[nameof(GraphInstanzKonfig.Straenge)] is { } straenge)
            i.Straenge = int.TryParse(straenge, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0
                ? n : throw new InvalidOperationException($"Umgebung: Instanz {g.Key}: Straenge „{straenge}“ ist keine Zahl ab 0 (0 = Standard).");
        foreach (var k in g.GetChildren().Where(k => k.Key is not (nameof(GraphInstanzKonfig.Graph)
                     or nameof(GraphInstanzKonfig.Rechner) or nameof(GraphInstanzKonfig.Straenge))))
        {
            var knoten = new KnotenKonfig();
            var rahmen = new ConfigurationBuilder().AddInMemoryCollection(k.AsEnumerable(makePathsRelative: true)
                .Where(kv => kv.Value != null && KnotenKonfig.Rahmenfelder.Contains(kv.Key.Split(':')[0]) && kv.Key.Split(':')[0] != nameof(KnotenKonfig.Technik)))
                .Build();
            rahmen.Bind(knoten, o => o.ErrorOnUnknownConfiguration = true);
            foreach (var t in k.GetSection(nameof(KnotenKonfig.Technik)).GetChildren()) knoten.Technik[t.Key] = t.Value ?? "";
            foreach (var (key, wert) in k.AsEnumerable(makePathsRelative: true).Where(kv => kv.Value != null && !KnotenKonfig.Rahmenfelder.Contains(kv.Key.Split(':')[0])))
                knoten.Werte[key] = wert;
            i.Knoten[k.Key] = knoten;
        }
        return i;
    }

    [GeneratedRegex(@"\$\{env:([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex EnvPlatzhalter();

    /// <summary>Ersetzt <c>${env:NAME}</c> (fehlt die Variable: Fehler) und <c>${branch}</c> (als DB-tauglicher Name) in allen Werten.</summary>
    public static void Ersetze(IConfiguration konfig, string branch)
    {
        var sicher = Regex.Replace(branch.ToLowerInvariant(), "[^a-z0-9_]", "_");
        foreach (var (key, wert) in konfig.AsEnumerable().Where(kv => kv.Value is { } v && v.Contains("${", StringComparison.Ordinal)).ToList())
        {
            var neu = EnvPlatzhalter().Replace(wert!, m => Environment.GetEnvironmentVariable(m.Groups[1].Value)
                ?? throw new InvalidOperationException($"Umgebung: {key} verweist auf ${{env:{m.Groups[1].Value}}}, die Variable fehlt."));
            neu = neu.Replace("${branch}", sicher, StringComparison.Ordinal);
            konfig[key] = neu;
        }
    }

}
