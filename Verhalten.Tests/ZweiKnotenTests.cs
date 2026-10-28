using Abstractions;
using Domain.ImagePair;
using Pruefdomaene.Posten;

namespace Verhalten.Tests;

/// <summary>
/// Zwei echte Knoten (zwei Host-Prozesse, ein Consul-Cluster, eine Datenbank) und je Knoten ein Client: eine Ablehnung bzw. ein gescheiterter
/// Command muss bei GENAU der Sitzung ankommen, die ihn geschickt hat — gleich, auf welchem Knoten das Aggregat lebt. Zwei Prozesse, nicht
/// zwei Knoten in einem Prozess: dort teilten sich die Knoten den Zähler der Sitzungs-Kennungen, und die Verwechslung bliebe unsichtbar.
///
/// Heute rot: die Sitzungs-Kennung ist ein Zähler je Prozess (<c>session-0001</c> …), die Abonnenten-Tabelle des Brokers überschreibt bei
/// gleicher Kennung — die ersten Sitzungen beider Knoten heißen gleich, und die Antwort für die eine landet bei der anderen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public sealed class ZweiKnotenTests
{
    [Fact(Skip = "G-13 offen (docs/13)")]
    public async Task Eine_Ablehnung_landet_bei_zwei_Knoten_an_der_richtigen_Sitzung()
    {
        var u = await Testumgebung.NeuAsync(Anwendung.Beispiel, "verhalten-zwei-knoten", consul: true);
        await using var a = await Hostlauf.StarteAsync(u, "knoten-a");
        await using var b = await Hostlauf.StarteAsync(u, "knoten-b");
        var log = new Log(u);
        var bilder = new Bildpaare(u);

        // Mehrere Paare, damit ihre Aggregate auf beide Knoten verteilt liegen.
        var paare = new List<Guid>();
        for (var i = 0; i < 6; i++) paare.Add((await bilder.LegeAbAsync()).Id);
        foreach (var p in paare)
            await Warte.BisAsync(async () => await log.ZahlAsync<ImagePairKomplett>(p) == 1, TimeSpan.FromSeconds(120), $"Paar {p} wird nicht komplett", a);

        await using var sitzungA = await RoheLeitung.VerbindeAsync(a, "Arbeitsplatz", Testumgebung.TokenArbeitsplatz);
        await using var sitzungB = await RoheLeitung.VerbindeAsync(b, "Arbeitsplatz", Testumgebung.TokenArbeitsplatz);

        // Jede Sitzung behauptet für jedes Paar eine veraltete Version → Versionskonflikt → gezielt an den Absender.
        var vonA = new List<string>();
        var vonB = new List<string>();
        foreach (var p in paare)
        {
            var ka = Guid.NewGuid().ToString();
            var kb = Guid.NewGuid().ToString();
            vonA.Add(ka);
            vonB.Add(kb);
            await sitzungA.SendeAsync(new LabelBildPaar(new(p), Klassifikation.Anomalie), "ImagePair", 1, ka);
            await sitzungB.SendeAsync(new LabelBildPaar(new(p), Klassifikation.Anomalie), "ImagePair", 1, kb);
        }

        foreach (var k in vonA)
            await sitzungA.WarteAufEventAsync(e => e.CorrelationId == k, TimeSpan.FromSeconds(30), $"Sitzung A ({sitzungA.SitzungsId}) bekam ihre Antwort {k} nicht");
        foreach (var k in vonB)
            await sitzungB.WarteAufEventAsync(e => e.CorrelationId == k, TimeSpan.FromSeconds(30), $"Sitzung B ({sitzungB.SitzungsId}) bekam ihre Antwort {k} nicht");
        await Warte.NachlaufAsync();
        sitzungA.Events.Should().NotContain(e => vonB.Contains(e.CorrelationId), "eine Antwort an B darf nie bei A landen");
        sitzungB.Events.Should().NotContain(e => vonA.Contains(e.CorrelationId), "eine Antwort an A darf nie bei B landen");
        sitzungA.Events.Where(e => vonA.Contains(e.CorrelationId)).Select(e => RoheLeitung.Payload(e)).Should().AllBeOfType<CommandFailed>();
        foreach (var p in paare) (await log.ZahlAsync<BildPaarGelabelt>(p)).Should().Be(0, "kein Command mit veralteter Version hat gewirkt");
    }

    /// <summary>
    /// Hartes Ende eines Knotens (Plan Pub/Sub-Neustrukturierung Phase 6, §7 Risiko 3): zwei Knoten der Prüfdomäne, Knoten B wird mit SIGKILL beendet.
    /// Danach weckt jeder Commit auf Knoten A die Funktion <c>Nachschub</c> in jeder Partition — auch in denen, die auf B aktiv waren und neu
    /// platziert werden — ohne auf den Takt (30 s) zu warten: sobald der Cluster B ausgetragen hat, kommt eine Runde aus je einem Posten je Partition
    /// dreimal hintereinander in unter 5 s durch. Jeder Eingang wirkt genau einmal, auch die aus der Übergangszeit.
    /// </summary>
    [Fact]
    public async Task Nach_dem_harten_Ende_eines_Knotens_wecken_Commits_die_neuen_Aktivierungen_ohne_den_Takt()
    {
        const int partitionen = 8;   // Konsum-Standard der Umgebung
        var u = await Testumgebung.NeuAsync(Anwendung.Pruefdomaene, "verhalten-wecker-knoten", consul: true);
        await using var a = await Hostlauf.StarteAsync(u, "knoten-a");
        await using var b = await Hostlauf.StarteAsync(u, "knoten-b");
        using var eingang = new JsonEingang(a);
        var log = new Log(u);
        var alle = new List<Guid>();

        // Je Partition ein Posten, dessen Reservierung eine Fehlmenge hinterlässt (die Funktion Nachschub bucht sie nach).
        async Task<IReadOnlyList<Guid>> RundeAsync()
        {
            var runde = new List<Guid>();
            for (var p = 0; p < partitionen; p++)
            {
                Guid id;
                do id = Guid.NewGuid(); while (Partitionierung.Von(id, partitionen) != p);
                var eroeffnet = await eingang.CommandAsync("EroeffnePosten", new() { ["AggregateId"] = id.ToString(), ["Bestand"] = 100, ["Mindestbestand"] = 98 });
                if (!eroeffnet.Ok) continue;   // Übergangszeit: das Ziel lag auf B
                var reserviert = await eingang.CommandAsync("ReservierePosten", new()
                    { ["AggregateId"] = id.ToString(), ["Menge"] = 5, ["Beleg"] = new System.Text.Json.Nodes.JsonObject { ["Art"] = "Keiner", ["Nummer"] = Guid.Empty.ToString() } });
                if (reserviert.Ok) runde.Add(id);
            }
            alle.AddRange(runde);
            return runde;
        }
        async Task<bool> NachgebuchtAsync(IEnumerable<Guid> posten)
        {
            foreach (var p in posten) if (await log.ZahlAsync<PostenZugebucht>(p) < 1) return false;
            return true;
        }

        // Aufwärmen: Aktivierungen auf beiden Knoten.
        var warm = await RundeAsync();
        await Warte.BisAsync(() => NachgebuchtAsync(warm), TimeSpan.FromSeconds(90), "Aufwärmen: der Nachschub kommt nicht", a);

        b.Toete();

        var schnell = 0;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        while (schnell < 3)
        {
            if (uhr.Elapsed > TimeSpan.FromSeconds(150))
                throw new TimeoutException($"Nach dem harten Ende von B kam keine Folge von drei schnellen Runden zustande.\n{a.LogEnde()}");
            var runde = await RundeAsync();
            var start = System.Diagnostics.Stopwatch.StartNew();
            var ok = runde.Count == partitionen;
            while (ok && !await NachgebuchtAsync(runde))
            {
                if (start.Elapsed > TimeSpan.FromSeconds(5)) { ok = false; break; }
                await Task.Delay(100);
            }
            schnell = ok ? schnell + 1 : 0;
        }

        // Genau einmal, auch für die Eingänge aus der Übergangszeit (die holt spätestens der Takt).
        await Warte.BisAsync(() => NachgebuchtAsync(alle), TimeSpan.FromSeconds(90), "ein Eingang aus der Übergangszeit wirkt nie", a);
        await Warte.NachlaufAsync();
        foreach (var p in alle) (await log.ZahlAsync<PostenZugebucht>(p)).Should().Be(1, $"Posten {p}: der Nachschub wirkt genau einmal");
    }
}
