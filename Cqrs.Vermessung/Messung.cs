using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>Was ein Block liefert: Kennzahlen, Exactly-once-Prüfungen, Details.</summary>
internal sealed class SzenarioWert
{
    public Dictionary<string, double> Kennzahlen { get; } = new();
    public List<Pruefung> Pruefungen { get; } = new();
    public JsonObject Details { get; } = new();
}

/// <summary>
/// Die Messung (§0.1, Höchstlast) in wenigen Minuten. Je Durchgang frische DBs; Beispieldomäne: S1 (Stoß TIFF-Paare), S4 (Datensatz über die
/// Paare aus S1), S3 (Teile an EINEN Sammelvorgang, 8 und 64 Sender), L1 (Leerlauf); dieselbe DB mit Bild-Funktionen im Python-Worker: S2
/// (kleiner Stoß); Prüfdomäne: S5 (Vorgänge an viele Aggregate, 1, 8 und 64 Sender), L5. Jeder Block sendet höchstens 15 s und baut danach
/// ab; nach jedem Szenario die Exactly-once-Prüfung, je Anwendung die globale.
/// </summary>
internal static class Messung
{
    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var o = k.Optionen;
        var werte = new Dictionary<string, List<SzenarioWert>>();
        var global = new JsonArray();
        var alleOk = true;
        var nichtAbgebaut = new List<string>();
        void Merke(string name, SzenarioWert w)
        {
            if (!werte.TryGetValue(name, out var l)) werte[name] = l = new();
            w.Details["fremdlast_danach"] = Werkzeuge.Fremdlast();
            l.Add(w);
            alleOk &= w.Pruefungen.All(p => p.Ok);
            if (w.Kennzahlen.TryGetValue("abgebaut", out var a) && a < 1) nichtAbgebaut.Add(name);
            foreach (var p in w.Pruefungen.Where(p => !p.Ok)) Console.WriteLine($"  ✗ {name}: {p.Name} — soll {p.Soll}, ist {p.Ist}");
        }
        void Global(string anwendung, int w, List<Pruefung> g)
        {
            alleOk &= g.All(p => p.Ok);
            global.Add(new JsonObject { ["wiederholung"] = w, ["anwendung"] = anwendung, ["pruefungen"] = new JsonArray(g.Select(p => (JsonNode)p.Json()).ToArray()) });
            foreach (var p in g.Where(p => !p.Ok)) Console.WriteLine($"  ✗ {anwendung}: {p.Name} — soll {p.Soll}, ist {p.Ist}");
        }

