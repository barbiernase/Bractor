using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Infrastructure.PubSub.Extensions;
using Infrastructure.PubSub.Messages;
using Proto.Cluster;

namespace Infrastructure.Pruefstand.PubSub;

/// <summary>
/// P1-3: Fragen an Broker-Actors beim Start wiederholen, solange die Mitgliedschaft noch nicht eingeschwungen ist (ein gerade hart beendeter
/// Knoten steht noch in der Mitgliederliste, der Versuch bleibt ohne Antwort) — jeder Versuch mit seiner Frist, das Ganze begrenzt.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class BrokerWiederholungTests
{
    private static readonly ClusterIdentity Ziel = ClusterIdentity.Create("t", "broker");

    [Fact]
    public async Task Versuche_ohne_Antwort_werden_wiederholt_bis_die_Frage_gelingt()
    {
        var versuche = 0;
        var bote = new Bote((_, _, _) =>
        {
            versuche++;
            return versuche switch
            {
                1 => throw new TimeoutException("Request timed out"),
                2 => throw new InvalidOperationException("Knoten weg"),
                _ => Task.FromResult<object?>(new Ack()),
            };
        });
        (await bote.FrageMitWiederholungAsync<Ack>(Ziel, new Activate(), "Test", default)).Should().NotBeNull();
        versuche.Should().Be(3, "ohne Antwort ist ohne Antwort — gleich, warum");
    }

    [Fact]
    public async Task Ein_Abbruch_des_Aufrufers_beendet_die_Wiederholung()
    {
        var bote = new Bote(async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return null; });
        using var abbruch = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var tun = () => bote.FrageMitWiederholungAsync<Ack>(Ziel, new Activate(), "Test", abbruch.Token);
        await tun.Should().ThrowAsync<OperationCanceledException>();
    }
}
