using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>Die Sättigung aus einer früheren Messung (Standard: die jüngste gültige E2E-Messung) — Grundlage der 50-%-Last der Langläufe.</summary>
internal static class Saettigung
{
    public static (double Wert, string Ordner) Aus(Kontext k, string szenario)
    {
        var basis = Path.Combine(k.Wurzel, "vermessung", "ergebnisse");
        var ordner = k.Optionen.SaettigungAus is { } o ? new[] { Path.IsPathRooted(o) ? o : Path.Combine(basis, o) }
            : Directory.GetDirectories(basis).OrderByDescending(d => d).ToArray();
        foreach (var d in ordner)
        {
            var datei = Path.Combine(d, "ergebnis.json");
            if (!File.Exists(datei)) continue;
            var j = JsonNode.Parse(File.ReadAllText(datei))!;
            if ((string?)j["art"] != "e2e" || (bool?)j["gueltig"] != true && k.Optionen.SaettigungAus == null) continue;
            if (j["szenarien"]?[szenario]?["median"]?["saettigung_je_s"] is { } w) return ((double)w!, Path.GetFileName(d));
        }
        throw new InvalidOperationException($"Keine gültige Messung mit {szenario} gefunden — erst eine Messung laufen lassen oder --saettigung-aus angeben.");
    }
}

/// <summary>Kennzahlen je Zeitfenster eines Langlaufs (Latenz der in diesem Fenster gestarteten Vorgänge, CPU und RSS).</summary>
internal static class Fenster
{
    public static JsonArray Auswerten(IEnumerable<Vorgang> vorgaenge, Ressourcen res, string rolle, DateTimeOffset beginn, DateTimeOffset ende, TimeSpan breite)
    {
        var liste = vorgaenge.ToList();
        var a = new JsonArray();
        for (var t = beginn; t < ende; t += breite)
        {
            var bis = t + breite;
            var drin = liste.Where(v => v.Start >= t && v.Start < bis).ToList();
            var lat = drin.Select(v => double.IsNaN(v.LatenzMs) ? double.PositiveInfinity : v.LatenzMs).ToList();
            var (cpu, rss) = res.Fenster(rolle, t, bis);
            a.Add(new JsonObject
            {
                ["von"] = t.ToString("O"), ["vorgaenge"] = drin.Count, ["nicht_sichtbar"] = drin.Count(v => v.Sichtbar == null),
                ["p50_ms"] = Ablage.Zahl(Statistik.Perzentil(lat, 50)), ["p99_ms"] = Ablage.Zahl(Statistik.Perzentil(lat, 99)),
                ["cpu_prozent"] = Ablage.Zahl(cpu), ["rss_mb"] = Ablage.Zahl(rss),
            });
        }
        return a;
    }
}

