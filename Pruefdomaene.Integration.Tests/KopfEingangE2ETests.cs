using Abstractions;
using FluentAssertions;
using Infrastructure.Eingaenge;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Import;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Ein Command ohne Ziel von einem Akteur an den Kopf eines Graphen (docs/04 §4.4, Transport „Akteur → Graph-Kopf“) gegen echtes Marten: derselbe Weg wie
/// der JSON-Eingang und die Leitung — ein offener Eingang je Instanz, die ihn am Kopf nimmt (Kennung aus der Command-Id), dann der schnelle Weg. Derselbe
/// Command zweimal ist dieselbe Zeile, derselbe Lauf, eine Wirkung; der schnelle Weg bringt ihn ohne die Frist des Zustellers.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class KopfEingangE2ETests
{
    private static async Task GibHineinAsync(IHost host, ImportAngefordert command, Guid commandId)
    {
        var eingaenge = Folgenbeilagen.VonAkteur(command, host.Services.GetRequiredService<Verkettung>(), commandId, null, commandId.ToString());
        eingaenge.Should().ContainSingle("genau ein Graph nimmt ImportAngefordert am Kopf");
        await host.Services.GetRequiredService<IOffeneEingaenge>().LegeAsync(eingaenge.Select(e => e.Eingang).ToList(), commandId, default);
        Folgenbeilagen.StelleZu(eingaenge, host.Services.GetRequiredService<IFunktionsWege>(), commandId);
    }

    private static Id<Importauftrag> Import(Guid commandId) =>
        new Herkunft(commandId, 0, default, null, WirkerId.FuerFunktion("Importannahme")).NeueId<Importauftrag>();

    [Fact]
    public async Task Ein_Command_ohne_Ziel_vom_Akteur_startet_ueber_den_Kopf_genau_einen_Import_auch_wiederholt()
    {
        using var host = Cluster("pruef-kopfeingang", takt: global::Infrastructure.Projections.Konsum.StandardTakt);   // der lange Takt: der schnelle Weg zählt
        await host.StartAsync();
        try
        {
            var commandId = Guid.NewGuid();
            await GibHineinAsync(host, new ImportAngefordert(12), commandId);
            await GibHineinAsync(host, new ImportAngefordert(12), commandId);

            await Warte(async () => (await Zustand<Importauftrag>(host.Services, Import(commandId).Wert)).Stand == 12, TimeSpan.FromSeconds(90),
                "die Anforderung startet den Import nicht");
            await Task.Delay(TimeSpan.FromSeconds(2));
            var log = host.Services.GetRequiredService<IEventStoreRepository>();
            (await log.ReadStreamAsync(Import(commandId).Wert, 0, default)).Count(e => e.Payload is ImportGestartet).Should().Be(1,
                "derselbe Command zweimal: dieselbe Zeile am Kopf, dieselbe Command-Id am Importauftrag");

            // Der schnelle Weg: der offene Eingang geht sofort an den Kopf, nicht erst mit dem Zusteller (fällig nach 5 s). Dreimal je unter 5 s.
            for (var i = 0; i < 3; i++)
            {
                var neu = Guid.NewGuid();
                await GibHineinAsync(host, new ImportAngefordert(3), neu);
                await Warte(async () => (await Zustand<Importauftrag>(host.Services, Import(neu).Wert)).Existiert, TimeSpan.FromSeconds(5),
                    $"Durchlauf {i + 1}: der Command erreicht den Kopf nicht auf dem schnellen Weg");
            }
        }
        finally { await host.StopAsync(); }
    }
}
