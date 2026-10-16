using System.Diagnostics;
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
/// Die Leistungsmessung (docs/12 §12.5) in wenigen Minuten, je Anwendung ein Host auf frischer DB. Beispieldomäne: <b>Bildeingang</b> (Stoß
/// TIFF-Paare: Quelle, Fluss mit Funktionen, Projektion, Query) und <b>Heiß</b> (Teile von 64 Sendern an EINEN Sammelvorgang), je dreimal;
/// danach der <b>Leerlauf</b> und der <b>Neuaufbau</b> aller Projektionen über das so entstandene Log. Prüfdomäne: <b>Verteilt</b> ruhig
/// (1 Sender) und an der Decke (64 Sender) — Schreiben, Prozess (Diamant, Kompensation, Fan-out), Reaktion —, je dreimal, danach der Leerlauf.
/// Nach jedem Block die Exactly-once-Prüfung, je Anwendung die globale.
/// </summary>
internal static class Leistung
{
    /// <summary>Die Szenarien in Tabellen-Reihenfolge; <c>Tabelle</c> „last“ (mit Vorgängen) oder „ruhe“ (ohne Eingang).</summary>
    public static readonly (string Name, string Titel, string Tabelle)[] Szenarien =
    [
        ("Bildeingang", "Bildeingang: Quelle, Fluss, Projektion (Paare)", "last"),
        ("Heiss", "Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile)", "last"),
        ("Verteilt-1", "Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge)", "last"),
        ("Verteilt-64", "Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge)", "last"),
        ("Leerlauf-Beispiel", "Leerlauf Beispieldomäne", "ruhe"),
        ("Leerlauf-Pruef", "Leerlauf Prüfdomäne", "ruhe"),
        ("Neuaufbau", "Neuaufbau aller Projektionen", "ruhe"),
    ];

    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var o = k.Optionen;
        var werte = new Dictionary<string, List<SzenarioWert>>();
        var global = new JsonArray();
        var alleOk = true;
        void Merke(string name, SzenarioWert w)
        {
            if (!werte.TryGetValue(name, out var l)) werte[name] = l = new();
            w.Details["fremdlast_danach"] = Werkzeuge.Fremdlast();
            l.Add(w);
            alleOk &= w.Pruefungen.All(p => p.Ok);
            foreach (var p in w.Pruefungen.Where(p => !p.Ok)) Console.WriteLine($"  ✗ {name}: {p.Name} — soll {p.Soll}, ist {p.Ist}");
        }
        void Global(string anwendung, List<Pruefung> g)
        {
            alleOk &= g.All(p => p.Ok);
            global.Add(new JsonObject { ["anwendung"] = anwendung, ["pruefungen"] = new JsonArray(g.Select(p => (JsonNode)p.Json()).ToArray()) });
            foreach (var p in g.Where(p => !p.Ok)) Console.WriteLine($"  ✗ {anwendung}: {p.Name} — soll {p.Soll}, ist {p.Ist}");
        }