/// <summary>
/// Dauerlauf (§0.1): S1 und S3 (N = 1 000) gleichzeitig bei 50 % ihrer Sättigung über <c>--dauer</c> (mindestens 8 h, Phase 2: 24 h).
/// Bewertet: Speicher flach (RSS erste gegen letzte Stunde), Latenz stabil, Exactly-once am Ende.
/// </summary>
internal static class Dauerlauf
{
    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var (satt1, ordner1) = Saettigung.Aus(k, "S1");
        var (satt3, ordner3) = Saettigung.Aus(k, "S3");
        var rateS1 = Math.Max(0.5, satt1 / 2);
        var rateS3 = Math.Max(0.5, satt3 / 2);
        Console.WriteLine($"Dauerlauf {k.Optionen.Dauer}: S1 {rateS1:F1} Paare/s, S3 {rateS3:F1} Teile/s (50 % aus {ordner1} / {ordner3})");
        k.Ressourcen.Abschnitt("dauerlauf");
        await using var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", "dauerlauf", true, null, "prozess_gestartet");
        await using var bild = new Bildeingang(lauf.Eingang, k.Beispiel.Quellpfad!, 2030, 1);
        var sv = new Sammelvorgang(lauf.Eingang, lauf.Log, lauf.Uhrversatz);
        sv.NeueGroesse(1000);
        var beginn = DateTimeOffset.UtcNow;
        var ende = beginn + k.Optionen.Dauer;
        var rampe = new Rampe(bild, k.Optionen.RampeBild);
        var rampe3 = new Rampe(sv, k.Optionen.RampeSammel);
        var fenster = TimeSpan.FromMinutes(10);
        while (DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30) < ende)
        {
            var dauer = ende - DateTimeOffset.UtcNow < fenster ? ende - DateTimeOffset.UtcNow : fenster;
            var a = rampe.StufeAsync("dauer", rateS1, dauer, default);
            var b = rampe3.StufeAsync("dauer", rateS3, dauer, default);
            var (st, st3) = (await a, await b);
            Console.WriteLine($"  {DateTimeOffset.UtcNow - beginn:hh\\:mm}: S1 p50 {st.P50:F0} p99 {st.P99:F0} ms ({st.Sichtbar}/{st.Anzahl}) · S3 p50 {st3.P50:F0} p99 {st3.P99:F0} ms ({st3.Sichtbar}/{st3.Anzahl})");
        }
        var s3 = rampe3.Alle.ToList();
        var s1Ok = await rampe.LeerlaufenAsync(TimeSpan.FromMinutes(15), default);
        var s3Ok = await rampe3.LeerlaufenAsync(TimeSpan.FromMinutes(15), default);
        foreach (var v in rampe.Alle) k.Ablage.Latenz("S1-dauer", 1, v);
        foreach (var v in s3) k.Ablage.Latenz("S3-dauer", 1, v);

        var pruefungen = new List<Pruefung> { new("S1: Rückstand am Ende abgebaut", s1Ok, "ja", s1Ok ? "ja" : "nein"), new("S3: Rückstand am Ende abgebaut", s3Ok, "ja", s3Ok ? "ja" : "nein") };
        pruefungen.AddRange(await bild.PruefeAsync(lauf.Db, 2 * rampe.Alle.Count(v => v.Fehler == null)));
        pruefungen.AddRange(await sv.PruefeAsync(lauf.Db, sv.Laeufe, "S3"));
        pruefungen.AddRange(await GlobalePruefung.PruefeAsync(lauf.Db, "Beispieldomäne"));

        var stunde = TimeSpan.FromHours(Math.Min(1, k.Optionen.Dauer.TotalHours / 4));
        var (_, rssAnfang) = k.Ressourcen.Fenster("host", beginn + stunde, beginn + 2 * stunde);   // die erste Stunde nach dem Aufwärmen
        var (_, rssEnde) = k.Ressourcen.Fenster("host", ende - stunde, ende);
        var alle = rampe.Alle.Concat(s3).ToList();
        var kz = new JsonObject
        {
            ["rate_s1_je_s"] = rateS1, ["rate_s3_je_s"] = rateS3, ["saettigung_aus"] = ordner1,
            ["rss_anfang_mb"] = Ablage.Zahl(rssAnfang), ["rss_ende_mb"] = Ablage.Zahl(rssEnde),
            ["rss_wachstum_prozent"] = Ablage.Zahl((rssEnde - rssAnfang) / rssAnfang * 100),
            ["s1_p99_erste_stunde_ms"] = Ablage.Zahl(P99(rampe.Alle, beginn, beginn + stunde)),
            ["s1_p99_letzte_stunde_ms"] = Ablage.Zahl(P99(rampe.Alle, ende - stunde, ende)),
            ["s3_p99_erste_stunde_ms"] = Ablage.Zahl(P99(s3, beginn, beginn + stunde)),
            ["s3_p99_letzte_stunde_ms"] = Ablage.Zahl(P99(s3, ende - stunde, ende)),
            ["vorgaenge"] = alle.Count,
        };
        Console.WriteLine($"  RSS {rssAnfang:F0} → {rssEnde:F0} MB");
        return new JsonObject
        {
            ["kennzahlen"] = kz,
            ["fenster_s1"] = Fenster.Auswerten(rampe.Alle, k.Ressourcen, "host", beginn, ende, fenster),
            ["fenster_s3"] = Fenster.Auswerten(s3, k.Ressourcen, "host", beginn, ende, fenster),
            ["exactly_once"] = new JsonArray(pruefungen.Select(p => (JsonNode)p.Json()).ToArray()),
            ["exactly_once_ok"] = pruefungen.All(p => p.Ok),
            ["zusammenfassung"] = new JsonArray(
                Zeile("Dauerlauf S1+S3", "RSS erste Stunde", "MB", rssAnfang),
                Zeile("Dauerlauf S1+S3", "RSS letzte Stunde", "MB", rssEnde),
                Zeile("Dauerlauf S1+S3", "RSS-Wachstum", "%", (rssEnde - rssAnfang) / rssAnfang * 100),
                Zeile("Dauerlauf S1+S3", "S1 p99 letzte Stunde", "ms", P99(rampe.Alle, ende - stunde, ende)),
                Zeile("Dauerlauf S1+S3", "S3 p99 letzte Stunde", "ms", P99(s3, ende - stunde, ende))),
        };
    }

    public static double P99(IEnumerable<Vorgang> v, DateTimeOffset von, DateTimeOffset bis)
        => Statistik.Perzentil(v.Where(x => x.Start >= von && x.Start < bis).Select(x => double.IsNaN(x.LatenzMs) ? double.PositiveInfinity : x.LatenzMs).ToList(), 99);

    public static JsonNode Zeile(string szenario, string kennzahl, string einheit, double wert)
        => new JsonObject { ["szenario"] = szenario, ["kennzahl"] = kennzahl, ["einheit"] = einheit, ["wert"] = Ablage.Zahl(wert) };
}

