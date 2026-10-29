using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;
using Proto.Cluster;
using Xunit;

namespace Infrastructure.Pruefstand.Lebenszyklus;

/// <summary>
/// Die zwei Verben (<see cref="Bote"/>) über die Naht: eine Frage ohne Antwort läuft in ihre Frist
/// und liefert „keine Antwort“, eine Ausnahme verlässt sie nie, ohne Frist gibt es keine Frage; eine Meldung kehrt sofort zurück und ist
/// trotzdem gebunden. Gegen den echten Cluster: <see cref="LebenszyklusImClusterTests"/>. Zeitkritisch (echte Uhr).
/// </summary>
[Collection(Zeitkritisch.Name)]
[Trait("Kategorie", "Garantie")]
public class BoteTests
{
    private static readonly ClusterIdentity Ziel = ClusterIdentity.Create("x", "probe");

    private static Bote NieAntwortend(TaskCompletionSource? abgebrochen = null, TimeSpan? meldeFrist = null)
        => new(async (_, _, ct) =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { abgebrochen?.TrySetResult(); }
            return null;
        }, meldeFrist: meldeFrist);

    [Fact]
    public async Task Eine_Frage_ohne_Antwort_laeuft_in_ihre_Frist()
    {
        var uhr = Stopwatch.StartNew();
        var antwort = await NieAntwortend().FrageAsync<WakeAck>(Ziel, new Wake(), TimeSpan.FromMilliseconds(300), default);

        antwort.Should().BeNull("keine Antwort in der Frist");
        uhr.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250)).And.BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Ein_Abbruch_des_Aufrufers_beendet_die_Frage_vor_ihrer_Frist()
    {
        using var abbruch = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var uhr = Stopwatch.StartNew();
        (await NieAntwortend().FrageAsync<WakeAck>(Ziel, new Wake(), TimeSpan.FromSeconds(30), abbruch.Token)).Should().BeNull();
        uhr.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Eine_Antwort_des_erwarteten_Typs_kommt_zurueck_eine_andere_ist_keine()
    {
        var bote = new Bote((_, n, _) => Task.FromResult<object?>(n is Wake ? new WakeAck(Aufgeholt: false) : (object)"fremd"));
        (await bote.FrageAsync<WakeAck>(Ziel, new Wake(), TimeSpan.FromSeconds(1), default)).Should().Be(new WakeAck(false));
        (await bote.FrageAsync<WakeAck>(Ziel, "anderes", TimeSpan.FromSeconds(1), default)).Should().BeNull();
    }

    [Fact]
    public async Task Eine_Ausnahme_verlaesst_die_Frage_nicht()
    {
        var bote = new Bote((_, _, _) => throw new InvalidOperationException("Knoten weg"));
        (await bote.FrageAsync<WakeAck>(Ziel, new Wake(), TimeSpan.FromSeconds(1), default)).Should().BeNull("Ausgang unbekannt");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Eine_Frage_ohne_endliche_Frist_gibt_es_nicht(int millisekunden)
    {
        var bote = new Bote((_, _, _) => Task.FromResult<object?>(new WakeAck()));
        var frist = millisekunden < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(millisekunden);
        var fragen = () => bote.FrageAsync<WakeAck>(Ziel, new Wake(), frist, default);
        await fragen.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Eine_Meldung_kehrt_sofort_zurueck_und_ist_trotzdem_gebunden()
    {
        var abgebrochen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bote = NieAntwortend(abgebrochen, meldeFrist: TimeSpan.FromMilliseconds(200));

        var uhr = Stopwatch.StartNew();
        bote.Melde(Ziel, new Wake());
        uhr.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100), "der Absender wartet nicht");

        await abgebrochen.Task.WaitAsync(TimeSpan.FromSeconds(3));   // die Zustellung endet mit ihrer Frist, statt ewig zu hängen
    }

    [Fact]
    public void Eine_Meldung_verschluckt_jeden_Fehler()
    {
        var bote = new Bote((_, _, _) => throw new InvalidOperationException("Knoten weg"));
        var melden = () => bote.Melde(Ziel, new Wake());
        melden.Should().NotThrow("verlierbar");
    }
}
