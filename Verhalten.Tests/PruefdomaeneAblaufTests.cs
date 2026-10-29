using System.Text.Json.Nodes;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Verhalten.Tests;

/// <summary>Ein echter Host der Prüfdomäne (ohne Tor: der JSON-Eingang braucht kein Token).</summary>
public sealed class PruefdomaeneHost : IAsyncLifetime
{
    public Testumgebung Umgebung { get; private set; } = null!;
    public Hostlauf Host { get; private set; } = null!;
    public JsonEingang Eingang { get; private set; } = null!;
    public Log Log { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Umgebung = await Testumgebung.NeuAsync(Anwendung.Pruefdomaene, "verhalten-pruefdomaene", taktSekunden: 2);
        Host = await Hostlauf.StarteAsync(Umgebung);
        Eingang = new JsonEingang(Host);
        Log = new Log(Umgebung);
    }

    public async Task<Guid> PostenAsync(int bestand, int mindestbestand = 0)
    {
        var id = Guid.NewGuid();
        await Eingang.MussAsync("EroeffnePosten", new() { ["AggregateId"] = id.ToString(), ["Bestand"] = bestand, ["Mindestbestand"] = mindestbestand });
        return id;
    }

    public async Task<Guid> KontingentAsync(int plaetze)
    {
        var id = Guid.NewGuid();
        await Eingang.MussAsync("RichteKontingentEin", new() { ["AggregateId"] = id.ToString(), ["Plaetze"] = plaetze });
        return id;
    }

    public async Task DisposeAsync()
    {
        Eingang.Dispose();
        await Host.DisposeAsync();
    }
}