/// <summary>
/// Aufholzeit (§0.1): S1 bei 50 % der Sättigung; nach dem Aufwärmen steht der Host <c>--stillstand</c> lang (Standard 1 h), während die
/// Kamera weiter Dateien schreibt. Gemessen: die Zeit vom Neustart, bis alle Paare sichtbar sind, und der Durchsatz beim Aufholen.
/// </summary>
internal static class Aufholzeit
{
    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var (s1, ordner) = Saettigung.Aus(k, "S1");
        var rate = Math.Max(0.5, s1 / 2);
        Console.WriteLine($"Aufholzeit: S1 {rate:F1} Paare/s, Stillstand {k.Optionen.Stillstand} (50 % aus {ordner})");
        k.Ressourcen.Abschnitt("aufholzeit");
        var lauf = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", "aufholzeit", true, null, "prozess_gestartet");
        await using var bild = new Bildeingang(lauf.Eingang, k.Beispiel.Quellpfad!, 2030, 1);
        var rampe = new Rampe(bild, k.Optionen.RampeBild);
        await rampe.StufeAsync("aufwaermen", rate, TimeSpan.FromMinutes(2), default);
        await lauf.Host.StoppeAsync();
        Console.WriteLine($"  Host gestoppt — {k.Optionen.Stillstand} Stillstand, die Quelle schreibt weiter");
        var still = await rampe.StufeAsync("stillstand", rate, k.Optionen.Stillstand, default);
        var rueckstand = rampe.Alle.Count(v => v.Sichtbar == null);
        var neustart = DateTimeOffset.UtcNow;
        await lauf.Host.StarteAsync(TimeSpan.FromMinutes(10));
        var gestartet = DateTimeOffset.UtcNow;
        var ok = await rampe.LeerlaufenAsync(TimeSpan.FromHours(3), default);
        var fertig = DateTimeOffset.UtcNow;
        var aufholS = (fertig - neustart).TotalSeconds;
        Console.WriteLine($"  Rückstand {rueckstand} Paare · Start {(gestartet - neustart).TotalSeconds:F0} s · aufgeholt nach {aufholS:F0} s");
        foreach (var v in rampe.Alle) k.Ablage.Latenz("S1-aufholzeit", 1, v);
        var pruefungen = new List<Pruefung> { new("Rückstand vollständig aufgeholt", ok, "ja", ok ? "ja" : "nein") };
        pruefungen.AddRange(await bild.PruefeAsync(lauf.Db, 2 * rampe.Alle.Count(v => v.Fehler == null)));
        pruefungen.AddRange(await GlobalePruefung.PruefeAsync(lauf.Db, "Beispieldomäne"));
        await lauf.DisposeAsync();
        return new JsonObject
        {
            ["kennzahlen"] = new JsonObject
            {
                ["rate_je_s"] = rate, ["saettigung_aus"] = ordner, ["rueckstand_paare"] = rueckstand,
                ["host_start_s"] = Ablage.Zahl((gestartet - neustart).TotalSeconds), ["aufholzeit_s"] = Ablage.Zahl(aufholS),
                ["aufholdurchsatz_paare_je_s"] = Ablage.Zahl(rueckstand / aufholS),
            },
            ["exactly_once"] = new JsonArray(pruefungen.Select(p => (JsonNode)p.Json()).ToArray()),
            ["exactly_once_ok"] = pruefungen.All(p => p.Ok),
            ["zusammenfassung"] = new JsonArray(
                Dauerlauf.Zeile("Aufholzeit S1", $"Aufholzeit nach {k.Optionen.Stillstand.TotalMinutes:0} min Stillstand", "s", aufholS),
                Dauerlauf.Zeile("Aufholzeit S1", "Aufholdurchsatz", "Paare/s", rueckstand / aufholS)),
        };
    }
}