        // ── Beispieldomäne ──
        await using (var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", "beispiel", zuruecksetzen: true,
                         mitDaten: ["prozess_gestartet"]))
        {
            var sv = new Sammelvorgang(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
            var abgebaut = true;
            for (var w = 1; w <= o.Wiederholungen; w++)
            {
                var bild = await BildeingangAsync(k, lauf, w);
                abgebaut &= bild.Kennzahlen["abgebaut"] > 0;
                Merke("Bildeingang", bild);

                sv.NeueGroesse(1_000_000);
                var vorher = sv.Laeufe.Count;
                var heiss = await BlockAsync(k, lauf, "Heiss", sv, o.Sender, o.Teile, w);
                abgebaut &= heiss.Kennzahlen["abgebaut"] > 0;
                heiss.Pruefungen.AddRange(await sv.PruefeAsync(lauf.Db, sv.Laeufe.Skip(vorher).ToList(), $"Heiß {w}", heiss.Kennzahlen["abgebaut"] > 0));
                Merke("Heiss", heiss);
            }
            Merke("Leerlauf-Beispiel", await LeerlaufAsync(k, lauf, "Leerlauf-Beispiel", abgebaut));
            for (var w = 1; w <= o.Wiederholungen; w++) Merke("Neuaufbau", await NeuaufbauAsync(k, lauf, w));
            Global("Beispieldomäne", await GlobalePruefung.PruefeAsync(lauf.Db, "Beispieldomäne"));
        }

        // ── Prüfdomäne ──
        await using (var lauf = await Anwendungslauf.StarteAsync(k, k.Pruef, "Host.Pruefdomaene", "host", "pruefdomaene", zuruecksetzen: true,
                         mitDaten: ["posten_reserviert"]))
        {
            var pd = new Pruefdomaene(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
            await pd.EinrichtenAsync();
            await AufwaermenAsync(pd);
            var abgebaut = true;
            for (var w = 1; w <= o.Wiederholungen; w++)
            {
                pd.LeereReaktion();
                var ruhig = await BlockAsync(k, lauf, "Verteilt-1", pd, 1, o.VorgaengeRuhig, w);
                var decke = await BlockAsync(k, lauf, "Verteilt-64", pd, o.Sender, o.VorgaengeDecke, w);
                abgebaut &= ruhig.Kennzahlen["abgebaut"] > 0 && decke.Kennzahlen["abgebaut"] > 0;
                // Die Reaktion läuft dem Prozess nach (eigener Konsument): ihre Latenz (Reservierung → Nachschub im Log) über beide Blöcke
                //   der Wiederholung, gewertet beim Block an der Decke, der die meisten auslöst.
                await WarteAufReaktionAsync(lauf);
                var reaktion = pd.ReaktionMs.ToList();
                decke.Kennzahlen["reaktion_p50_ms"] = Statistik.Perzentil(reaktion, 50);
                decke.Kennzahlen["reaktion_p99_ms"] = Statistik.Perzentil(reaktion, 99);
                Merke("Verteilt-1", ruhig);
                Merke("Verteilt-64", decke);
            }
            var pruef = await pd.PruefeAsync(lauf.Db, abgebaut);   // über alle Blöcke
            alleOk &= pruef.All(p => p.Ok);
            werte["Verteilt-64"][^1].Pruefungen.AddRange(pruef);
            foreach (var p in pruef.Where(p => !p.Ok)) Console.WriteLine($"  ✗ Verteilt: {p.Name} — soll {p.Soll}, ist {p.Ist}");
            Merke("Leerlauf-Pruef", await LeerlaufAsync(k, lauf, "Leerlauf-Pruef", abgebaut));
            Global("Prüfdomäne", await GlobalePruefung.PruefeAsync(lauf.Db, "Prüfdomäne"));
        }

        var szenarien = new JsonObject();
        foreach (var (name, titel, tabelle) in Szenarien)
        {
            if (!werte.TryGetValue(name, out var liste)) continue;
            var schluessel = liste.SelectMany(w => w.Kennzahlen.Keys).Distinct().ToList();
            JsonObject Je(Func<IEnumerable<double>, double> f) => new(schluessel.Select(key => KeyValuePair.Create(key,
                Ablage.Zahl(f(liste.Select(w => w.Kennzahlen.TryGetValue(key, out var v) ? v : double.NaN))))));
            szenarien[name] = new JsonObject
            {
                ["titel"] = titel,
                ["tabelle"] = tabelle,
                ["wiederholungen"] = new JsonArray(liste.Select(w => (JsonNode)new JsonObject
                {
                    ["kennzahlen"] = new JsonObject(w.Kennzahlen.Select(kv => KeyValuePair.Create(kv.Key, Ablage.Zahl(kv.Value)))),
                    ["details"] = w.Details.DeepClone(),
                }).ToArray()),
                ["median"] = Je(Statistik.Median),
                ["min"] = Je(x => x.Where(d => !double.IsNaN(d)).DefaultIfEmpty(double.NaN).Min()),
                ["max"] = Je(x => x.Where(d => !double.IsNaN(d)).DefaultIfEmpty(double.NaN).Max()),
                ["exactly_once"] = new JsonObject
                {
                    ["ok"] = liste.All(w => w.Pruefungen.All(p => p.Ok)),
                    ["pruefungen"] = new JsonArray(liste.SelectMany((w, i) => w.Pruefungen.Select(p => { var j = p.Json(); j["wiederholung"] = i + 1; return (JsonNode)j; })).ToArray()),
                },
            };
        }
        return new JsonObject { ["szenarien"] = szenarien, ["global"] = global, ["exactly_once_ok"] = alleOk };
    }

    /// <summary>Ein Lastblock: <paramref name="sender"/> Sender schicken höchstens <paramref name="anzahl"/> Vorgänge so schnell, wie der
    /// Eingang annimmt; danach der Abbau bis alles sichtbar. Durchsatz, Latenz, Kosten je Vorgang, CPU, RSS.</summary>
    private static async Task<SzenarioWert> BlockAsync(Kontext k, Anwendungslauf lauf, string name, ILast last, int sender, int anzahl, int w)
    {
        Console.WriteLine($"  {name} ({w}/{k.Optionen.Wiederholungen})");
        k.Ressourcen.Abschnitt($"{name}-{w}");
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle, lauf);
        await fenster.StarteAsync();
        var (b, vorgaenge) = await Volllast.LaufeAsync(last, name, sender, k.Optionen.Fenster, k.Optionen.AbbauFrist, anzahl);
        var (kz, _) = await fenster.EndeAsync(b.Angenommen, k.Ablage, name, w);
        var wert = new SzenarioWert();
        wert.Kennzahlen["durchsatz_je_s"] = b.DurchsatzJeS;
        wert.Kennzahlen["angenommen_je_s"] = b.AngenommenJeS;
        wert.Kennzahlen["latenz_p50_ms"] = b.P50;
        wert.Kennzahlen["latenz_p99_ms"] = b.P99;
        wert.Kennzahlen["rueckstand"] = b.Rueckstand;
        wert.Kennzahlen["abbau_s"] = b.AbbauS;
        wert.Kennzahlen["abgebaut"] = b.Abgebaut ? 1 : 0;
        wert.Kennzahlen["fehler"] = b.Fehler;
        foreach (var (key, v) in kz) wert.Kennzahlen[key] = v;
        Console.WriteLine($"      {kz["tx_je_vorgang"]:F1} Tx, {kz["queries_je_vorgang"]:F0} Queries, {kz["cpu_ms_je_vorgang"]:F1} ms CPU je Vorgang · RSS {kz["rss_mb"]:F0} MB");
        wert.Details["block"] = new JsonObject
        {
            ["sender"] = sender, ["angenommen"] = b.Angenommen, ["fehler"] = b.Fehler, ["sichtbar"] = b.Sichtbar,
            ["fenster_s"] = Ablage.Zahl(b.FensterS), ["abbau_s"] = Ablage.Zahl(b.AbbauS), ["abgebaut"] = b.Abgebaut,
        };
        foreach (var v in vorgaenge) k.Ablage.Latenz(name, w, v);
        return wert;
    }

    /// <summary>Aufwärmen (nicht gewertet): erste Aktivierungen, JIT, Marten-Schema.</summary>
    private static async Task AufwaermenAsync(ILast last, int anzahl = 50)
    {
        var (b, _) = await Volllast.LaufeAsync(last, "aufwaermen", 4, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), anzahl);
        if (!b.Abgebaut) throw new InvalidOperationException("Aufwärmen baut sich nicht ab — der Host ist nicht bereit.");
    }

    /// <summary>Bildeingang: ein Stoß Dateien — ein Sender schreibt die Paare so schnell, wie das Dateisystem sie nimmt (eine Kamera nach
    /// einem Stillstand). Jede Wiederholung trägt eigene Dateinamen.</summary>
    private static async Task<SzenarioWert> BildeingangAsync(Kontext k, Anwendungslauf lauf, int w)
    {
        await using var bild = new Bildeingang(lauf.Eingang, k.Beispiel.Quellpfad!, 2030, w);
        if (w == 1) await AufwaermenAsync(bild, 10);
        var wert = await BlockAsync(k, lauf, "Bildeingang", bild, 1, k.Optionen.Bildpaare, w);
        wert.Pruefungen.AddRange(await bild.PruefeAsync(lauf.Db, 2 * bild.Paare.Count, wert.Kennzahlen["abgebaut"] > 0));
        return wert;
    }

    /// <summary>
    /// Grundlast ohne Eingang, mit allem, was die Blöcke davor angelegt haben (PF-14). Zuerst der <b>Nachlauf</b>: ab dem Ende des letzten
    /// Blocks, bis alle durablen Konsumenten am Kopf des Logs stehen (<see cref="Konsumentenstand.Eingeholt"/>) — seine Dauer und seine Queries
    /// sind eigene Kennzahlen. Danach das Leerlauf-Fenster über ganze Poll-Perioden.
    /// </summary>
    private static async Task<SzenarioWert> LeerlaufAsync(Kontext k, Anwendungslauf lauf, string name, bool ohneRueckstand)
    {
        if (!ohneRueckstand) Console.WriteLine("    (ein Block davor wurde nicht abgebaut — der Leerlauf misst den Rückstand mit)");
        Console.WriteLine($"  {name}");
        k.Ressourcen.Abschnitt($"{name}-nachlauf");
        var nachlauf = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await nachlauf.StarteAsync();
        var sw = Stopwatch.StartNew();
        var stand = await Konsumentenstand.LiesAsync(lauf.Db);
        while (!stand.Eingeholt && sw.Elapsed < k.Optionen.NachlaufFrist)
        {
            await Task.Delay(1000);
            stand = await Konsumentenstand.LiesAsync(lauf.Db);
        }
        var nachlaufS = stand.Eingeholt ? sw.Elapsed.TotalSeconds : double.NaN;
        var (nkz, _) = await nachlauf.EndeAsync(0, k.Ablage, $"{name}-nachlauf", 1);

        k.Ressourcen.Abschnitt(name);
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle, lauf);
        await fenster.StarteAsync();
        await Task.Delay(k.Optionen.Leerlauf);
        var (kz, _) = await fenster.EndeAsync(0, k.Ablage, name, 1);
        var wert = new SzenarioWert();
        foreach (var key in new[] { "tx_je_s", "queries_je_s", "cpu_prozent", "rss_mb", "rss_ende_mb", "rss_lauf_max_mb", "aktive_actors" })
            wert.Kennzahlen[key] = kz[key];
        wert.Kennzahlen["nachlauf_s"] = nachlaufS;
        wert.Kennzahlen["nachlauf_queries"] = nkz["queries"];
        wert.Details["mit_rueckstand"] = !ohneRueckstand;
        wert.Details["nachlauf_eingeholt"] = stand.Eingeholt;
        Console.WriteLine($"    Nachlauf {nachlaufS:F0} s ({nkz["queries"]:F0} Queries) · Leerlauf {kz["queries_je_s"]:F1} Queries/s, CPU {kz["cpu_prozent"]:F1} %, "
                          + $"RSS am Ende {kz["rss_ende_mb"]:F0} MB");
        return wert;
    }

    /// <summary>
    /// Neuaufbau aller Projektionen über das Log der Blöcke davor: Host stoppen, den gespeicherten Projektions-Stand löschen (in Sim heißt das
    /// „Code geändert“, <c>ProjektionsStandDienst</c>), Host starten — die Leseseite wird geleert, jede Partition jeder Projektion liest ab 0.
    /// Gemessen wird über den Rückstand in <c>/monitoring/metrics</c> (relevante Events jenseits der Positionen, Summe über die Projektionen):
    /// vom ersten Abbau bis null, alle 50 ms abgefragt. Danach muss das Read-Model wieder dem Log entsprechen. Dreimal hintereinander — ein
    /// Neuaufbau über das Log dieser Messung dauert unter einer Sekunde, die Spanne zeigt die Unschärfe der Abfrage.
    /// </summary>
    private static async Task<SzenarioWert> NeuaufbauAsync(Kontext k, Anwendungslauf lauf, int w)
    {
        Console.WriteLine($"  Neuaufbau ({w}/{k.Optionen.Wiederholungen})");
        var kopf = await lauf.Db.ZahlAsync($"select coalesce(max(seq_id), 0) from {lauf.Db.Schema}.mt_events");
        await lauf.Host.StoppeAsync();
        await lauf.Db.AusfuehrenAsync(lauf.Db.Name, $"delete from {lauf.Db.Schema}.stand where schluessel like 'projektion:%'");
        k.Ressourcen.Abschnitt($"Neuaufbau-{w}");
        var fenster = new Messfenster(lauf.Db, k.Ressourcen, lauf.Rolle);
        await fenster.StarteAsync();
        var gesamt = Stopwatch.StartNew();
        await lauf.Host.StarteAsync();
        var start = gesamt.Elapsed;

        long hoechst = -1;
        TimeSpan? beginn = null, ende = null, zuletztHoechst = null;
        while (gesamt.Elapsed < k.Optionen.NeuaufbauFrist)
        {
            var r = await lauf.Eingang.KonsumRueckstandAsync();
            var t = gesamt.Elapsed;
            if (r > hoechst) { hoechst = r; zuletztHoechst = t; }
            else if (r == hoechst) zuletztHoechst = t;
            if (beginn == null && r >= 0 && r < hoechst) beginn = zuletztHoechst;
            if (r == 0 && hoechst > 0) { ende = t; break; }
            await Task.Delay(50);
        }
        var (kz, _) = await fenster.EndeAsync((int)Math.Max(0, hoechst), k.Ablage, "Neuaufbau", w);
        var dauer = ende is { } e && beginn is { } b ? (e - b).TotalSeconds : double.NaN;
        var wert = new SzenarioWert();
        wert.Kennzahlen["events"] = hoechst;
        wert.Kennzahlen["log_events"] = kopf;
        wert.Kennzahlen["dauer_s"] = dauer;
        wert.Kennzahlen["events_je_s"] = hoechst / dauer;
        wert.Kennzahlen["start_s"] = start.TotalSeconds;
        wert.Kennzahlen["bis_eingeholt_s"] = ende?.TotalSeconds ?? double.NaN;
        wert.Kennzahlen["cpu_prozent"] = kz["cpu_prozent"];
        wert.Kennzahlen["queries"] = kz["queries"];
        wert.Pruefungen.Add(new Pruefung("Neuaufbau: der Rückstand fällt auf null", ende != null && hoechst > 0, "ja",
            ende != null ? "ja" : $"Rückstand {hoechst} nach {k.Optionen.NeuaufbauFrist.TotalSeconds:0} s"));
        wert.Pruefungen.AddRange((await Bildeingang.ReadModelAsync(lauf.Eingang, lauf.Db))
            .Select(p => p with { Name = "nach dem Neuaufbau: " + p.Name }));
        Console.WriteLine($"    {hoechst} Projektions-Events (Log {kopf}) in {dauer:F1} s = {hoechst / dauer:F0}/s · Host-Start {start.TotalSeconds:F1} s");
        return wert;
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
