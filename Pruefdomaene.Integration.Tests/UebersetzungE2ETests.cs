using Abstractions;
using FluentAssertions;
using Infrastructure.Eingaenge;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Messung;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Übersetzung in beide Richtungen (docs/04 §4.4) gegen echtes Marten: eine Client-Nachricht (<see cref="HochrechnungAngefordert"/>) wird am Kopf in den
/// Eingang des Katalogs Rechenwerk übersetzt (Id → Guid ausdrücklich, Faktor aus dem Standardwert), der Skalierer rechnet, das Ergebnis wird ein Command
/// an die Messstelle (Guid → Id). Genau einmal: derselbe Eingang zweimal, und derselbe Kopf nach einem Neustart in einem frischen Host (der Graph-Actor
/// weiß nichts mehr, die Kette rechnet neu) — die Command-Id folgt aus Kopf, Übersetzung und Stelle, die Inbox der Messstelle verwirft die Wiederholung.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class UebersetzungE2ETests
{
    private static IReadOnlyList<(EingangOffen Eingang, object Folge)> Eingaenge(IHost host, HochrechnungAngefordert command, Guid commandId)
    {
        var eingaenge = Folgenbeilagen.VonAkteur(command, host.Services.GetRequiredService<Verkettung>(), commandId, null, commandId.ToString());
        eingaenge.Should().ContainSingle("genau ein Graph nimmt HochrechnungAngefordert am Kopf (die Übersetzung Hinein)");
        return eingaenge;
    }

    private static async Task GibHineinAsync(IHost host, HochrechnungAngefordert command, Guid commandId)
    {
        var eingaenge = Eingaenge(host, command, commandId);
        await host.Services.GetRequiredService<IOffeneEingaenge>().LegeAsync(eingaenge.Select(e => e.Eingang).ToList(), commandId, default);
        Folgenbeilagen.StelleZu(eingaenge, host.Services.GetRequiredService<IFunktionsWege>(), commandId);
    }

    private static async Task<List<AusreisserGemeldet>> GemeldetAsync(IHost host, Id<Messstelle> stelle) =>
        (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stelle.Wert, 0, default))
            .Select(e => e.Payload).OfType<AusreisserGemeldet>().ToList();

    [Fact]
    public async Task Eine_Client_Nachricht_wirkt_ueber_Katalog_und_Uebersetzungen_genau_einmal_am_Aggregat()
    {
        using var host = Cluster("pruef-uebersetzung");
        await host.StartAsync();
        try
        {
            var stelle = new Id<Messstelle>(Guid.NewGuid());
            var commandId = Guid.NewGuid();
            await GibHineinAsync(host, new HochrechnungAngefordert(stelle, 21), commandId);
            await GibHineinAsync(host, new HochrechnungAngefordert(stelle, 21), commandId);

            await Warte(async () => (await GemeldetAsync(host, stelle)).Count > 0, TimeSpan.FromSeconds(60),
                "die Anforderung kommt nicht über Übersetzung → Skalierer → Übersetzung an der Messstelle an");
            await Task.Delay(TimeSpan.FromSeconds(2));
            var gemeldet = await GemeldetAsync(host, stelle);
            gemeldet.Should().ContainSingle("derselbe Eingang zweimal: ein Kopf, eine Kette, eine Command-Id an der Messstelle");
            gemeldet[0].Wert.Should().Be(42, "der Skalierer verdoppelt (Faktor aus dem Standardwert der Übersetzung)");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Derselbe_Kopf_nach_einem_Neustart_rechnet_neu_und_wirkt_nicht_noch_einmal()
    {
        var stelle = new Id<Messstelle>(Guid.NewGuid());
        var commandId = Guid.NewGuid();
        var command = new HochrechnungAngefordert(stelle, 5);

        using (var host = Cluster("pruef-uebersetzung-vorher"))
        {
            await host.StartAsync();
            try
            {
                await GibHineinAsync(host, command, commandId);
                await Warte(async () => (await GemeldetAsync(host, stelle)).Count > 0, TimeSpan.FromSeconds(60), "die erste Kette erreicht die Messstelle nicht");
            }
            finally { await host.StopAsync(); }
        }

        // Ein frischer Host: dem Graph-Actor fehlt das „fertig“ des Kopfs (wie nach einem Absturz vor dem Austragen) — derselbe Kopf kommt erneut,
        //   gesichert, und wir warten auf sein „fertig“: die Kette rechnet neu bis zur Messstelle, deren Inbox die Wiederholung verwirft.
        using (var host = Cluster("pruef-uebersetzung-nachher"))
        {
            await host.StartAsync();
            try
            {
                var (eingang, folge) = Eingaenge(host, command, commandId).Single();
                var kopf = new EingangZustellen(eingang.Kennung, eingang.Instanz, folge, Eingangsart.Kette, commandId, 0, DateTimeOffset.UtcNow, null,
                    commandId.ToString(), null);
                var wege = host.Services.GetRequiredService<IFunktionsWege>();
                EingangQuittung? quittung = null;
                await Warte(async () => (quittung = await wege.StelleZuAsync(kopf, TimeSpan.FromSeconds(10), default))?.Ausgang == Quittungsart.Fertig,
                    TimeSpan.FromSeconds(60), $"der Kopf wird im neuen Host nicht fertig (zuletzt: {quittung?.Ausgang})");

                var gemeldet = await GemeldetAsync(host, stelle);
                gemeldet.Should().ContainSingle("die Kette rechnete im neuen Host neu, mit derselben Command-Id — die Inbox der Messstelle verwarf sie");
                gemeldet[0].Wert.Should().Be(10);
            }
            finally { await host.StopAsync(); }
        }
    }
}
