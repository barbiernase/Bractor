using Abstractions;
using FluentAssertions;
using Infrastructure.Pipeline;

namespace Infrastructure.Pruefstand.Pipeline;

/// <summary>
/// Ebene 1 (reiner Kontrollfluss, Fakes) — der entkoppelte Timer-Kern <see cref="TimerTrigger.TickAsync"/>.
///
/// Bewiesen wird die deterministische Naht: Fabrik → Send. Das TIMING (der <c>ReenterAfter</c>-Loop im
/// <see cref="TimerTriggerActor"/>) wird hier bewusst NICHT getestet (flaky) — das deckt der
/// real-schedulende Integrationstest ab.
/// </summary>
public class TimerTriggerTests
{
    private record TestTrigger(int Seq) : IPipelineTrigger;

    [Fact]
    public async Task Jeder_Tick_erzeugt_frisch_aus_der_Fabrik_und_sendet_genau_das()
    {
        var erzeugt = new TestTrigger(7);
        IPipelineTrigger? gesendet = null;

        await TimerTrigger.TickAsync(
            erzeugeTrigger: () => erzeugt,
            sende: t => { gesendet = t; return Task.CompletedTask; });

        gesendet.Should().BeSameAs(erzeugt, "der Send-Seam bekommt exakt den von der Fabrik erzeugten Trigger");
    }

    [Fact]
    public async Task Eine_zustandsbehaftete_Fabrik_zaehlt_ueber_mehrere_Ticks_hoch()
    {
        var seq = 0;
        Func<IPipelineTrigger> fabrik = () => new TestTrigger(++seq);
        var gesendet = new List<int>();
        Func<IPipelineTrigger, Task> sende = t => { gesendet.Add(((TestTrigger)t).Seq); return Task.CompletedTask; };

        for (var i = 0; i < 3; i++)
            await TimerTrigger.TickAsync(fabrik, sende);

        // jeder Tick zieht den nächsten Fabrik-Wert und stellt ihn zu
        gesendet.Should().Equal(1, 2, 3);
    }
}
