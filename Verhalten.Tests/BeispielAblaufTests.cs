using System.Text.Json.Nodes;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;

namespace Verhalten.Tests;

/// <summary>Ein eigener Host der Beispieldomäne für die Abläufe (eigene Datenbank: die Range eines Datensatzes nimmt alle Paare im Log).</summary>
public sealed class BeispielAblaufHost : IAsyncLifetime
{
    public Testumgebung Umgebung { get; private set; } = null!;
    public Hostlauf Host { get; private set; } = null!;
    public JsonEingang Eingang { get; private set; } = null!;
    public Log Log { get; private set; } = null!;
    public Bildpaare Bilder { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Umgebung = await Testumgebung.NeuAsync(Anwendung.Beispiel, "verhalten-ablauf");
        Host = await Hostlauf.StarteAsync(Umgebung);
        Eingang = new JsonEingang(Host);
        Log = new Log(Umgebung);
        Bilder = new Bildpaare(Umgebung);
    }

    public async Task DisposeAsync()
    {
        Eingang.Dispose();
        await Host.DisposeAsync();
    }
}

/// <summary>
/// Exactly-once je Ablauf der Beispieldomäne als FACHLICHES Soll (docs/12 §12.6): was am Ende im Log und in den Antworten steht — nicht,
/// über welchen Mechanismus (Dirigent, Fluss, Prozess-Events, Funktions-Streams) es entstand. Angestoßen nur von außen: Quell-Verzeichnis und
/// JSON-Eingang mit dem Token des Akteurs.
/// </summary>
[Trait("Kategorie", "Garantie")]
public sealed class BeispielAblaufTests(BeispielAblaufHost h) : IClassFixture<BeispielAblaufHost>
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Bildeingang_jedes_Paar_genau_einmal_angelegt_zwei_Bilder_einmal_komplett_auch_wenn_Dateien_doppelt_kommen()
    {
        var paare = new List<Bildpaare.Paar>();
        for (var i = 0; i < 3; i++) paare.Add(await h.Bilder.LegeAbAsync());
        foreach (var p in paare)
            await Warte.BisAsync(async () => await h.Log.ZahlAsync<ImagePairKomplett>(p.Id) == 1, Frist, $"Paar {p.Schluessel} wird nicht komplett", h.Host);

        // Dieselben Dateien noch einmal (gleicher Name, gleiche Größe = dieselbe Kennung): nichts darf ein zweites Mal geschehen.
        foreach (var p in paare)
            foreach (var d in p.Dateien) await h.Bilder.LegeDateiAbAsync(d);
        await Warte.NachlaufAsync(8);   // Takt des Treibers 1 s, Stabilität 1 s — danach wäre eine zweite Meldung längst im Log

        foreach (var p in paare)
        {
            foreach (var d in p.Dateien)
                (await h.Log.ZahlMitFeldAsync<DateiErkannt>("Name", d)).Should().Be(1, $"{d} ist genau einmal als Quell-Nachricht im Log");
            (await h.Log.ZahlAsync<ImagePairErstellt>(p.Id)).Should().Be(1);
            var bilder = await h.Log.DatenAsync<BildVerfuegbar>(p.Id);
            bilder.Should().HaveCount(2);
            bilder.Select(x => JsonNode.Parse(x)!["Version"]!.ToJsonString()).Distinct().Should().HaveCount(2, "je Version (DC0, DC2) genau ein Bild");
            (await h.Log.ZahlAsync<ImagePairKomplett>(p.Id)).Should().Be(1);

            var antwort = await h.Eingang.QueryAsync("GetImagePair", new() { ["PairId"] = p.Id.ToString() }, Testumgebung.TokenInspekteur);
            antwort["typ"]!.ToString().Should().Be("ImagePairAntwort");
            ((bool)antwort["daten"]!["IstKomplett"]!).Should().BeTrue();
            antwort["daten"]!["Dc0Pfad"]!.ToString().Should().NotBeEmpty();
            antwort["daten"]!["Dc2Pfad"]!.ToString().Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Datensatz_Range_einmal_aufgenommen_und_genau_einmal_als_Version_1_eingefroren()
    {
        var paare = new List<Guid>();
        for (var i = 0; i < 2; i++) paare.Add((await h.Bilder.LegeAbAsync()).Id);
        foreach (var p in paare)
            await Warte.BisAsync(async () => await h.Log.ZahlAsync<ImagePairKomplett>(p) == 1, Frist, $"Paar {p} wird nicht komplett", h.Host);
        var imLog = await h.Log.ZahlAsync<ImagePairErstellt>();

        var ds = Guid.NewGuid();
        await h.Eingang.MussAsync("ErstelleDatensatz", new() { ["AggregateId"] = ds.ToString(), ["Name"] = "Verhalten" }, Testumgebung.TokenKiOperator);
        await h.Eingang.MussAsync("FuegeRangeHinzu", new() { ["AggregateId"] = ds.ToString(), ["Kriterien"] = new JsonObject() }, Testumgebung.TokenKiOperator);
        await Warte.BisAsync(async () => (int?)(await HoleAsync(ds))["AnzahlMitglieder"] == imLog, Frist, "die Range wurde nicht aufgenommen", h.Host);

        var einfrieren = Guid.NewGuid();   // derselbe Command zweimal (Client wiederholt mit derselben Id)
        await h.Eingang.MussAsync("FriereEin", new() { ["AggregateId"] = ds.ToString() }, Testumgebung.TokenKiOperator, einfrieren);
        await h.Eingang.MussAsync("FriereEin", new() { ["AggregateId"] = ds.ToString() }, Testumgebung.TokenKiOperator, einfrieren);
        await Warte.BisAsync(async () => await HoleAsync(ds) is var d && (string?)d["Status"] == "Eingefroren" && (int?)d["EingefroreneVersion"] == 1,
            Frist, "der Datensatz wird nicht eingefroren", h.Host);
        await Warte.NachlaufAsync();

        (await h.Log.ZahlAsync<PaareAufgenommen>(ds)).Should().Be(1, "eine Range wird genau einmal aufgenommen");
        var eingefroren = await h.Log.DatenAsync<DatensatzEingefroren>(ds);
        eingefroren.Should().ContainSingle("genau eine eingefrorene Version");
        JsonNode.Parse(eingefroren[0])!["Mitglieder"]!.AsArray().Should().HaveCount((int)imLog);
        ((int?)(await HoleAsync(ds))["AnzahlMitglieder"]).Should().Be((int)imLog);
    }

    [Fact]
    public async Task Sammelvorgang_N_Teile_ergeben_N_Verbuchungen_und_genau_einen_Abschluss()
    {
        const int n = 4;
        var sv = Guid.NewGuid();
        var teile = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();
        await h.Eingang.MussAsync("StarteSammelvorgang", new() { ["AggregateId"] = sv.ToString(), ["Anzahl"] = n }, Testumgebung.TokenDisponent);
        foreach (var t in teile)
            await h.Eingang.MussAsync("StarteTeilauftrag", new() { ["AggregateId"] = t.ToString(), ["SammelvorgangId"] = sv.ToString() }, Testumgebung.TokenDisponent);
        var wiederholt = Guid.NewGuid();
        foreach (var t in teile)
            await h.Eingang.MussAsync("SchließeTeilauftragAb", new() { ["AggregateId"] = t.ToString() }, Testumgebung.TokenDisponent,
                t == teile[1] ? wiederholt : null);
        await h.Eingang.MussAsync("SchließeTeilauftragAb", new() { ["AggregateId"] = teile[1].ToString() }, Testumgebung.TokenDisponent, wiederholt);
        await h.Eingang.CommandAsync("SchließeTeilauftragAb", new() { ["AggregateId"] = teile[0].ToString() }, Testumgebung.TokenDisponent);   // fachlich schon fertig

        await Warte.BisAsync(async () => await h.Log.ZahlAsync<SammelvorgangAbgeschlossen>(sv) == 1, Frist, "der Sammelvorgang schließt nicht ab", h.Host);
        await Warte.NachlaufAsync();

        foreach (var t in teile) (await h.Log.ZahlAsync<TeilauftragAbgeschlossen>(t)).Should().Be(1);
        (await h.Log.ZahlAsync<TeilVerbucht>(sv)).Should().Be(n, "jedes fertige Teil wird genau einmal verbucht");
        (await h.Log.ZahlAsync<SammelvorgangAbgeschlossen>(sv)).Should().Be(1);
    }

    private async Task<JsonNode> HoleAsync(Guid datensatz)
    {
        var a = await h.Eingang.QueryAsync("HoleDatensatz", new() { ["DatensatzId"] = datensatz.ToString() }, Testumgebung.TokenKiOperator);
        return a["typ"]?.ToString() == "DatensatzAntwort" ? a["daten"]! : new JsonObject();
    }
}
