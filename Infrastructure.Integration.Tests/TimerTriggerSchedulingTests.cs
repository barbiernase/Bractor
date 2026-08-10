using Abstractions;
using FluentAssertions;
using Infrastructure.Pipeline;
using Proto;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — der REALE Zeit-Loop des <see cref="TimerTriggerActor"/> (das Stück, das der deterministische
/// Prüfstand bewusst auslässt). Ein echtes <see cref="ActorSystem"/> (kein Cluster nötig — der Send-Seam ist
/// ein Zähler) beweist: der <c>ReenterAfter</c>-Loop feuert WIEDERHOLT im konfigurierten Intervall, und
/// <see cref="Stopping"/> beendet ihn.
///
/// Timing-tolerant (Intervall 200ms, Wartefenster großzügig) — kein Postgres/Consul/Redis.
/// </summary>
public class TimerTriggerSchedulingTests
{
    private record TestTrigger(int Seq) : IPipelineTrigger;

    [Fact]
    public async Task Der_Actor_feuert_wiederholt_im_Intervall_und_stoppt_sauber()
    {
        var system = new ActorSystem();
        var seq = 0;
        var gesendet = 0;

        var props = Props.FromProducer(() => new TimerTriggerActor(
            name: "test",
            intervall: TimeSpan.FromMilliseconds(200),
            erzeugeTrigger: () => new TestTrigger(Interlocked.Increment(ref seq)),
            sende: _ => { Interlocked.Increment(ref gesendet); return Task.CompletedTask; }));

        var pid = system.Root.Spawn(props);
        try
        {
            // Mindestens drei Ticks innerhalb eines großzügigen Fensters → der Loop läuft echt.
            await WaitAsync(() => Volatile.Read(ref gesendet) >= 3, TimeSpan.FromSeconds(10),
                "der ReenterAfter-Loop hat nicht wiederholt gefeuert");

            gesendet.Should().BeGreaterThanOrEqualTo(3);
        }
        finally
        {
            await system.Root.StopAsync(pid);
        }

        // Nach dem Stop darf kein weiterer Tick mehr laufen.
        var standNachStop = Volatile.Read(ref gesendet);
        await Task.Delay(600);
        Volatile.Read(ref gesendet).Should().Be(standNachStop, "Stopping beendet die Zeitschleife");

        await system.ShutdownAsync();
    }

    private static async Task WaitAsync(Func<bool> bedingung, TimeSpan timeout, string message)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (bedingung()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException(message);
    }
}