/// <summary>
/// Exactly-once je Ablauf der Prüfdomäne als FACHLICHES Soll (docs/12 §12.6): Auftrag (Diamant mit Join), Auftrag mit Kompensation,
/// Verteilung (Fan-out, Count-Join), Nachschub-Reaktion. Je Szenario eigene Aggregate, damit die Zahl der Fakten je Stream das Soll ist —
/// gezählt im Log, nie über Prozess-Events. Auch der doppelt geschickte Start (dieselbe CommandId) wirkt genau einmal.
/// </summary>
[Trait("Kategorie", "Garantie")]
public sealed class PruefdomaeneAblaufTests(PruefdomaeneHost h) : IClassFixture<PruefdomaeneHost>
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    private static JsonObject Auftrag(Guid id, Guid posten, Guid kontingent, int menge) => new()
    {
        ["AggregateId"] = id.ToString(), ["PostenId"] = posten.ToString(), ["KontingentId"] = kontingent.ToString(), ["Menge"] = menge,
    };

    [Fact]
    public async Task Ein_Auftrag_reserviert_und_belegt_je_genau_einmal_und_wird_einmal_bestaetigt()
    {
        var posten = await h.PostenAsync(100);
        var kontingent = await h.KontingentAsync(10);
        var auftrag = Guid.NewGuid();
        var start = Guid.NewGuid();
        await h.Eingang.MussAsync("StarteAuftrag", Auftrag(auftrag, posten, kontingent, 3), commandId: start);
        await h.Eingang.MussAsync("StarteAuftrag", Auftrag(auftrag, posten, kontingent, 3), commandId: start);   // wiederholt
        await h.Eingang.CommandAsync("StarteAuftrag", Auftrag(auftrag, posten, kontingent, 3));   // ein zweiter Start: fachlich abgelehnt

        await Warte.BisAsync(async () => await h.Log.ZahlAsync<AuftragBestaetigt>(auftrag) == 1, Frist, "der Auftrag wird nicht bestätigt", h.Host);
        await Warte.NachlaufAsync();

        (await h.Log.ZahlAsync<AuftragGestartet>(auftrag)).Should().Be(1);
        (await h.Log.ZahlAsync<PostenReserviert>(posten)).Should().Be(1);
        (await h.Log.ZahlAsync<KontingentBelegt>(kontingent)).Should().Be(1);
        (await h.Log.ZahlAsync<AuftragBestaetigt>(auftrag)).Should().Be(1);
        (await h.Log.ZahlAsync<PostenFreigegeben>(posten)).Should().Be(0, "nichts ist gescheitert, nichts wird zurückgenommen");
    }

    [Fact]
    public async Task Scheitert_ein_Zweig_wird_der_gelungene_genau_einmal_zurueckgenommen_und_nie_bestaetigt()
    {
        var posten = await h.PostenAsync(100);
        var voll = await h.KontingentAsync(0);
        var auftrag = Guid.NewGuid();
        await h.Eingang.MussAsync("StarteAuftrag", Auftrag(auftrag, posten, voll, 3));

        await Warte.BisAsync(async () => await h.Log.ZahlAsync<PostenFreigegeben>(posten) == 1, Frist, "die Reservierung wird nicht zurückgenommen", h.Host);
        await Warte.NachlaufAsync();

        (await h.Log.ZahlAsync<PostenReserviert>(posten)).Should().Be(1);
        var frei = await h.Log.DatenAsync<PostenFreigegeben>(posten);
        frei.Should().ContainSingle();
        ((int)JsonNode.Parse(frei[0])!["Menge"]!).Should().Be(3, "genau die reservierte Menge geht zurück");
        (await h.Log.ZahlAsync<KontingentBelegt>(voll)).Should().Be(0);
        (await h.Log.ZahlAsync<AuftragBestaetigt>(auftrag)).Should().Be(0);
    }

    [Fact]
    public async Task Eine_Verteilung_bucht_jedes_Ziel_genau_einmal_und_schliesst_einmal_ab()
    {
        var quelle = await h.PostenAsync(100);
        var ziele = new List<Guid>();
        for (var i = 0; i < 4; i++) ziele.Add(await h.PostenAsync(0));
        var verteilung = Guid.NewGuid();
        var start = Guid.NewGuid();
        var daten = new JsonObject
        {
            ["AggregateId"] = verteilung.ToString(), ["QuelleId"] = quelle.ToString(),
            ["Ziele"] = new JsonArray(ziele.Select(z => (JsonNode)z.ToString()).ToArray()), ["MengeJeZiel"] = 5,
        };
        await h.Eingang.MussAsync("StarteVerteilung", daten, commandId: start);
        await h.Eingang.MussAsync("StarteVerteilung", (JsonObject)daten.DeepClone(), commandId: start);

        await Warte.BisAsync(async () => await h.Log.ZahlAsync<VerteilungAbgeschlossen>(verteilung) == 1, Frist, "die Verteilung schließt nicht ab", h.Host);
        await Warte.NachlaufAsync();

        foreach (var z in ziele)
        {
            var zu = await h.Log.DatenAsync<PostenZugebucht>(z);
            zu.Should().ContainSingle("jedes Ziel genau einmal");
            ((int)JsonNode.Parse(zu[0])!["Menge"]!).Should().Be(5);
        }
        (await h.Log.ZahlAsync<PostenReserviert>(quelle)).Should().Be(1);
        var ab = await h.Log.DatenAsync<VerteilungAbgeschlossen>(verteilung);
        ((int)JsonNode.Parse(ab.Single())!["Zugebucht"]!).Should().Be(20);
    }

    [Fact]
    public async Task Eine_Reservierung_unter_dem_Mindestbestand_bestellt_genau_einmal_nach()
    {
        var posten = await h.PostenAsync(100, mindestbestand: 98);
        var kontingent = await h.KontingentAsync(100);
        await h.Eingang.MussAsync("StarteAuftrag", Auftrag(Guid.NewGuid(), posten, kontingent, 5));   // 95 < 98 → Fehlmenge 3

        await Warte.BisAsync(async () => await h.Log.ZahlAsync<PostenZugebucht>(posten) >= 1, Frist, "kein Nachschub", h.Host);
        await Warte.NachlaufAsync(6);    // über drei Takt-Perioden (hier 2 s): auch das Netz des Takts darf nicht ein zweites Mal bestellen

        var zu = await h.Log.DatenAsync<PostenZugebucht>(posten);
        zu.Should().ContainSingle("je Reservierung unter dem Mindestbestand genau ein Nachschub");
        ((int)JsonNode.Parse(zu[0])!["Menge"]!).Should().Be(3);
    }
}
