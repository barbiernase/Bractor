using FluentAssertions;
using Infrastructure.PubSub.Extensions;

namespace Infrastructure.Pruefstand.PubSub;

/// <summary>
/// P1-3: Anfragen an Broker-Actors beim Start wiederholen, solange die Mitgliedschaft noch nicht eingeschwungen ist (ein gerade hart beendeter
/// Knoten steht noch in der Mitgliederliste, die Anfrage läuft in den Timeout) — andere Fehler gehen sofort durch.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class BrokerWiederholungTests
{
    [Fact]
    public async Task Timeouts_werden_wiederholt_bis_die_Anfrage_gelingt()
    {
        var versuche = 0;
        await ClusterExtensions.MitWiederholungAsync(_ =>
        {
            versuche++;
            return versuche < 3 ? throw new TimeoutException("Request timed out") : Task.CompletedTask;
        }, "Test", default);
        versuche.Should().Be(3);
    }

    [Fact]
    public async Task Ein_anderer_Fehler_wird_nicht_wiederholt()
    {
        var versuche = 0;
        var tun = () => ClusterExtensions.MitWiederholungAsync(_ =>
        {
            versuche++;
            throw new InvalidOperationException("kaputt");
        }, "Test", default);
        await tun.Should().ThrowAsync<InvalidOperationException>();
        versuche.Should().Be(1);
    }
}
