using System.Globalization;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Die Parameter eines Laufs — fest, damit jede Messung dieselbe ist (§0.1). Über die Kommandozeile wählbar sind nur Art, Anlass, Spalte,
/// Szenarien, Wiederholungen und die Dauer der Langläufe; <c>--probe</c> verkürzt alles für einen Funktionstest (Ergebnis ungültig).
/// </summary>
internal sealed class Optionen
{
    public string Art { get; private set; } = "messung";
    public string Anlass { get; private set; } = "";
    public string? Spalte { get; private set; }
    public HashSet<string> Szenarien { get; private set; } = new() { "S1", "S2", "S3", "S4", "S5" };
    public int Wiederholungen { get; private set; } = 3;
    public TimeSpan Dauer { get; private set; } = TimeSpan.FromHours(8);
    public TimeSpan Stillstand { get; private set; } = TimeSpan.FromHours(1);
    public bool Probe { get; private set; }
    /// <summary>Ergebnis-Ordner, aus dem Dauer-, Aufhol- und Störungslauf die Sättigung nehmen (Standard: die jüngste gültige Messung).</summary>
    public string? SaettigungAus { get; private set; }

    // ── feste Parameter ──
    public Rampe.Parameter RampeBild => Probe
        ? new(2, 2, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 2000, TimeSpan.FromSeconds(10), 40, 2000)
        : new(2, 1.5, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), 2000, TimeSpan.FromSeconds(60), 2000, 2000);
    public Rampe.Parameter RampePruef => Probe
        ? new(5, 2, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 2000, TimeSpan.FromSeconds(10), 100, 2000)
        : new(5, 1.5, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), 2000, TimeSpan.FromSeconds(60), 5000, 2000);
    public Rampe.Parameter RampeSammel => Probe
        ? new(2, 2, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 2000, TimeSpan.FromSeconds(10), 40, 2000)
        : new(2, 1.5, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), 2000, TimeSpan.FromSeconds(60), 2000, 2000);
    public int[] SammelN => Probe ? [100, 500] : [1000, 10000];

    public static Optionen Lies(string[] args)
    {
        var o = new Optionen();
        for (var i = 0; i < args.Length; i++)
        {
            string Wert() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} braucht einen Wert");
            switch (args[i])
            {
                case "--art": o.Art = Wert(); break;
                case "--anlass": o.Anlass = Wert(); break;
                case "--spalte": o.Spalte = Wert(); break;
                case "--szenarien": o.Szenarien = Wert().Split(',').Select(s => s.Trim().ToUpperInvariant()).ToHashSet(); break;
                case "--wiederholungen": o.Wiederholungen = int.Parse(Wert(), CultureInfo.InvariantCulture); break;
                case "--dauer": o.Dauer = TimeSpan.Parse(Wert(), CultureInfo.InvariantCulture); break;
                case "--stillstand": o.Stillstand = TimeSpan.Parse(Wert(), CultureInfo.InvariantCulture); break;
                case "--saettigung-aus": o.SaettigungAus = Wert(); break;
                case "--probe": o.Probe = true; break;
                default: throw new ArgumentException($"unbekannte Option {args[i]}");
            }
        }
        if (o.Anlass.Length == 0) throw new ArgumentException("--anlass fehlt (z. B. baseline, phase-1)");
        if (o.Probe) o.Wiederholungen = Math.Min(o.Wiederholungen, 1);
        return o;
    }

    public JsonObject Json()
    {
        JsonObject Rampe(Rampe.Parameter p) => new()
        {
            ["start_rate_je_s"] = p.StartRate, ["faktor"] = p.Faktor, ["stufen_dauer_s"] = p.StufenDauer.TotalSeconds,
            ["nachlauf_s"] = p.Nachlauf.TotalSeconds, ["p99_grenze_ms"] = p.P99GrenzeMs, ["latenzlauf_dauer_s"] = p.LatenzDauer.TotalSeconds,
            ["latenzlauf_rate"] = "50 % der Sättigung",
        };
        return new JsonObject
        {
            ["szenarien"] = new JsonArray(Szenarien.Order().Select(s => (JsonNode)s).ToArray()),
            ["wiederholungen"] = Wiederholungen,
            ["probe"] = Probe,
            ["rampe_s1_s2"] = Rampe(RampeBild),
            ["rampe_s5"] = Rampe(RampePruef),
            ["rampe_s3"] = Rampe(RampeSammel),
            ["s3_rampe_sammelvorgang"] = "je 200 Teile",
            ["s3_n"] = new JsonArray(SammelN.Select(n => (JsonNode)n).ToArray()),
            ["s3_n_rate"] = "50 % der Sättigung aus der Rampe, ein Sammelvorgang mit N Teilen",
            ["s5_muster"] = "je 10 Vorgänge: 8 Aufträge ok, 1 Auftrag gegen volles Kontingent (Kompensation), 1 Verteilung auf 4 Ziele; 50 Posten, 20 Ziele",
            ["dauer"] = Dauer.ToString(), ["stillstand"] = Stillstand.ToString(),
            ["transaktionen"] = "COMMITs aus pg_stat_statements (track = top); Queries = übrige Statements ohne BEGIN/COMMIT/ROLLBACK/DISCARD/SET/SHOW; ohne die Lese-Rolle des Treibers",
            ["sichtbar"] = "S1/S2: Query GetImagePair je offenem Paar (komplett), alle 100 ms; S3: TeilVerbucht im Log; S4: Query HoleDatensatz; S5: ProzessBeendet im Log",
        };
    }
}
