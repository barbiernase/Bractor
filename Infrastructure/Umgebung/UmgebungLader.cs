using System.Text.Json;
using System.Text.RegularExpressions;
using Abstractions;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Umgebung;

/// <summary>
/// Lädt die Umgebung (docs/06 §6.5, W7: eine Umgebung = ein Ordner): <c>umgebungen/&lt;name&gt;/umgebung.json</c> und je Instanz eines Graphen
/// <c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c>; Name aus <c>--umgebung &lt;name&gt;</c> bzw. <c>CQRS_UMGEBUNG</c>. Umgebungsvariablen und
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
        konfig.AddInMemoryCollection(Instanzen(Path.Combine(Path.GetDirectoryName(datei)!, Abstractions.Projektstruktur.GraphenOrdner)));
        konfig.AddEnvironmentVariables();
        konfig.AddCommandLine(args);
        Ersetze(konfig, branch ?? Environment.GetEnvironmentVariable("CQRS_BRANCH") ?? "main");
        var u = Binde(konfig);
        if (string.IsNullOrWhiteSpace(u.Umgebung.Name)) u.Umgebung.Name = name;
        if (u.Umgebung.Name != name)
            throw new InvalidOperationException($"Umgebung: die Datei {Path.GetFileName(datei)} heißt innen '{u.Umgebung.Name}'.");
        return u;
    }

    /// <summary>
    /// Die Instanz-Dateien eines Ordners als Konfig-Schlüssel unter <c>Graphen:&lt;instanz&gt;</c> (Name der Instanz = Dateiname) — sie gehen durch dieselbe
    /// Ersetzung der Platzhalter wie die Umgebung.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Instanzen(string ordner)
    {
        if (!Directory.Exists(ordner)) yield break;
        foreach (var datei in Directory.GetFiles(ordner, "*.json").OrderBy(d => d, StringComparer.Ordinal))
        {
            var instanz = Path.GetFileNameWithoutExtension(datei);
            var werte = new ConfigurationBuilder().AddJsonFile(datei, optional: false, reloadOnChange: false).Build();
            foreach (var (key, wert) in werte.AsEnumerable().Where(kv => kv.Value != null))
                yield return new($"{UmgebungsAbschnitte.Graphen}:{instanz}:{key}", wert);
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
        foreach (var g in konfig.GetSection(UmgebungsAbschnitte.Graphen).GetChildren())
            u.Graphen[g.Key] = Instanz(g);
        foreach (var c in konfig.GetSection(UmgebungsAbschnitte.Clients).GetChildren())
            u.Clients[c.Key] = Client(c);
        return u;
    }

    /// <summary>
    /// Bindet einen Client streng — seine Eingänge von Hand (<see cref="Eingang"/>): ihre Werte sind die Felder der Konfig-Records ihrer Quellart, flach
    /// wie am Knoten einer Instanz, geprüft beim Start gegen die Quellart.
    /// </summary>
    public static ClientKonfig Client(IConfigurationSection c)
    {
        var ohne = new ConfigurationBuilder().AddInMemoryCollection(c.AsEnumerable(makePathsRelative: true)
            .Where(kv => kv.Value != null && kv.Key.Split(':')[0] != nameof(ClientKonfig.Eingaenge))).Build();
        var k = ohne.Get<ClientKonfig>(o => o.ErrorOnUnknownConfiguration = true) ?? new ClientKonfig();
        foreach (var e in c.GetSection(nameof(ClientKonfig.Eingaenge)).GetChildren()) k.Eingaenge[e.Key] = Eingang(c.Key, e);
        return k;
    }

    /// <summary>Bindet einen Eingang: <c>Art</c>, <c>Akteur</c>, <c>Rechner</c>, <c>Offen</c>, <c>Technik</c> — jeder andere Schlüssel ein Wert.</summary>
    public static EingangKonfig Eingang(string client, IConfigurationSection e)
    {
        var k = new EingangKonfig
        {
            Art = e[nameof(EingangKonfig.Art)] ?? "", Akteur = e[nameof(EingangKonfig.Akteur)] ?? "", Rechner = e[nameof(EingangKonfig.Rechner)],
        };
        if (e[nameof(EingangKonfig.Offen)] is { } offen)
            k.Offen = int.TryParse(offen, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0
                ? n : throw new InvalidOperationException($"Umgebung: Clients:{client}:Eingaenge:{e.Key}: Offen „{offen}“ ist keine Zahl ab 0 (0 = Standard).");
        foreach (var t in e.GetSection(nameof(EingangKonfig.Technik)).GetChildren()) k.Technik[t.Key] = t.Value ?? "";
        foreach (var (key, wert) in e.AsEnumerable(makePathsRelative: true).Where(kv => kv.Value != null && !EingangKonfig.Rahmenfelder.Contains(kv.Key.Split(':')[0])))
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
