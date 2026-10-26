using Abstractions;
using FluentAssertions;
using Infrastructure.Quellen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Import;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Quell-Nachrichten gegen echtes Marten (Plan Pub/Sub-Neustrukturierung §4.2, Phase 3): vom Client mit der Command-Id als Kennung — genau einmal im
/// Log, derselbe Vorgang beim Wiederholen —, und die Option „ein Stream je Schlüssel“: viele Nachrichten in einem Stream, genau einmal je Kennung über
/// das Kennungs-Verzeichnis in derselben Transaktion.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class QuellenE2ETests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Eine_Quell_Nachricht_vom_Client_steht_genau_einmal_im_Log_und_startet_ueber_eine_Funktion_den_Import()
    {
        using var host = Cluster("pruef-quelle-client");
        await host.StartAsync();
        try
        {
            var eingang = QuellEingang.Live(host.Services);
            var kennung = Guid.NewGuid().ToString();
            var vorgang = await eingang.NimmAufAsync(new ImportAngefordert(12) { Kennung = kennung }, default);
            (await eingang.NimmAufAsync(new ImportAngefordert(12) { Kennung = kennung }, default)).Should().Be(vorgang, "dieselbe Kennung, derselbe Vorgang");

            var log = host.Services.GetRequiredService<IEventStoreRepository>();
            (await log.ReadStreamAsync(vorgang, 0, default)).Should().ContainSingle(e => e.Payload is ImportAngefordert);

            // Die Funktion Importannahme (gleichbleibend) startet den Import; die Strom-Funktion legt die Posten an. Ein Event aus einer Quelle weckt
            //   heute niemanden — der Takt holt es (bis zum Wecker am Flush, Phase 6).
            var import = new Herkunft(vorgang, 1, default, null, WirkerId.FuerFunktion("Importannahme")).NeueId<Importauftrag>();
            await Warte(async () => (await Zustand<Importauftrag>(host.Services, import.Wert)).Stand == 12, TimeSpan.FromSeconds(90),
                "die Anforderung startet den Import nicht");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Stream_je_Schluessel_nimmt_jede_Kennung_genau_einmal()
    {
        using var host = Cluster("pruef-quelle-schluessel");
        await host.StartAsync();
        try
        {
            var eingang = QuellEingang.Live(host.Services);
            var schluessel = "ordner-" + Guid.NewGuid().ToString("N")[..8];
            var (a, b) = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            var va = await eingang.NimmAufAsync(new ImportAngefordert(0) { Kennung = a }, default, schluessel);
            var vb = await eingang.NimmAufAsync(new ImportAngefordert(0) { Kennung = b }, default, schluessel);
            (await eingang.NimmAufAsync(new ImportAngefordert(0) { Kennung = a }, default, schluessel)).Should().Be(va, "dieselbe Kennung ein zweites Mal");
            // Gleichzeitig dieselbe neue Kennung von zwei Wegen (Treiber und Webhook): genau eine gewinnt.
            var c = Guid.NewGuid().ToString();
            var gleichzeitig = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => eingang.NimmAufAsync(new ImportAngefordert(0) { Kennung = c }, default, schluessel)));
            gleichzeitig.Distinct().Should().ContainSingle();

            var stream = QuellVorgang.StreamFürSchluessel(typeof(ImportAngefordert), schluessel);
            var events = await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);
            events.Select(e => ((ImportAngefordert)e.Payload).Kennung).Should().BeEquivalentTo(new[] { a, b, c }, "je Kennung genau ein Event im Stream des Schlüssels");
            (await new Infrastructure.Persistence.MartenStreamBestand(host.Services.GetRequiredService<Marten.IDocumentStore>()).BekannteAsync(new[] { va, vb, Guid.NewGuid() }, default))
                .Should().BeEquivalentTo(new[] { va, vb }, "der Bestand kennt die Vorgänge auch ohne eigenen Stream (Abgleich der Treiber beim Start)");
        }
        finally { await host.StopAsync(); }
    }
}
