using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Messung;
using Pruefdomaene.Projections.Messung;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Store-Klassen 2 und 3 im echten Cluster (Plan Pub/Sub-Neustrukturierung §2.4, Phase 8): die Messreihe schreibt in ein Datei-Ziel
/// (Klasse 2, Fortschritt beim Framework), der Messzähler in eine Datei-Senke (Klasse 3, ausdrücklich erlaubt) — derselbe generierte Batch-Leser,
/// derselbe Query-Dienst wie für den Marten-Store. Read-your-writes über den Fortschritt beim Framework: die Query mit „mindestens S auf v“ sieht
/// den eigenen Stand, und die Antwort trägt den Stand der gezeigten Id.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class StoreKlassenE2ETests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Projektionen_in_Stores_der_Klasse_2_und_3_wirken_ueber_denselben_Leser_und_Query_Dienst()
    {
        using var host = Cluster("pruef-storeklassen", schema: "pruef_storeklassen");
        await host.StartAsync();
        try
        {
            var stelle = Id<Messstelle>.Neu();
            var werte = Enumerable.Range(1, 20).Select(i => i * 1.5).ToList();
            foreach (var w in werte)
                (await FrageAsync(host, new ErfasseMesswert(stelle, w), Guid.NewGuid()))!.Success.Should().BeTrue();

            var queries = host.Services.GetRequiredService<IQueryDienst>();
            var antwort = await queries.ExecuteAsync(new HoleMessreihe(stelle), new[] { new StreamStand(stelle.Wert, werte.Count) });
            antwort.NochNicht.Should().BeFalse("die Projektion holt den Stand in der Frist ein (Fortschritt beim Framework)");
            var reihe = (Messreihe)antwort.Data;
            reihe.Werte.Should().Equal(werte, "je Event genau ein Messpunkt, in Log-Reihenfolge");
            reihe.Letzter.Should().Be(werte[^1]);
            antwort.Deps.Should().ContainSingle(d => d.Id == stelle.Wert && d.Version == werte.Count, "die Antwort trägt den Stand der gezeigten Id");
            antwort.Projektionen.Should().Equal("MessreiheProjektion");

            await Warte(async () => ((Messzaehler)(await queries.ExecuteAsync(new HoleMesszaehler(stelle))).Data).Anzahl >= werte.Count,
                Frist, "die Senke zählt nicht nach");
            var zaehler = await queries.ExecuteAsync(new HoleMesszaehler(stelle));
            ((Messzaehler)zaehler.Data).Anzahl.Should().Be(werte.Count, "ohne Störung genau einmal — zugesichert ist nur mindestens einmal");
            zaehler.Deps.Should().ContainSingle(d => d.Id == stelle.Wert && d.Version == werte.Count,
                "die Antwort zeigt die Id ihrer Query (ohne „mindestens“): der Generator findet sie im Id<T>-Feld");
        }
        finally { await host.StopAsync(); }
    }
}