/// <summary>
/// Störungslauf (§0.2): S1 und S5 gleichzeitig bei 50 % der Sättigung, dabei Störungen — Host der Beispieldomäne hart beenden und neu
/// starten, Redis (Versions-Index) 30 s stoppen, Host der Prüfdomäne hart beenden, beide zugleich. Jeder harte Abbruch verliert alle
/// Signale im Flug (der Broker läuft im Host; einen einzelnen Broker-Shard kann man von außen nicht neu starten). Danach dieselbe
/// Exactly-once-Prüfung wie in der Messung. Clients wiederholen mit derselben CommandId (§11.3).
/// </summary>
internal static class Stoerungslauf
{
    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var (s1, o1) = Saettigung.Aus(k, "S1");
        var (s5, o5) = Saettigung.Aus(k, "S5");
        var (rate1, rate5) = (Math.Max(0.5, s1 / 2), Math.Max(0.5, s5 / 2));
        var dauer = k.Optionen.Probe ? TimeSpan.FromMinutes(3) : TimeSpan.FromMinutes(8);
        Console.WriteLine($"Störungslauf {dauer}: S1 {rate1:F1}/s, S5 {rate5:F1}/s (50 % aus {o1} / {o5})");
        k.Ressourcen.Abschnitt("stoerung");
        var beispiel = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", "stoerung-beispiel", true, null, "prozess_gestartet");
        var pruef = await Anwendungslauf.StarteAsync(k, k.Pruef, "Host.Pruefdomaene", "host-pruef", "stoerung-pruefdomaene", true, null, "posten_reserviert");
        await using var bild = new Bildeingang(beispiel.Eingang, k.Beispiel.Quellpfad!, 2030, 1);
        var pd = new Pruefdomaene(pruef.Eingang, pruef.Log, pruef.Uhrversatz) { Wiederholen = true };
        await pd.EinrichtenAsync();
        var rampe1 = new Rampe(bild, k.Optionen.RampeBild);
        var rampe5 = new Rampe(pd, k.Optionen.RampePruef);
        var last1 = rampe1.StufeAsync("stoerung", rate1, dauer, default);
        var last5 = rampe5.StufeAsync("stoerung", rate5, dauer, default);

