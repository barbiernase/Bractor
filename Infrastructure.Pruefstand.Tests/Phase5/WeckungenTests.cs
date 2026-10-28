using FluentAssertions;
using Infrastructure.Wecken;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Weckungen je Ziel zusammengefasst (PF-12): viele Signale während einer laufenden Weckung ergeben genau eine weitere, nicht viele; ein
/// Signal nach dem Ende wieder eine eigene; verschiedene Ziele laufen unabhängig.
/// </summary>
public class WeckungenTests
{
    [Fact]
    public async Task Viele_Signale_waehrend_einer_Weckung_ergeben_genau_eine_hinterher()
    {
        var gesendet = new List<Guid>();
        var freigabe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var w = new Weckungen<Guid>(async z => { lock (gesendet) gesendet.Add(z); await freigabe.Task; });
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        w.Wecke(a);
        for (var i = 0; i < 100; i++) w.Wecke(a);
        w.Wecke(b);
        lock (gesendet) gesendet.Should().BeEquivalentTo(new[] { a, b }, "je Ziel ist nur eine unterwegs");

        freigabe.SetResult();
        await Task.Delay(50);
        lock (gesendet) gesendet.Count(z => z == a).Should().Be(2, "nach der Quittung genau eine hinterher");

        w.Wecke(a);
        await Task.Delay(50);
        lock (gesendet) gesendet.Count(z => z == a).Should().Be(3, "nach dem Ende weckt ein Signal wieder");
    }
}
