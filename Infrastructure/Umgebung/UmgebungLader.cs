using System.Text.Json;
using System.Text.RegularExpressions;
using Abstractions;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Umgebung;

/// <summary>
/// Lädt die Umgebung (docs/06 §6.5): <c>umgebungen/umgebung.&lt;name&gt;.json</c>, Name aus <c>--umgebung &lt;name&gt;</c>
/// bzw. <c>CQRS_UMGEBUNG</c>. Umgebungsvariablen und Kommandozeile überschreiben wie gewohnt; <c>${env:NAME}</c> und <c>${branch}</c>
/// werden ersetzt. Ein unbekannter Abschnitt oder ein unbekanntes Feld ist ein Start-Fehler — die Bindung ist generiert, nicht reflektiert.
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

    /// <summary>Die Datei der Umgebung: <c>CQRS_UMGEBUNGEN</c>, neben dem Programm, oder aufwärts vom Arbeitsverzeichnis.</summary>
    public static string Datei(string name)
    {
        var datei = $"umgebung.{name}.json";
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
        konfig.AddEnvironmentVariables();
        konfig.AddCommandLine(args);
        Ersetze(konfig, branch ?? Environment.GetEnvironmentVariable("CQRS_BRANCH") ?? "main");
        var u = Binde(konfig);
        if (string.IsNullOrWhiteSpace(u.Umgebung.Name)) u.Umgebung.Name = name;
        if (u.Umgebung.Name != name)
            throw new InvalidOperationException($"Umgebung: die Datei {Path.GetFileName(datei)} heißt innen '{u.Umgebung.Name}'.");
        return u;
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
        foreach (var f in konfig.GetSection(UmgebungsAbschnitte.Funktionen).GetChildren())
            u.Funktionen[f.Key] = f.Get<FunktionsKonfig>(o => o.ErrorOnUnknownConfiguration = true) ?? new FunktionsKonfig();
        foreach (var d in konfig.GetSection(UmgebungsAbschnitte.Dienste).GetChildren())
            u.Dienste[d.Key] = d.Get<DienstKonfig>(o => o.ErrorOnUnknownConfiguration = true) ?? new DienstKonfig();
        foreach (var c in konfig.GetSection(UmgebungsAbschnitte.Clients).GetChildren())
            u.Clients[c.Key] = c.Get<ClientKonfig>(o => o.ErrorOnUnknownConfiguration = true) ?? new ClientKonfig();
        return u;
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