        var protokoll = new JsonArray();
        var sw = Stopwatch.StartNew();
        async Task Bei(double anteil, string was, Func<Task> tu)
        {
            var bis = TimeSpan.FromTicks((long)(dauer.Ticks * anteil)) - sw.Elapsed;
            if (bis > TimeSpan.Zero) await Task.Delay(bis);
            Console.WriteLine($"  {sw.Elapsed:mm\\:ss} Störung: {was}");
            protokoll.Add(new JsonObject { ["zeit_s"] = Math.Round(sw.Elapsed.TotalSeconds), ["stoerung"] = was });
            await tu();
        }
        await Bei(0.12, "Host Beispieldomäne hart beendet (SIGKILL), Neustart nach 5 s", async () =>
        {
            beispiel.Host.Toete(); await Task.Delay(5000); await beispiel.Host.StarteAsync();
        });
        await Bei(0.30, "Redis (Versions-Index) 30 s gestoppt", async () =>
        {
            Werkzeuge.Laufe("docker", "stop cqrs-vermessung-redis"); await Task.Delay(30_000); Werkzeuge.Laufe("docker", "start cqrs-vermessung-redis");
        });
        await Bei(0.50, "Host Prüfdomäne hart beendet (SIGKILL), Neustart nach 5 s", async () =>
        {
            pruef.Host.Toete(); await Task.Delay(5000); await pruef.Host.StarteAsync();
        });
        await Bei(0.70, "beide Hosts zugleich hart beendet, Neustart nach 10 s", async () =>
        {
            beispiel.Host.Toete(); pruef.Host.Toete(); await Task.Delay(10_000);
            await Task.WhenAll(beispiel.Host.StarteAsync(), pruef.Host.StarteAsync());
        });
        var st1 = await last1;
        var st5 = await last5;
        var ok1 = await rampe1.LeerlaufenAsync(TimeSpan.FromMinutes(10), default);
        var ok5 = await rampe5.LeerlaufenAsync(TimeSpan.FromMinutes(10), default);
        await WarteAufReaktion(pruef);
        foreach (var v in rampe1.Alle) k.Ablage.Latenz("S1-stoerung", 1, v);
        foreach (var v in rampe5.Alle) k.Ablage.Latenz("S5-stoerung", 1, v);

        var pruefungen = new List<Pruefung>
        {
            new("S1: alle Paare nach den Störungen sichtbar", ok1, "ja", ok1 ? "ja" : $"{rampe1.Alle.Count(v => v.Sichtbar == null)} offen"),
            new("S5: alle Vorgänge nach den Störungen beendet", ok5, "ja", ok5 ? "ja" : $"{rampe5.Alle.Count(v => v.Sichtbar == null)} offen"),
            Pruefung.Gleich("S5: kein Eingang endgültig gescheitert", 0, rampe5.Alle.Count(v => v.Fehler != null)),
        };
        pruefungen.AddRange(await bild.PruefeAsync(beispiel.Db, 2 * rampe1.Alle.Count(v => v.Fehler == null)));
        pruefungen.AddRange(await pd.PruefeAsync(pruef.Db));
        pruefungen.AddRange(await GlobalePruefung.PruefeAsync(beispiel.Db, "Beispieldomäne"));
        pruefungen.AddRange(await GlobalePruefung.PruefeAsync(pruef.Db, "Prüfdomäne"));
        await beispiel.DisposeAsync();
        await pruef.DisposeAsync();
        foreach (var p in pruefungen.Where(p => !p.Ok)) Console.WriteLine($"  ✗ {p.Name} — soll {p.Soll}, ist {p.Ist}");
        return new JsonObject
        {
            ["stoerungen"] = protokoll,
            ["kennzahlen"] = new JsonObject
            {
                ["rate_s1_je_s"] = rate1, ["rate_s5_je_s"] = rate5, ["vorgaenge_s1"] = st1.Anzahl, ["vorgaenge_s5"] = st5.Anzahl,
                ["s1_p99_ms"] = Ablage.Zahl(Statistik.Perzentil(rampe1.Alle.Select(v => v.LatenzMs).Where(x => !double.IsNaN(x)).ToList(), 99)),
                ["s5_p99_ms"] = Ablage.Zahl(Statistik.Perzentil(rampe5.Alle.Select(v => v.LatenzMs).Where(x => !double.IsNaN(x)).ToList(), 99)),
            },
            ["exactly_once"] = new JsonArray(pruefungen.Select(p => (JsonNode)p.Json()).ToArray()),
            ["exactly_once_ok"] = pruefungen.All(p => p.Ok),
        };
    }

    private static async Task WarteAufReaktion(Anwendungslauf lauf)
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
