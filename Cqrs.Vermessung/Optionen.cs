using System.Globalization;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Die Parameter eines Laufs — fest, damit jede Messung dieselbe ist (§0.1). Über die Kommandozeile wählbar sind nur Art, Anlass, Spalte,
/// Szenarien und Wiederholungen; <c>--probe</c> verkürzt alles für einen Funktionstest des Treibers (Ergebnis nicht im Repo).
/// </summary>
internal sealed class Optionen
{
    public string Art { get; private set; } = "messung";
    public string Anlass { get; private set; } = "";
    public string? Spalte { get; private set; }
    public HashSet<string> Szenarien { get; private set; } = new() { "S1", "S2", "S3", "S4", "S5" };
    public int Wiederholungen { get; private set; } = 1;
    public bool Probe { get; private set; }

    // ── feste Parameter (Höchstlast, Plan §0.1) ──
    /// <summary>Wie lange ein Volllast-Block sendet.</summary>
    public TimeSpan Fenster => Probe ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(15);
    /// <summary>Wie lange der Rückstand nach dem Fenster höchstens abgebaut wird, bevor der Block als „nicht abgebaut“ gilt.</summary>
    public TimeSpan AbbauFrist => TimeSpan.FromSeconds(120);
    /// <summary>S1/S2: so viele TIFF-Paare auf einmal ins Quell-Verzeichnis (ein Stoß).</summary>
    public int StossS1 => Probe ? 100 : 600;
    public int StossS2 => Probe ? 50 : 200;
    /// <summary>Gleichzeitige Sender je Block.</summary>
    public int[] SenderS3 => [8, 64];
    public int[] SenderS5 => [1, 8, 64];
    /// <summary>Höchstzahl Vorgänge je Block: unter Höchstlast nimmt der Eingang weit mehr an, als dahinter durchkommt — ohne Deckel wüchse
    /// der Rückstand auf zehntausende und der Abbau dauerte Stunden.</summary>
    public int MaxS3 => Probe ? 500 : 2000;
    public int MaxS5 => Probe ? 1000 : 3000;
    public TimeSpan Leerlauf => Probe ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(15);
    /// <summary>
    /// Ruhe nach dem ersten Start eines Hosts, bevor gemessen wird (nicht nach einem Neustart im Störungslauf): der JIT stuft heiße Methoden
    /// erst nach und nach hoch. Ohne sie misst der erste Block kalten Code — gemessen S3-8 −28 % Durchsatz. Früher bekam der Host diese Zeit
    /// zufällig: auf einer frischen DB meldete <c>/health</c> erst gesund, wenn der Prozess-Poll nach 30 s die Event-Tabellen angelegt hatte.
    /// </summary>
    public TimeSpan Warmlauf => Probe ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);

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
                case "--probe": o.Probe = true; break;
                default: throw new ArgumentException($"unbekannte Option {args[i]}");
            }
        }
        if (o.Anlass.Length == 0) throw new ArgumentException("--anlass fehlt (z. B. baseline, phase-1)");
        return o;
    }

    public JsonObject Json() => new()
    {
        ["szenarien"] = new JsonArray(Szenarien.Order().Select(s => (JsonNode)s).ToArray()),
        ["wiederholungen"] = Wiederholungen,
        ["probe"] = Probe,
        ["fenster_s"] = Fenster.TotalSeconds,
        ["abbau_frist_s"] = AbbauFrist.TotalSeconds,
        ["warmlauf_s"] = Warmlauf.TotalSeconds,
        ["s1_stoss_paare"] = StossS1,
        ["s2_stoss_paare"] = StossS2,
        ["s3_sender"] = new JsonArray(SenderS3.Select(n => (JsonNode)n).ToArray()),
        ["s3"] = "je Block ein Sammelvorgang (N = 1 000 000, die Barriere wird nicht erreicht); je Teil StarteTeilauftrag + SchließeTeilauftragAb",
        ["s5_sender"] = new JsonArray(SenderS5.Select(n => (JsonNode)n).ToArray()),
        ["s3_hoechstens_teile_je_block"] = MaxS3,
        ["s5_hoechstens_vorgaenge_je_block"] = MaxS5,
        ["s5_muster"] = "je 10 Vorgänge: 8 Aufträge ok, 1 Auftrag gegen volles Kontingent (Kompensation), 1 Verteilung auf 4 Ziele; 50 Posten, 20 Ziele",
        ["leerlauf_s"] = Leerlauf.TotalSeconds,
        ["sichtbar"] = "S1/S2: Query GetImagePair je offenem Paar (komplett), alle 100 ms; S3: TeilVerbucht im Log; S4: Query HoleDatensatz; S5: ProzessBeendet im Log",
        ["transaktionen"] = "COMMITs aus pg_stat_statements (track = top); Queries = übrige Statements ohne BEGIN/COMMIT/ROLLBACK/DISCARD/SET/SHOW; ohne die Lese-Rolle des Treibers",
    };
}
