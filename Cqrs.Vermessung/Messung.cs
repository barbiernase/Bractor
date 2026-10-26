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
                await using (var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", $"beispiel-w{w}", zuruecksetzen: true))
                {
                    if (o.Diagnose) await Zeitreihe.LaufeAsync(k, lauf, "L1-leer", o.ZeitreiheLeer, o.ZeitreiheTakt);
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
                            wert.Pruefungen.AddRange(await sv.PruefeAsync(lauf.Db, sv.Laeufe.Skip(vorher).ToList(), $"S3 ×{sender}", wert.Kennzahlen["abgebaut"] > 0));
                            Merke($"S3-{sender}", wert);
                        }
                    }
                    if (o.Diagnose) await Zeitreihe.LaufeAsync(k, lauf, "L1-nach-last", o.ZeitreiheNachLast, o.ZeitreiheTakt);
                    else Merke("L1", await LeerlaufAsync(k, lauf, "L1", w, nichtAbgebaut.Count == 0));
                }
                if (o.Diagnose) await NeustartZeitreiheAsync(k, k.Beispiel, "Host.Grpc", $"beispiel-neustart-w{w}", "L1-neustart");
                if (o.Szenarien.Contains("S2"))
                {
                    var extern_ = new Dictionary<string, string> { ["Funktionen__IBildaufbereitung__Laufort"] = "Extern" };
                    await using var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", $"beispiel-s2-w{w}", zuruecksetzen: false,
                        extern_);
                    await lauf.StarteWorkerAsync(k.Beispiel, "IBildaufbereitung", $"worker-bildaufbereitung-w{w}");
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
                if (o.Diagnose) await Zeitreihe.LaufeAsync(k, lauf, "L5-leer", o.ZeitreiheLeer, o.ZeitreiheTakt);
                SzenarioWert? letzter = null;
                var alleAbgebaut = true;
                foreach (var sender in o.SenderS5)
                {
                    letzter = await BlockAsync(k, lauf, $"S5-{sender}", pd, sender, o.MaxS5, w);
                    alleAbgebaut &= letzter.Kennzahlen["abgebaut"] > 0;
                    Merke($"S5-{sender}", letzter);
                }
                await Pruefdomaene.WarteAufNachschubAsync(lauf);
                var nachschub = pd.NachschubMs.ToList();
                letzter!.Kennzahlen["nachschub_p50_ms"] = Statistik.Perzentil(nachschub, 50);
                letzter.Kennzahlen["nachschub_p99_ms"] = Statistik.Perzentil(nachschub, 99);
                var pruef = await pd.PruefeAsync(lauf.Db, alleAbgebaut);   // über alle S5-Blöcke
                letzter.Pruefungen.AddRange(pruef);
                alleOk &= pruef.All(p => p.Ok);
                foreach (var p in pruef.Where(p => !p.Ok)) Console.WriteLine($"  ✗ S5: {p.Name} — soll {p.Soll}, ist {p.Ist}");
                if (o.Diagnose) await Zeitreihe.LaufeAsync(k, lauf, "L5-nach-last", o.ZeitreiheNachLast, o.ZeitreiheTakt);
                else Merke("L5", await LeerlaufAsync(k, lauf, "L5", w, alleAbgebaut));
                Global("Prüfdomäne", w, await GlobalePruefung.PruefeAsync(lauf.Db, "Prüfdomäne"));
                if (o.Diagnose)
                {
                    await lauf.DisposeAsync();
                    await NeustartZeitreiheAsync(k, k.Pruef, "Host.Pruefdomaene", $"pruefdomaene-neustart-w{w}", "L5-neustart");
                }
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
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle, lauf);
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
        // Aufwärmen (nicht gewertet, PF-19): ein eigener Datensatz läuft einmal ganz durch — die erste Nutzung der Datensatz-Typen (Marten erzeugt
        //   ihren Code zur Laufzeit), JIT; sonst misst S4 den Kaltstart. Fenster, Kennzahlen und Prüfungen gelten nur dem zweiten.
        await new Datensatz(lauf.Eingang).LaufeAsync(paare, TimeSpan.FromMinutes(5));
        var ds = new Datensatz(lauf.Eingang);
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle, lauf);
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
    /// <summary>
    /// Grundlast ohne Eingang, mit allem, was die Blöcke davor angelegt haben (PF-2, PF-4, PF-14). Zuerst der <b>Nachlauf</b>: ab dem Ende
    /// des letzten Blocks, bis alle durablen Konsumenten am Kopf des Logs stehen (<see cref="Konsumentenstand.Eingeholt"/>) — die Partitionen
    /// lesen dann einmal nach, was sie unter Last nicht bestätigt vorrücken konnten; seine Dauer und seine Queries sind eigene Kennzahlen.
    /// Danach das Leerlauf-Fenster über ganze Poll-Perioden (<see cref="Optionen.Leerlauf"/>), damit jeder periodische Takt gleich oft
    /// hineinfällt. Früher maß ein 15-s-Fenster 5 s nach dem letzten Block — je nach Lage traf es den Nachlauf oder nicht.
    /// </summary>
    private static async Task<SzenarioWert> LeerlaufAsync(Kontext k, Anwendungslauf lauf, string name, int w, bool ohneRueckstand)
    {
        if (!ohneRueckstand) Console.WriteLine("    (ein Block davor wurde nicht abgebaut — der Leerlauf misst den Rückstand mit)");
        Console.WriteLine($"  {name} {Titel(name)}");
        k.Ressourcen.Abschnitt($"{name}-nachlauf-w{w}");
        var nachlauf = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await nachlauf.StarteAsync();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var stand = await Konsumentenstand.LiesAsync(lauf.Db);
        while (!stand.Eingeholt && sw.Elapsed < k.Optionen.NachlaufFrist)
        {
            await Task.Delay(1000);
            stand = await Konsumentenstand.LiesAsync(lauf.Db);
        }
        var nachlaufS = stand.Eingeholt ? sw.Elapsed.TotalSeconds : double.NaN;
        var (nkz, _) = await nachlauf.EndeAsync(0, k.Ablage, $"{name}-nachlauf", w);
        Console.WriteLine(stand.Eingeholt
            ? $"    Nachlauf {nachlaufS:F0} s · {nkz["queries"]:F0} Queries · CPU {nkz["cpu_prozent"]:F1} % (Kopf {stand.Kopf}, Projektionen ab {stand.Projektion}, Funktionen ab {stand.Funktion})"
            : $"    Nachlauf nicht vorbei nach {k.Optionen.NachlaufFrist.TotalSeconds:F0} s (Kopf {stand.Kopf}, Projektionen ab {stand.Projektion}, Funktionen ab {stand.Funktion}) — der Leerlauf misst ihn mit");

        k.Ressourcen.Abschnitt($"{name}-w{w}");
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle, lauf);
        await fenster.StarteAsync();
        await Task.Delay(k.Optionen.Leerlauf);
        var (kz, _) = await fenster.EndeAsync(0, k.Ablage, name, w);
        var wert = new SzenarioWert();
        foreach (var key in new[] { "tx_je_s", "queries_je_s", "cpu_prozent", "rss_mb", "rss_ende_mb", "rss_lauf_max_mb", "rss_ende_anteil",
                     "aktive_actors", "aktive_actors_hoechst" })
            wert.Kennzahlen[key] = kz[key];
        wert.Kennzahlen["nachlauf_s"] = nachlaufS;
        wert.Kennzahlen["nachlauf_queries"] = nkz["queries"];
        wert.Details["mit_rueckstand"] = !ohneRueckstand;
        wert.Details["nachlauf_eingeholt"] = stand.Eingeholt;
        Console.WriteLine($"    {kz["tx_je_s"]:F1} Tx/s · {kz["queries_je_s"]:F1} Q/s · CPU {kz["cpu_prozent"]:F1} % · RSS {kz["rss_mb"]:F0} MB, "
            + $"am Ende {kz["rss_ende_mb"]:F0} MB = {kz["rss_ende_anteil"]:P0} des Höchststands {kz["rss_lauf_max_mb"]:F0} MB · "
            + $"aktive Actors {kz["aktive_actors"]:F0} (Höchststand {kz["aktive_actors_hoechst"]:F0})");
        return wert;
    }

    /// <summary>Diagnose: derselbe Bestand, der Host frisch gestartet, ohne Warmlauf und ohne Last — was tut er von selbst?</summary>
    private static async Task NeustartZeitreiheAsync(Kontext k, Umgebungsdatei u, string hostProjekt, string logName, string reihe)
    {
        await using var lauf = await Anwendungslauf.StarteAsync(k, u, hostProjekt, "host", logName, zuruecksetzen: false, warmlauf: false);
        await Zeitreihe.LaufeAsync(k, lauf, reihe, k.Optionen.ZeitreiheNeustart, k.Optionen.ZeitreiheTakt);
    }
}
