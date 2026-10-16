using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Die Parameter eines Laufs — fest, damit jede Messung dieselbe ist (docs/12 §12.5). Über die Kommandozeile wählbar sind nur der Anlass und
/// die Art (Leistung oder <c>--stoerung</c>).
/// </summary>
internal sealed class Optionen
{
    public bool Stoerung { get; private set; }
    public string Anlass { get; private set; } = "";
    public string Art => Stoerung ? "stoerung" : "leistung";

    /// <summary>Jeder Lastblock läuft so oft im selben Host (frische Aggregate, wachsendes Log) — die Tabelle zeigt Median und Spanne.</summary>
    public int Wiederholungen => 3;
    /// <summary>Wie lange ein Block höchstens sendet (meist endet er früher an seiner Höchstzahl).</summary>
    public TimeSpan Fenster => TimeSpan.FromSeconds(15);
    /// <summary>Wie lange der Rückstand nach dem Senden höchstens abgebaut wird, bevor der Block als „nicht abgebaut“ gilt.</summary>
    public TimeSpan AbbauFrist => TimeSpan.FromSeconds(120);
    /// <summary>Bildeingang: so viele TIFF-Paare auf einmal ins Quell-Verzeichnis (ein Stoß).</summary>
    public int Bildpaare => 600;
    /// <summary>Heißer Schreiber: so viele Teile, von <see cref="Sender"/> Sendern, an EINEN Sammelvorgang.</summary>
    public int Teile => 2000;
    /// <summary>Verteilt: Vorgänge der Prüfdomäne — ruhig mit einem Sender, an der Decke mit <see cref="Sender"/>.</summary>
    public int VorgaengeRuhig => 1000;
    public int VorgaengeDecke => 3000;
    public int Sender => 64;
    /// <summary>Leerlauf-Fenster: zwei ganze Poll-Perioden (30 s), damit jeder periodische Takt gleich oft hineinfällt (PF-14).</summary>
    public TimeSpan Leerlauf => TimeSpan.FromSeconds(60);
    /// <summary>Wie lange der Leerlauf höchstens auf das Ende des Nachlaufs wartet (alle Konsumenten am Kopf des Logs).</summary>
    public TimeSpan NachlaufFrist => TimeSpan.FromSeconds(180);
    /// <summary>Wie lange der Neuaufbau höchstens dauern darf.</summary>
    public TimeSpan NeuaufbauFrist => TimeSpan.FromSeconds(300);
    /// <summary>
    /// Ruhe nach dem ersten Start eines Hosts, bevor gemessen wird: der JIT stuft heiße Methoden erst nach und nach hoch. Ohne sie misst der
    /// erste Block kalten Code (gemessen: −28 % Durchsatz am heißen Schreiber).
    /// </summary>
    public TimeSpan Warmlauf => TimeSpan.FromSeconds(30);

    public static Optionen Lies(string[] args)
    {
        var o = new Optionen();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--anlass": o.Anlass = i + 1 < args.Length ? args[++i] : throw new ArgumentException("--anlass braucht einen Wert"); break;
                case "--stoerung": o.Stoerung = true; break;
                default: throw new ArgumentException($"unbekannte Option {args[i]} (nur --anlass <a> und --stoerung)");
            }
        }
        if (o.Anlass.Length == 0) throw new ArgumentException("--anlass fehlt (z. B. server-leistung)");
        return o;
    }

    public JsonObject Json() => new()
    {
        ["wiederholungen"] = Wiederholungen,
        ["fenster_s"] = Fenster.TotalSeconds,
        ["abbau_frist_s"] = AbbauFrist.TotalSeconds,
        ["warmlauf_s"] = Warmlauf.TotalSeconds,
        ["bildeingang"] = $"Stoß von {Bildpaare} TIFF-Paaren (96 × 64), ein Sender",
        ["heiss"] = $"{Teile} Teile von {Sender} Sendern an EINEN Sammelvorgang (N = 1 000 000), je Teil StarteTeilauftrag + SchließeTeilauftragAb",
        ["verteilt"] = $"Prüfdomäne, ruhig: 1 Sender, höchstens {VorgaengeRuhig} Vorgänge; Decke: {Sender} Sender, höchstens {VorgaengeDecke} Vorgänge; "
                       + "je 10 Vorgänge 8 Aufträge, 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung auf 4 Ziele; 50 Posten, 20 Ziele",
        ["neuaufbau"] = "Host gestoppt, der gespeicherte Projektions-Stand gelöscht (wirkt wie eine Code-Änderung in Sim), Host gestartet: alle Projektionen "
                        + "lesen das Log ab 0; Dauer vom ersten Abbau bis kein Rückstand (/monitoring/metrics, alle 50 ms); dreimal",
        ["leerlauf_s"] = Leerlauf.TotalSeconds,
        ["leerlauf"] = "erst nach dem Nachlauf (alle Poll-Cursor und Projektions-Positionen am Kopf des Logs, höchstens nachlauf_frist_s), dann das Fenster",
        ["nachlauf_frist_s"] = NachlaufFrist.TotalSeconds,
        ["sichtbar"] = "Bildeingang: Query GetImagePair je offenem Paar (komplett), alle 100 ms; Heiß: TeilVerbucht im Log; Verteilt: ProzessBeendet im Log",
        ["transaktionen"] = "COMMITs aus pg_stat_statements (track = top); Queries = übrige Statements ohne BEGIN/COMMIT/ROLLBACK/DISCARD/SET/SHOW; ohne die Lese-Rolle des Treibers",
        ["cpu_ms_je_vorgang"] = "mittlere CPU des Hosts im Fenster × Dauer des Fensters / Vorgänge",
    };
}