        for (var w = 1; w <= o.Wiederholungen; w++)
        {
            Console.WriteLine($"── Wiederholung {w}/{o.Wiederholungen} ──");
            if (o.Szenarien.Overlaps(["S1", "S2", "S3", "S4"]))
            {
                await using (var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", $"beispiel-w{w}", zuruecksetzen: true,
                                 mitDaten: ["prozess_gestartet"]))
                {
                    if (o.Szenarien.Contains("S1") || o.Szenarien.Contains("S4")) Merke("S1", await StossAsync(k, lauf, "S1", 2030, w, o.StossS1));
                    if (o.Szenarien.Contains("S4")) Merke("S4", await DatensatzAsync(k, lauf, w));
                    if (o.Szenarien.Contains("S3"))
                    {
                        var sv = new Sammelvorgang(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
                        foreach (var sender in o.SenderS3)
                        {
                            sv.NeueGroesse(1_000_000);
                            var vorher = sv.Laeufe.Count;
                            var wert = await BlockAsync(k, lauf, $"S3-{sender}", sv, sender, o.MaxS3, w);
                            wert.Kennzahlen["prozessende_nachlauf_s"] = await WarteAufProzessendeAsync(lauf, TimeSpan.FromSeconds(60));
                            wert.Pruefungen.AddRange(await sv.PruefeAsync(lauf.Db, sv.Laeufe.Skip(vorher).ToList(), $"S3 ×{sender}", wert.Kennzahlen["abgebaut"] > 0));
                            Merke($"S3-{sender}", wert);
                        }
                    }
                    Merke("L1", await LeerlaufAsync(k, lauf, "L1", w, nichtAbgebaut.Count == 0));
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
                    Merke("S2", await StossAsync(k, lauf, "S2", 2031, w, o.StossS2));
                }
                Global("Beispieldomäne", w, await GlobalePruefung.PruefeAsync(new Datenbank(k.Beispiel.Verbindung, k.Beispiel.Datenbank, k.Beispiel.Schema), "Beispieldomäne"));
            }
            if (o.Szenarien.Contains("S5"))
            {
                await using var lauf = await Anwendungslauf.StarteAsync(k, k.Pruef, "Host.Pruefdomaene", "host", $"pruefdomaene-w{w}", zuruecksetzen: true,
                    mitDaten: ["posten_reserviert"]);
                var pd = new Pruefdomaene(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
                await pd.EinrichtenAsync();
                await AufwaermenAsync(pd);
                SzenarioWert? letzter = null;
                var alleAbgebaut = true;
                foreach (var sender in o.SenderS5)
                {
                    letzter = await BlockAsync(k, lauf, $"S5-{sender}", pd, sender, o.MaxS5, w);
                    alleAbgebaut &= letzter.Kennzahlen["abgebaut"] > 0;
                    Merke($"S5-{sender}", letzter);
                }
                letzter!.Kennzahlen["prozessende_nachlauf_s"] = await WarteAufProzessendeAsync(lauf, TimeSpan.FromSeconds(60));
                await WarteAufReaktionAsync(lauf);
                var reaktion = pd.ReaktionMs.ToList();
                letzter!.Kennzahlen["reaktion_p50_ms"] = Statistik.Perzentil(reaktion, 50);
                letzter.Kennzahlen["reaktion_p99_ms"] = Statistik.Perzentil(reaktion, 99);
                var pruef = await pd.PruefeAsync(lauf.Db, alleAbgebaut);   // über alle S5-Blöcke
                letzter.Pruefungen.AddRange(pruef);
                alleOk &= pruef.All(p => p.Ok);
                foreach (var p in pruef.Where(p => !p.Ok)) Console.WriteLine($"  ✗ S5: {p.Name} — soll {p.Soll}, ist {p.Ist}");
                Merke("L5", await LeerlaufAsync(k, lauf, "L5", w, alleAbgebaut));
                Global("Prüfdomäne", w, await GlobalePruefung.PruefeAsync(lauf.Db, "Prüfdomäne"));
            }
        }

        var szenarien = new JsonObject();
        foreach (var (name, liste) in werte.OrderBy(kv => kv.Key, StringComparer.Ordinal))
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
            ["nicht_abgebaut"] = new JsonArray(nichtAbgebaut.Distinct().Select(n => (JsonNode)n).ToArray()),
            ["zusammenfassung"] = Zusammenfassung.Zeilen(szenarien),
        };
    }

    public static string Titel(string s) => s switch
    {
        "S1" => "Bildeingang Host, Stoß", "S2" => "Bildeingang mit Python-Worker, Stoß", "S4" => "Datensatz einfrieren",
        "L1" => "Leerlauf Beispieldomäne", "L5" => "Leerlauf Prüfdomäne",
        _ when s.StartsWith("S3-") => $"Sammelvorgang heiß, {s[3..]} Sender",
        _ when s.StartsWith("S5-") => $"Prüfdomäne verteilt, {s[3..]} Sender",
        _ => s,
    };

    /// <summary>Ein Volllast-Block im Messfenster: Durchsatz, Rückstand, Abbau, Latenz, Kosten je Vorgang, CPU, RSS.</summary>
    private static async Task<SzenarioWert> BlockAsync(Kontext k, Anwendungslauf lauf, string name, ILast last, int sender, int maxAnzahl, int w)
    {
        Console.WriteLine($"  {name} {Titel(name)}");
        k.Ressourcen.Abschnitt($"{name}-w{w}");
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        var (b, vorgaenge) = await Volllast.LaufeAsync(last, name, sender, k.Optionen.Fenster, k.Optionen.AbbauFrist, maxAnzahl);
        var (kz, _) = await fenster.EndeAsync(b.Angenommen, k.Ablage, name, w);
        var wert = new SzenarioWert();
        wert.Kennzahlen["angenommen_je_s"] = b.AngenommenJeS;
        wert.Kennzahlen["sichtbar_je_s"] = b.SichtbarJeS;
        wert.Kennzahlen["durchsatz_je_s"] = b.DurchsatzJeS;
        wert.Kennzahlen["rueckstand"] = b.Rueckstand;
        wert.Kennzahlen["abbau_s"] = b.AbbauS;
        wert.Kennzahlen["abgebaut"] = b.Abgebaut ? 1 : 0;
        wert.Kennzahlen["latenz_p50_ms"] = b.P50;
        wert.Kennzahlen["latenz_p95_ms"] = b.P95;
        wert.Kennzahlen["latenz_p99_ms"] = b.P99;
        wert.Kennzahlen["fehler"] = b.Fehler;
        foreach (var (key, v) in kz) wert.Kennzahlen[key] = v;
        var workerCpu = k.Ressourcen.Summe("worker-", fenster.Von, DateTimeOffset.UtcNow);
        if (!double.IsNaN(workerCpu)) wert.Kennzahlen["worker_cpu_prozent"] = workerCpu;
        Console.WriteLine($"      {kz["tx_je_vorgang"]:F1} Tx und {kz["queries_je_vorgang"]:F0} Queries je Vorgang · CPU {kz["cpu_prozent"]:F0} % · RSS {kz["rss_mb"]:F0} MB");
        wert.Details["block"] = new JsonObject
        {
            ["sender"] = sender, ["angenommen"] = b.Angenommen, ["fehler"] = b.Fehler, ["sichtbar_im_fenster"] = b.SichtbarImFenster,
            ["sichtbar"] = b.Sichtbar, ["fenster_s"] = Ablage.Zahl(b.FensterS), ["abbau_s"] = Ablage.Zahl(b.AbbauS), ["abgebaut"] = b.Abgebaut,
        };
        foreach (var v in vorgaenge) k.Ablage.Latenz(name, w, v);
        return wert;
    }

    /// <summary>Aufwärmen (nicht gewertet): erste Aktivierungen, JIT, Marten-Schema — sonst misst der erste Block den Kaltstart.</summary>
    private static async Task AufwaermenAsync(ILast last, int anzahl = 50)
    {
        var (b, _) = await Volllast.LaufeAsync(last, "aufwaermen", 4, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), anzahl);
        if (!b.Abgebaut) throw new InvalidOperationException("Aufwärmen baut sich nicht ab — der Host ist nicht bereit.");
    }

    // ── S1/S2: ein Stoß Dateien ────────────────────────────────────────────────────────────────────────────────────────────
    private static async Task<SzenarioWert> StossAsync(Kontext k, Anwendungslauf lauf, string name, int jahr, int w, int paare)
    {
        await using var bild = new Bildeingang(lauf.Eingang, k.Beispiel.Quellpfad!, jahr, w);
        await AufwaermenAsync(bild, 10);
        // Der Stoß: ein Sender schreibt die Paare so schnell, wie das Dateisystem sie nimmt (eine Kamera nach einem Stillstand).
        var wert = await BlockAsync(k, lauf, name, bild, 1, paare, w);
        wert.Pruefungen.AddRange(await bild.PruefeAsync(lauf.Db, 2 * bild.Paare.Count, wert.Kennzahlen["abgebaut"] > 0));
        return wert;
    }

    // ── S4 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    private static async Task<SzenarioWert> DatensatzAsync(Kontext k, Anwendungslauf lauf, int w)
    {
        Console.WriteLine($"  S4 {Titel("S4")}");
        k.Ressourcen.Abschnitt($"S4-w{w}");
        var paare = (int)await lauf.Db.ZahlAsync($"select count(*) from {lauf.Db.Schema}.mt_events where type = 'image_pair_erstellt'");
        var ds = new Datensatz(lauf.Eingang);
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        var z = await ds.LaufeAsync(paare, TimeSpan.FromMinutes(5));
        var (kz, _) = await fenster.EndeAsync(paare, k.Ablage, "S4", w);
        Console.WriteLine($"    {paare} Paare · Aufnahme {z.AufnahmeS:F1} s · Einfrieren {z.EinfrierenS:F1} s · {paare / (z.AufnahmeS + z.EinfrierenS):F0} Paare/s");
        var wert = new SzenarioWert();
        wert.Kennzahlen["durchsatz_je_s"] = paare / (z.AufnahmeS + z.EinfrierenS);
        wert.Kennzahlen["paare"] = paare;
        wert.Kennzahlen["dauer_aufnahme_s"] = z.AufnahmeS;
        wert.Kennzahlen["dauer_einfrieren_s"] = z.EinfrierenS;
        foreach (var (key, v) in kz) wert.Kennzahlen[key] = v;
        wert.Pruefungen.AddRange(await ds.PruefeAsync(lauf.Db, paare));
        return wert;
    }

    // ── Leerlauf ───────────────────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>Grundlast ohne Eingang (PF-2, PF-4), mit allem, was die Blöcke davor angelegt haben.</summary>
    private static async Task<SzenarioWert> LeerlaufAsync(Kontext k, Anwendungslauf lauf, string name, int w, bool ohneRueckstand)
    {
        if (!ohneRueckstand) Console.WriteLine("    (ein Block davor wurde nicht abgebaut — der Leerlauf misst den Rückstand mit)");
        Console.WriteLine($"  {name} {Titel(name)}");
        k.Ressourcen.Abschnitt($"{name}-w{w}");
        await Task.Delay(TimeSpan.FromSeconds(5));   // Nachlauf der Blöcke abklingen lassen
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        await Task.Delay(k.Optionen.Leerlauf);
        var (kz, _) = await fenster.EndeAsync(0, k.Ablage, name, w);
        var wert = new SzenarioWert();
        foreach (var key in new[] { "tx_je_s", "queries_je_s", "cpu_prozent", "rss_mb" }) wert.Kennzahlen[key] = kz[key];
        wert.Details["mit_rueckstand"] = !ohneRueckstand;
        Console.WriteLine($"    {kz["tx_je_s"]:F1} Tx/s · {kz["queries_je_s"]:F1} Q/s · CPU {kz["cpu_prozent"]:F1} % · RSS {kz["rss_mb"]:F0} MB");
        return wert;
    }

    /// <summary>
    /// Das Prozess-Ende kommt dem sichtbaren Ergebnis nach (der Vorgang schreibt <c>ProzessBeendet</c> erst bei der nächsten Weckung, unter Last
    /// über den Backstop, PF-9): warten, bis kein Vorgang mehr offen ist, und die Zeit dafür liefern (NaN = nicht in der Frist).
    /// </summary>
    private static async Task<double> WarteAufProzessendeAsync(Anwendungslauf lauf, TimeSpan frist)
    {
        var s = lauf.Db.Schema;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            var offen = await lauf.Db.ZahlAsync($"select count(*) from {s}.mt_events g where g.type = 'prozess_gestartet' " +
                $"and not exists (select 1 from {s}.mt_events b where b.stream_id = g.stream_id and b.type = 'prozess_beendet')");
            if (offen == 0) return sw.Elapsed.TotalSeconds;
            await Task.Delay(500);
        }
        return double.NaN;
    }

    /// <summary>Die Reaktion läuft dem Prozess nach (eigener Konsument): warten, bis jede auslösende Reservierung ihren Nachschub hat.</summary>
    public static async Task WarteAufReaktionAsync(Anwendungslauf lauf)
    {
        var s = lauf.Db.Schema;
        for (var i = 0; i < 120; i++)
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
