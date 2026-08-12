using System;
using System.Threading;
using System.Threading.Tasks;
using Client.Infrastructure.Connection;
using Xunit;

namespace Client.Infrastructure.CollectionTests;

/// <summary>
/// T2b — der PendingCommandTracker trägt die „warte auf korrelierte Antwort oder Timeout"-Logik des
/// Client-Retry-Loops. Getestet ohne echten Server (reine Warte-/Ack-Semantik).
/// </summary>
public class PendingCommandTrackerTests
{
    [Fact]
    public async Task Ack_beendet_das_Warten_sofort()
    {
        var t = new PendingCommandTracker();
        t.Register("c1");

        var wait = t.WaitAsync("c1", TimeSpan.FromSeconds(5), CancellationToken.None);
        t.Ack("c1");                       // Antwort trifft ein

        Assert.True(await wait);           // geackt, nicht Timeout
    }

    [Fact]
    public async Task Ohne_Ack_laeuft_der_Timeout_ab()
    {
        var t = new PendingCommandTracker();
        t.Register("c2");

        var acked = await t.WaitAsync("c2", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(acked);               // Stille → Timeout
    }

    [Fact]
    public async Task Nicht_registrierte_Correlation_gilt_als_geackt()
        => Assert.True(await new PendingCommandTracker()
            .WaitAsync("unbekannt", TimeSpan.FromSeconds(5), CancellationToken.None));

    [Fact]
    public void Ack_unbekannter_Id_ist_folgenlos()
        => new PendingCommandTracker().Ack("gibtsnicht");   // wirft nicht

    [Fact]
    public void Register_ist_idempotent_und_Forget_raeumt_auf()
    {
        var t = new PendingCommandTracker();
        t.Register("c3");
        t.Register("c3");                  // kein zweiter Eintrag
        Assert.Equal(1, t.PendingCount);
        Assert.True(t.IsPending("c3"));

        t.Forget("c3");
        Assert.False(t.IsPending("c3"));
        Assert.Equal(0, t.PendingCount);
    }
}
