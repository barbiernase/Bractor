using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>Was eine Wiederholung eines Szenarios liefert: Kennzahlen, Exactly-once-Prüfungen, Details (Stufen der Rampe).</summary>
internal sealed class SzenarioWert
{
    public Dictionary<string, double> Kennzahlen { get; } = new();
    public List<Pruefung> Pruefungen { get; } = new();
    public JsonObject Details { get; } = new();
}

/// <summary>
/// Die Messung S1–S5 (§0.1) in <c>Wiederholungen</c> Durchgängen. Je Durchgang: frische DBs; Beispieldomäne mit Funktionen im Host → S1,
/// S4 (über die Paare aus S1), S3 (N = 1 000, 10 000); dieselbe DB mit Bild-Funktionen im Python-Worker → S2; Prüfdomäne → S5.
/// Ergebnis je Kennzahl: alle Wiederholungen und ihr Median; Exactly-once je Szenario und global je Anwendung.
/// </summary>
internal static class Messung
{
    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var o = k.Optionen;
        var werte = new Dictionary<string, List<SzenarioWert>>();
        var global = new JsonArray();
        var alleOk = true;
        void Merke(string szenario, SzenarioWert w)
        {
            if (!werte.TryGetValue(szenario, out var l)) werte[szenario] = l = new();
            w.Details["fremdlast_danach"] = Werkzeuge.Fremdlast();   // je Szenario — ein Spiel oder ein Build daneben macht eine Wiederholung unbrauchbar
            l.Add(w);
            alleOk &= w.Pruefungen.All(p => p.Ok);
            foreach (var p in w.Pruefungen.Where(p => !p.Ok)) Console.WriteLine($"  ✗ {szenario}: {p.Name} — soll {p.Soll}, ist {p.Ist}");
        }

