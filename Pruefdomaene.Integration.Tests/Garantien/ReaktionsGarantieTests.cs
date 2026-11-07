using Abstractions;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Reaktion eines Clients (Schritt 6, docs/konzept-akteure.md §5.5) gegen echtes Marten, im echten Cluster: ein Decide gibt nur das Fakt aus —
/// eine Reservierung unter den Mindestbestand ist <see cref="PostenReserviert"/> mit Fehlmenge —, der Client gibt daraufhin den Command an den Kopf
/// des Nachschubs. Genau einmal: liest ein zweiter Client mit verlorenem Stand dieselben Fakten, rechnet er dieselben Command-Ids; der Nachschub
/// läuft noch einmal, seine Buchung trägt dieselbe Id, die Inbox des Postens verwirft sie. Die Ursache der Buchung ist das Fakt (der Eingang am
/// Kopf hat es als Herkunft).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ReaktionsGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(30);

    private static async Task<IReadOnlyList<global::Abstractions.EventEnvelope>> StreamAsync(IHost host, Guid stream) =>
        (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default)).ToList();

    private static Task Reaktionen(int port, global::Client.Infrastructure.Reaktionen.IReaktionsStand stand, CancellationToken ct) =>
        Pruefdomaene.Client.Worker.Reaktionslauf.LaufeAsync($"http://127.0.0.1:{port}", null, stand, Reaktionsname, ct, TimeSpan.FromMilliseconds(200));

    [Fact]
    public async Task Ein_unterschrittener_Posten_wird_genau_einmal_nachgebucht_auch_wenn_ein_Client_mit_verlorenem_Stand_noch_einmal_liest()
    {
        var port = FreierPort();
        using var host = Cluster("reaktion-nachschub", mitReaktionen: false, port: port);
        var stand = NeuerReaktionsstand("pruef_e2e");
        var verloren = NeuerReaktionsstand("pruef_e2e");   // derselbe Anfang: ein Client, dessen Stand nie gesichert wurde
        await host.StartAsync();
        using var ende = new CancellationTokenSource();
        var erster = Reaktionen(port, stand, ende.Token);
        try
        {
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();   // Fehlmenge 3

            await Warte(async () => (await StreamAsync(host, p)).Any(e => e.Payload is PostenZugebucht), Frist, "der Client bucht den Nachschub nicht");
            var zweiter = Reaktionen(port, verloren, ende.Token);
            await Task.Delay(TimeSpan.FromSeconds(5));

            var log = await StreamAsync(host, p);
            log.Where(e => e.Payload is PostenZugebucht).Select(e => ((PostenZugebucht)e.Payload).Menge).Should().Equal([3],
                "genau eine Nachbuchung der Fehlmenge — der zweite Client rechnet dieselben Ids");
            var reserviert = log.First(e => e.Payload is PostenReserviert);
            log.Single(e => e.Payload is PostenZugebucht).Ursache.Should().Be(Spur.Ursache(p, reserviert.AggregateVersion),
                "die Reaktion nennt das Fakt als Ursache, der Eingang am Kopf des Nachschubs trägt es weiter — wie früher die Folge eines Decide");
            await ende.CancelAsync();
            await Task.WhenAll(erster, zweiter);
        }
        finally
        {
            await ende.CancelAsync();
            await host.StopAsync();
        }
    }
}
