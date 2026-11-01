using Abstractions;
using FluentAssertions;
using Infrastructure.Emit;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Proto.Cluster;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Antwort auf einen emittierten Command (docs/04 §4.4) gegen echtes Marten: sie trägt die Events, die der Command entschied, mit ihren
/// Versionen — auch bei einer erkannten Wiederholung (dann die des ersten Mals, gesucht über die causation_id im Stream des Ziels). Eine Funktion vor
/// dem Aggregat hört sie als Antwort auf ihren Command; rechnet sie einen Eingang erneut, sieht sie dieselben Events.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AntwortGarantieTests
{
    [Fact]
    public async Task Die_Antwort_traegt_die_entschiedenen_Events_auch_bei_einer_Wiederholung()
    {
        using var host = Cluster("antwort-wiederholung", inboxCache: 1);   // kleiner Cache: die Wiederholung findet die Inbox-Tabelle
        await host.StartAsync();
        try
        {
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 0), Guid.NewGuid()))!.Success.Should().BeTrue();
            var emitter = new CommandEmitter(new Bote(host.Services.GetRequiredService<ActorSystem>().Cluster()));
            var id = Guid.NewGuid();
            var erste = await emitter.EmitMitAntwortAsync(new ReservierePosten(new(p), 5, Beleg.Ohne), id, Guid.NewGuid(), default);
            erste.Ausgang.Should().Be(EmitAusgang.Zugestellt);
            erste.Events.Should().ContainSingle().Which.Should().Be(new Antwortereignis(new PostenReserviert(5, 0, Beleg.Ohne), 2));

            // Andere Commands dazwischen — dann dieselbe Id noch einmal: die Inbox erkennt sie, die Antwort ist die des ersten Mals.
            (await FrageAsync(host, new ReservierePosten(new(p), 1, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();
            var zweite = await emitter.EmitMitAntwortAsync(new ReservierePosten(new(p), 5, Beleg.Ohne), id, Guid.NewGuid(), default);
            zweite.Ausgang.Should().Be(EmitAusgang.Zugestellt);
            zweite.Events.Should().Equal(erste.Events, "eine Wiederholung bekommt dieselben Events mit denselben Versionen");
            (await Zustand<Posten.Posten>(host.Services, p)).Reserviert.Should().Be(6, "gewirkt hat die Reservierung einmal");
        }
        finally { await host.StopAsync(); }
    }
}