        for (var w = 1; w <= o.Wiederholungen; w++)
        {
            Console.WriteLine($"── Wiederholung {w}/{o.Wiederholungen} ──");
            if (o.Szenarien.Overlaps(["S1", "S2", "S3", "S4"]))
            {
                await using (var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", $"beispiel-w{w}", zuruecksetzen: true,
                                 mitDaten: ["prozess_gestartet"]))
                {
                    if (o.Szenarien.Contains("S1") || o.Szenarien.Contains("S4"))
                        Merke("S1", await BildAsync(k, lauf, "S1", 2030, w));
                    if (o.Szenarien.Contains("S4")) Merke("S4", await DatensatzAsync(k, lauf, w));
                    if (o.Szenarien.Contains("S3"))
                        foreach (var (name, wert) in await SammelAsync(k, lauf, w)) Merke(name, wert);
                    Merke("L1", await LeerlaufAsync(k, lauf, "L1", w));
                }
                if (o.Szenarien.Contains("S2"))
                {
                    var extern_ = new Dictionary<string, string>
                    {
                        ["Funktionen__IBildVerkleinerung__Laufort"] = "Extern", ["Funktionen__IHistogrammAusgleich__Laufort"] = "Extern",
                    };
                    await using var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", $"beispiel-s2-w{w}", zuruecksetzen: false,
                        extern_, "prozess_gestartet");
                    await lauf.StarteWorkerAsync(k.Beispiel, "IBildVerkleinerung", $"worker-verkleinerung-w{w}");
                    await lauf.StarteWorkerAsync(k.Beispiel, "IHistogrammAusgleich", $"worker-ausgleich-w{w}");
                    Merke("S2", await BildAsync(k, lauf, "S2", 2031, w));
                }
                // Global über alles, was die Beispieldomäne in diesem Durchgang geschrieben hat.
                var db = new Datenbank(k.Beispiel.Verbindung, k.Beispiel.Datenbank, k.Beispiel.Schema);
                var g = await GlobalePruefung.PruefeAsync(db, "Beispieldomäne");
                alleOk &= g.All(p => p.Ok);
                global.Add(new JsonObject { ["wiederholung"] = w, ["anwendung"] = "Beispieldomäne", ["pruefungen"] = new JsonArray(g.Select(p => (JsonNode)p.Json()).ToArray()) });
            }
            if (o.Szenarien.Contains("S5"))
            {
                await using var lauf = await Anwendungslauf.StarteAsync(k, k.Pruef, "Host.Pruefdomaene", "host", $"pruefdomaene-w{w}", zuruecksetzen: true,
                    mitDaten: ["posten_reserviert"]);
                Merke("S5", await PruefAsync(k, lauf, w));
                Merke("L5", await LeerlaufAsync(k, lauf, "L5", w));
                var g = await GlobalePruefung.PruefeAsync(lauf.Db, "Prüfdomäne");
                alleOk &= g.All(p => p.Ok);
                global.Add(new JsonObject { ["wiederholung"] = w, ["anwendung"] = "Prüfdomäne", ["pruefungen"] = new JsonArray(g.Select(p => (JsonNode)p.Json()).ToArray()) });
            }
        }

        var szenarien = new JsonObject();
        foreach (var (name, liste) in werte.OrderBy(kv => kv.Key))
        {
            var median = new JsonObject();
            foreach (var key in liste.SelectMany(w => w.Kennzahlen.Keys).Distinct())
                median[key] = Ablage.Zahl(Statistik.Median(liste.Select(w => w.Kennzahlen.TryGetValue(key, out var v) ? v : double.NaN)));
            szenarien[name] = new JsonObject
            {
                ["titel"] = Titel(name),
                ["wiederholungen"] = new JsonArray(liste.Select(w => (JsonNode)new JsonObject
                {
                    ["kennzahlen"] = new JsonObject(w.Kennzahlen.Select(kv => KeyValuePair.Create(kv.Key, Ablage.Zahl(kv.Value)))),
                    ["details"] = w.Details.DeepClone(),
                }).ToArray()),
                ["median"] = median,
                ["exactly_once"] = new JsonObject
                {
                    ["ok"] = liste.All(w => w.Pruefungen.All(p => p.Ok)),
                    ["pruefungen"] = new JsonArray(liste.SelectMany((w, i) => w.Pruefungen.Select(p => { var j = p.Json(); j["wiederholung"] = i + 1; return (JsonNode)j; })).ToArray()),
                },
            };
        }
        return new JsonObject
        {
            ["szenarien"] = szenarien,
            ["global"] = global,
            ["exactly_once_ok"] = alleOk,
            ["zusammenfassung"] = Zusammenfassung.Zeilen(szenarien),
        };
    }

    public static string Titel(string s) => s switch
    {
        "S1" => "Bildeingang Host", "S2" => "Bildeingang mit Python-Worker", "S3" => "Sammelvorgang, Rampe (je 200 Teile)", "S3-1000" => "Sammelvorgang N = 1 000", "S3-10000" => "Sammelvorgang N = 10 000",
        "S4" => "Datensatz einfrieren", "S5" => "Prüfdomäne: Prozesse und Reaktion",
        "L1" => "Leerlauf Beispieldomäne (nach S1, S4, S3)", "L5" => "Leerlauf Prüfdomäne (nach S5)",
        _ when s.StartsWith("S3-") => $"Sammelvorgang N = {s[3..]}", _ => s,
    };

    // ── S1/S2 ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
    private static async Task<SzenarioWert> BildAsync(Kontext k, Anwendungslauf lauf, string name, int jahr, int w)
    {
        Console.WriteLine($"  {name} {Titel(name)}");
        k.Ressourcen.Abschnitt($"{name}-w{w}");
        var wert = new SzenarioWert();
        await using var bild = new Bildeingang(lauf.Eingang, k.Beispiel.Quellpfad!, jahr, w);
        var rampe = new Rampe(bild, k.Optionen.RampeBild);
        var (satt, stufen) = await rampe.SaettigungAsync(default);
        await LatenzlaufAsync(k, lauf, rampe, name, w, satt, k.Optionen.RampeBild, wert);
        wert.Details["stufen"] = Stufen(stufen);
        foreach (var v in rampe.Alle) k.Ablage.Latenz(name, w, v);
        wert.Pruefungen.AddRange(await bild.PruefeAsync(lauf.Db, 2 * rampe.Alle.Count(v => v.Fehler == null)));
        return wert;
    }

    /// <summary>Latenzlauf bei 50 % der Sättigung, mit Messfenster (Tx/s, Queries/s, CPU, RSS).</summary>
    private static async Task LatenzlaufAsync(Kontext k, Anwendungslauf lauf, Rampe rampe, string name, int w, double satt, Rampe.Parameter p, SzenarioWert wert)
    {
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        var stufe = await rampe.StufeAsync("latenz", Math.Max(0.5, satt / 2), p.LatenzDauer, default);
        if (!await rampe.LeerlaufenAsync(TimeSpan.FromSeconds(180), default)) throw new RampenFehler($"{name}: Latenzlauf baut sich nicht ab");
        var (kz, _) = await fenster.EndeAsync(stufe.Anzahl, k.Ablage, name, w);
        Console.WriteLine($"    Sättigung {satt:F1}/s · Latenz bei 50 %: p50 {stufe.P50:F0} p95 {stufe.P95:F0} p99 {stufe.P99:F0} ms · " +
                          $"{kz["tx_je_s"]:F0} Tx/s · {kz["queries_je_s"]:F0} Q/s · CPU {kz["cpu_prozent"]:F0} % · RSS {kz["rss_mb"]:F0} MB");
        wert.Kennzahlen["saettigung_je_s"] = satt;
        wert.Kennzahlen["latenz_p50_ms"] = stufe.P50;
        wert.Kennzahlen["latenz_p95_ms"] = stufe.P95;
        wert.Kennzahlen["latenz_p99_ms"] = stufe.P99;
        foreach (var (key, v) in kz) wert.Kennzahlen[key] = v;
        var workerCpu = k.Ressourcen.Summe("worker-", fenster.Von, DateTimeOffset.UtcNow);
        if (!double.IsNaN(workerCpu)) wert.Kennzahlen["worker_cpu_prozent"] = workerCpu;
        wert.Details["latenzlauf"] = StufeJson(stufe);
    }

    private static JsonArray Stufen(IEnumerable<Stufe> stufen) => new(stufen.Select(s => (JsonNode)StufeJson(s)).ToArray());

    private static JsonObject StufeJson(Stufe s) => new()
    {
        ["phase"] = s.Phase, ["rate_je_s"] = Ablage.Zahl(s.Rate), ["anzahl"] = s.Anzahl, ["sichtbar"] = s.Sichtbar, ["fehler"] = s.Fehler,
        ["p50_ms"] = Ablage.Zahl(s.P50), ["p95_ms"] = Ablage.Zahl(s.P95), ["p99_ms"] = Ablage.Zahl(s.P99), ["bestanden"] = s.Bestanden, ["grund"] = s.Grund,
    };

    // ── Leerlauf ───────────────────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>Grundlast ohne Eingang (PF-2, PF-4): 60 s nichts tun, mit allem, was die Szenarien davor angelegt haben.</summary>
    private static async Task<SzenarioWert> LeerlaufAsync(Kontext k, Anwendungslauf lauf, string name, int w)
    {
        Console.WriteLine($"  {name} {Titel(name)}");
        k.Ressourcen.Abschnitt($"{name}-w{w}");
        await Task.Delay(TimeSpan.FromSeconds(10));   // Nachlauf der Szenarien abklingen lassen
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        await Task.Delay(k.Optionen.Probe ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(60));
        var (kz, _) = await fenster.EndeAsync(0, k.Ablage, name, w);
        var wert = new SzenarioWert();
        foreach (var key in new[] { "tx_je_s", "queries_je_s", "cpu_prozent", "rss_mb" }) wert.Kennzahlen[key] = kz[key];
        Console.WriteLine($"    {kz["tx_je_s"]:F1} Tx/s · {kz["queries_je_s"]:F1} Q/s · CPU {kz["cpu_prozent"]:F1} % · RSS {kz["rss_mb"]:F0} MB");
        return wert;
    }

    // ── S4 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    private static async Task<SzenarioWert> DatensatzAsync(Kontext k, Anwendungslauf lauf, int w)
    {
        Console.WriteLine($"  S4 {Titel("S4")}");
        k.Ressourcen.Abschnitt($"S4-w{w}");
        var wert = new SzenarioWert();
        var paare = (int)await lauf.Db.ZahlAsync($"select count(*) from {lauf.Db.Schema}.mt_events where type = 'image_pair_erstellt'");
        var ds = new Datensatz(lauf.Eingang);
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        var z = await ds.LaufeAsync(paare, TimeSpan.FromMinutes(15));
        var (kz, _) = await fenster.EndeAsync(paare, k.Ablage, "S4", w);
        var gesamt = z.AufnahmeS + z.EinfrierenS;
        Console.WriteLine($"    {paare} Paare · Aufnahme {z.AufnahmeS:F1} s · Einfrieren {z.EinfrierenS:F1} s · {paare / gesamt:F0} Paare/s");
        wert.Kennzahlen["saettigung_je_s"] = paare / gesamt;
        wert.Kennzahlen["paare"] = paare;
        wert.Kennzahlen["dauer_aufnahme_s"] = z.AufnahmeS;
        wert.Kennzahlen["dauer_einfrieren_s"] = z.EinfrierenS;
        foreach (var (key, v) in kz) wert.Kennzahlen[key] = v;
        wert.Pruefungen.AddRange(await ds.PruefeAsync(lauf.Db, paare));
        return wert;
    }

    // ── S3 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// S3: erst die Rampe (Sammelvorgänge zu je 200 Teilen) für die Sättigung, dann je N ein Sammelvorgang mit N Teilen bei 50 % der
    /// Sättigung — Latenz, Zeit je Teil am Anfang und am Ende des Streams, Tx/s, CPU.
    /// </summary>
    private static async Task<List<(string Name, SzenarioWert Wert)>> SammelAsync(Kontext k, Anwendungslauf lauf, int w)
    {
        var sv = new Sammelvorgang(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
        var liste = new List<(string, SzenarioWert)>();
        Console.WriteLine($"  S3 {Titel("S3")}");
        k.Ressourcen.Abschnitt($"S3-w{w}");
        var rampe = new Rampe(sv, k.Optionen.RampeSammel);
        var (satt, stufen) = await rampe.SaettigungAsync(default);
        var wert = new SzenarioWert();
        wert.Kennzahlen["saettigung_je_s"] = satt;
        wert.Details["stufen"] = Stufen(stufen);
        foreach (var v in rampe.Alle) k.Ablage.Latenz("S3", w, v);
        wert.Pruefungen.AddRange(await sv.PruefeAsync(lauf.Db, sv.Laeufe.ToList(), "Rampe"));
        liste.Add(("S3", wert));
        Console.WriteLine($"    Sättigung {satt:F1} Teile/s");

        foreach (var n in k.Optionen.SammelN)
        {
            var name = $"S3-{n}";
            Console.WriteLine($"  {name} {Titel(name)}");
            k.Ressourcen.Abschnitt($"{name}-w{w}");
            sv.NeueGroesse(n);
            var vorher = sv.Laeufe.Count;
            var r = new Rampe(sv, k.Optionen.RampeSammel);
            var rate = Math.Max(0.5, satt / 2);
            var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
            await fenster.StarteAsync();
            var st = await r.StufeAsync($"latenz-n{n}", rate, TimeSpan.FromSeconds(n / rate), default);
            if (!await r.LeerlaufenAsync(TimeSpan.FromSeconds(300), default)) throw new RampenFehler($"{name}: baut sich nicht ab");
            var (kz, _) = await fenster.EndeAsync(st.Anzahl, k.Ablage, name, w);
            var lat = r.Alle.Select(v => v.LatenzMs).ToList();
            var zehntel = Math.Max(1, lat.Count / 10);
            var wn = new SzenarioWert();
            wn.Kennzahlen["latenz_p50_ms"] = Statistik.Perzentil(lat, 50);
            wn.Kennzahlen["latenz_p95_ms"] = Statistik.Perzentil(lat, 95);
            wn.Kennzahlen["latenz_p99_ms"] = Statistik.Perzentil(lat, 99);
            wn.Kennzahlen["zeit_je_teil_erstes_zehntel_ms"] = Statistik.Median(lat.Take(zehntel));
            wn.Kennzahlen["zeit_je_teil_letztes_zehntel_ms"] = Statistik.Median(lat.Skip(lat.Count - zehntel));
            foreach (var (key, v) in kz) wn.Kennzahlen[key] = v;
            wn.Details["latenzlauf"] = StufeJson(st);
            Console.WriteLine($"    {rate:F1} Teile/s: p50 {wn.Kennzahlen["latenz_p50_ms"]:F0} p99 {wn.Kennzahlen["latenz_p99_ms"]:F0} ms · erstes/letztes Zehntel " +
                              $"{wn.Kennzahlen["zeit_je_teil_erstes_zehntel_ms"]:F0}/{wn.Kennzahlen["zeit_je_teil_letztes_zehntel_ms"]:F0} ms · {kz["tx_je_s"]:F0} Tx/s · CPU {kz["cpu_prozent"]:F0} %");
            foreach (var v in r.Alle) k.Ablage.Latenz(name, w, v);
            wn.Pruefungen.AddRange(await sv.PruefeAsync(lauf.Db, sv.Laeufe.Skip(vorher).ToList(), $"N={n}"));
            liste.Add((name, wn));
        }
        return liste;
    }

    // ── S5 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    private static async Task<SzenarioWert> PruefAsync(Kontext k, Anwendungslauf lauf, int w)
    {
        Console.WriteLine($"  S5 {Titel("S5")}");
        k.Ressourcen.Abschnitt($"S5-w{w}");
        var wert = new SzenarioWert();
        var pd = new Pruefdomaene(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
        await pd.EinrichtenAsync();
        var rampe = new Rampe(pd, k.Optionen.RampePruef);
        var (satt, stufen) = await rampe.SaettigungAsync(default);
        await LatenzlaufAsync(k, lauf, rampe, "S5", w, satt, k.Optionen.RampePruef, wert);
        wert.Details["stufen"] = Stufen(stufen);
        foreach (var v in rampe.Alle) k.Ablage.Latenz("S5", w, v);
        await WarteAufReaktionAsync(lauf);
        var reaktion = pd.ReaktionMs.ToList();
        wert.Kennzahlen["reaktion_p50_ms"] = Statistik.Perzentil(reaktion, 50);
        wert.Kennzahlen["reaktion_p99_ms"] = Statistik.Perzentil(reaktion, 99);
        wert.Kennzahlen["reaktion_anzahl"] = reaktion.Count;
        wert.Pruefungen.AddRange(await pd.PruefeAsync(lauf.Db));
        return wert;
    }

    /// <summary>Die Reaktion läuft dem Prozess nach (eigener Konsument): warten, bis jede auslösende Reservierung ihren Nachschub hat.</summary>
    private static async Task WarteAufReaktionAsync(Anwendungslauf lauf)
    {
        var s = lauf.Db.Schema;
        for (var i = 0; i < 300; i++)
        {
            var offen = await lauf.Db.ZahlAsync($"""
                select (select count(*) from {s}.mt_events where type = 'posten_reserviert' and (data->>'Fehlmenge')::int > 0)
                     - (select count(distinct headers->>'ursache') from {s}.mt_events where type = 'posten_zugebucht' and headers->>'ueber' = 'reaktion:NachschubBeiMindestbestand')
                """);
            if (offen <= 0) return;
            await Task.Delay(1000);
        }
    }
}
