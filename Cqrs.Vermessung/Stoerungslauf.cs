using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Störungslauf (docs/11 §11.14), etwa vier Minuten: Bildeingang (zwei Paare je Sekunde) und Verteilt (8 Sender) gleichzeitig, dabei Störungen — Host der
/// Beispieldomäne hart beenden und neu starten, Redis (Versions-Index) 10 s stoppen, Host der Prüfdomäne hart beenden. Jeder harte Abbruch
/// verliert alle Signale im Flug (der Broker läuft im Host). Clients wiederholen mit derselben CommandId (§11.3). Danach dieselbe
/// Exactly-once-Prüfung wie in der Messung.
/// </summary>
internal static class Stoerungslauf
{
    public static async Task<JsonObject> LaufeAsync(Kontext k)
    {
        var dauer = TimeSpan.FromSeconds(90);
        Console.WriteLine($"Störungslauf {dauer.TotalSeconds:0} s: Bildeingang 2 Paare/s, Verteilt 8 Sender");
        k.Ressourcen.Abschnitt("stoerung");
        var beispiel = await Anwendungslauf.StarteAsync(k, k.Beispiel, "Host.Grpc", "host", "stoerung-beispiel", true, "prozess_gestartet");
        var pruef = await Anwendungslauf.StarteAsync(k, k.Pruef, "Host.Pruefdomaene", "host-pruef", "stoerung-pruefdomaene", true, "posten_reserviert");
        await using var bild = new Bildeingang(beispiel.Eingang, k.Beispiel.Quellpfad!, 2031, 1);
        var pd = new Pruefdomaene(pruef.Eingang, pruef.Log, pruef.Uhrversatz) { Wiederholen = true };
        await pd.EinrichtenAsync();

        // Bildeingang: eine Kamera mit zwei Paaren je Sekunde; Verteilt: 8 Sender so schnell wie angenommen (mit Wiederholung über Host-Abbrüche hinweg).
        var bilder = new List<Vorgang>();
        var kamera = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            for (var i = 0; sw.Elapsed < dauer; i++)
            {
                var warte = TimeSpan.FromSeconds(i / 2.0) - sw.Elapsed;
                if (warte > TimeSpan.Zero) await Task.Delay(warte);
                var v = new Vorgang { Schluessel = i.ToString(), Phase = "stoerung" };
                bilder.Add(v);
                await bild.StarteAsync(v, default);
            }
        });
        // Abbau nach Fortschritt (höchstens 10 min, Hänger = 60 s ohne fertigen Vorgang): geprüft wird Lebendigkeit, nicht Tempo — in Phase 1
        //   brauchte der langsamste Vorgang schon 201 s, eine feste Frist von 3 min machte einen schnelleren Eingang (mehr Rückstand) rot.
        var lastVerteilt = Volllast.LaufeAsync(pd, "stoerung", 8, dauer, TimeSpan.FromMinutes(10), ohneFortschritt: TimeSpan.FromSeconds(60));

        var protokoll = new JsonArray();
        var uhr = Stopwatch.StartNew();
        async Task Bei(double anteil, string was, Func<Task> tu)
        {
            var bis = TimeSpan.FromTicks((long)(dauer.Ticks * anteil)) - uhr.Elapsed;
            if (bis > TimeSpan.Zero) await Task.Delay(bis);
            Console.WriteLine($"  {uhr.Elapsed:mm\\:ss} Störung: {was}");
            protokoll.Add(new JsonObject { ["zeit_s"] = Math.Round(uhr.Elapsed.TotalSeconds), ["stoerung"] = was });
            await tu();
        }
        await Bei(0.2, "Host Beispieldomäne hart beendet (SIGKILL), Neustart nach 3 s", async () =>
        {
            beispiel.Host.Toete(); await Task.Delay(3000); await beispiel.Host.StarteAsync();
        });
        await Bei(0.45, "Redis (Versions-Index) 10 s gestoppt", async () =>
        {
            Werkzeuge.Laufe("docker", "stop cqrs-vermessung-redis"); await Task.Delay(10_000); Werkzeuge.Laufe("docker", "start cqrs-vermessung-redis");
        });
        await Bei(0.7, "Host Prüfdomäne hart beendet (SIGKILL), Neustart nach 3 s", async () =>
        {
            pruef.Host.Toete(); await Task.Delay(3000); await pruef.Host.StarteAsync();
        });
        await kamera;
        var (verteilt, verteiltVorgaenge) = await lastVerteilt;
        var bilderOk = await Volllast.LeerlaufenAsync(bilder, TimeSpan.FromMinutes(5));
        await Leistung.WarteAufReaktionAsync(pruef);
        foreach (var v in bilder) k.Ablage.Latenz("Bildeingang-stoerung", 1, v);
        foreach (var v in verteiltVorgaenge) k.Ablage.Latenz("Verteilt-stoerung", 1, v);

        var pruefungen = new List<Pruefung>
        {
            new("Bildeingang: alle Paare nach den Störungen sichtbar", bilderOk, "ja", bilderOk ? "ja" : $"{bilder.Count(v => v.Sichtbar == null)} offen"),
            new("Verteilt: alle Vorgänge nach den Störungen beendet", verteilt.Abgebaut, "ja", verteilt.Abgebaut ? "ja" : $"{verteiltVorgaenge.Count(v => v.Sichtbar == null)} offen"),
            Pruefung.Gleich("Verteilt: kein Eingang endgültig gescheitert", 0, verteilt.Fehler),
        };
        pruefungen.AddRange(await bild.PruefeAsync(beispiel.Db, 2 * bild.Paare.Count));
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
                ["bildpaare"] = bilder.Count, ["vorgaenge"] = verteilt.Angenommen, ["abbau_s"] = Ablage.Zahl(verteilt.AbbauS),
                ["bildeingang_p99_ms"] = Ablage.Zahl(Statistik.Perzentil(bilder.Select(v => v.LatenzMs).Where(x => !double.IsNaN(x)).ToList(), 99)),
                ["verteilt_p99_ms"] = Ablage.Zahl(verteilt.P99),
            },
            ["exactly_once"] = new JsonArray(pruefungen.Select(p => (JsonNode)p.Json()).ToArray()),
            ["exactly_once_ok"] = pruefungen.All(p => p.Ok),
        };
    }
}
