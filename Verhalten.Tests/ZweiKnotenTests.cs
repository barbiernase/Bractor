using Abstractions;
using Domain.ImagePair;

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
}
