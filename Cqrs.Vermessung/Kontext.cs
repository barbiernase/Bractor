using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>Eine Exactly-once-Prüfung (docs/plan-garantien-und-durchsatz.md §0.2): Soll gegen Ist aus Queries und Log.</summary>
internal sealed record Pruefung(string Name, bool Ok, string Soll, string Ist)
{
    public static Pruefung Gleich(string name, long soll, long ist) => new(name, soll == ist, soll.ToString(), ist.ToString());
    public JsonObject Json() => new() { ["name"] = Name, ["ok"] = Ok, ["soll"] = Soll, ["ist"] = Ist };
}

/// <summary>Die Kennzahlen eines Messfensters: Transaktionen und Queries je Sekunde (pg_stat_*), CPU und RSS des Hosts.</summary>
internal sealed class Messfenster
{
    private readonly Datenbank _db;
    private readonly Ressourcen _res;
    private readonly string _rolle;
    private Datenbank.PgStand? _a;
    private long _leserVorher;
    public DateTimeOffset Von { get; private set; }

    public Messfenster(Datenbank db, Ressourcen res, string rolle) { _db = db; _res = res; _rolle = rolle; }

    public async Task StarteAsync()
    {
        _leserVorher = _db.LeserStatements;
        _a = await _db.PgStandAsync();
        Von = DateTimeOffset.UtcNow;
    }

    /// <summary>Schließt das Fenster: Kennzahlen je Sekunde und je Vorgang, dazu die Statements für pg_stat_statements.csv.</summary>
    public async Task<(Dictionary<string, double> Werte, List<Datenbank.Statement> Statements)> EndeAsync(int vorgaenge, Ablage ablage, string szenario, int wiederholung)
    {
        var b = await _db.PgStandAsync();
        var bis = DateTimeOffset.UtcNow;
        var (txs, qs, tx, q, xact, st) = Datenbank.Differenz(_a!, b, _db.LeserStatements - _leserVorher);
        var (cpu, rss) = _res.Fenster(_rolle, Von, bis);
        var werte = new Dictionary<string, double>
        {
            ["tx_je_s"] = txs, ["queries_je_s"] = qs,
            ["tx_je_vorgang"] = vorgaenge > 0 ? (double)tx / vorgaenge : double.NaN,
            ["queries_je_vorgang"] = vorgaenge > 0 ? (double)q / vorgaenge : double.NaN,
            ["cpu_prozent"] = cpu, ["rss_mb"] = rss, ["xact_je_s_info"] = xact,
        };
        ablage.Statements(szenario, wiederholung, st);
        return (werte, st);
    }
}

/// <summary>Was alle Szenarien teilen.</summary>
internal sealed class Kontext
{
    public required string Wurzel { get; init; }
    public required Ablage Ablage { get; init; }
    public required Ressourcen Ressourcen { get; init; }
    public required Optionen Optionen { get; init; }
    public required Umgebungsdatei Beispiel { get; init; }
    public required Umgebungsdatei Pruef { get; init; }

    public string HostDll(string projekt) => Path.Combine(Wurzel, projekt, "bin", "Release", "net9.0", projekt + ".dll");
    public string LogDatei(string name) => Path.Combine(Ablage.ScratchLogs, name + ".log");
}

/// <summary>Die Werte einer Umgebungs-Datei, die der Treiber braucht (gelesen als JSON, ohne Bindung).</summary>
internal sealed class Umgebungsdatei
{
    public required string Name { get; init; }
    public required JsonNode Json { get; init; }
    public string Datenbank => (string)Json["EventStore"]!["Datenbank"]!;
    public string Schema => (string?)Json["EventStore"]!["Schema"] ?? "es";
    public string Verbindung => (string)Json["EventStore"]!["Verbindung"]!;
    public int EinblickPort => (int)Json["Transport"]!["EinblickPort"]!;
    public int GrpcPort => (int)Json["Transport"]!["GrpcPort"]!;
    public string RedisEndpunkt => (string)Json["VersionIndex"]!["Endpunkt"]!;
    public int RedisDatenbank => (int)Json["VersionIndex"]!["Datenbank"]!;
    public string Python => Ersetze((string?)Json["Umgebung"]!["Python"] ?? "python3");
    public string? Quellpfad => Json["Quellen"]?["DateiErkannt"]?["Pfad"] is { } p ? Ersetze((string)p!) : null;
    public string? Ausgabepfad => Json["Konfig"]?["BildaufbereitungKonfig"]?["AusgabePfad"] is { } p ? Ersetze((string)p!) : null;

    public static Umgebungsdatei Lies(string wurzel, string name)
        => new() { Name = name, Json = JsonNode.Parse(File.ReadAllText(Path.Combine(wurzel, "umgebungen", $"umgebung.{name}.json")))! };

    /// <summary>Der Worker-Block einer Funktion (Skript, Argumente, Token) mit ersetzten <c>${env:X}</c>.</summary>
    public (string Skript, List<string> Argumente, string Token) Worker(string wurzel, string funktion)
    {
        var w = Json["Funktionen"]![funktion]!["Worker"]!;
        return (Path.Combine(wurzel, (string)w["Skript"]!), w["Argumente"]!.AsArray().Select(a => Ersetze((string)a!)).ToList(), (string)w["Token"]!);
    }

    public static string Ersetze(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\$\{env:([A-Za-z0-9_]+)\}",
        m => Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? throw new InvalidOperationException($"${{env:{m.Groups[1].Value}}} fehlt"));
}
