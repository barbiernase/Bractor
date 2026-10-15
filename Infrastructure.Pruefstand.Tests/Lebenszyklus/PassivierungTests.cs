using System;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Lebenszyklus;

/// <summary>
/// Die eine Passivierungs-Regel (PF-5): ein Actor ohne Nachricht stoppt nach dem Leerlauf; jede Nachricht schiebt den Leerlauf hinaus; 0 heißt nie.
/// Lokales <see cref="ActorSystem"/> ohne Cluster — dass Aggregat und Adapter nach der Reaktivierung denselben Stand haben, prüft die Prüfdomäne
/// (<c>LebenszyklusGarantieTests</c>).
/// </summary>
public class PassivierungTests
{
    private sealed class Probe(TimeSpan leerlauf, TaskCompletionSource gestoppt) : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (Passivierung.Behandle(context, leerlauf)) return Task.CompletedTask;
            if (context.Message is Stopped) gestoppt.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private static (ActorSystem System, PID Pid, TaskCompletionSource Gestoppt) Starte(TimeSpan leerlauf)
    {
        var system = new ActorSystem();
        var gestoppt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pid = system.Root.Spawn(Props.FromProducer(() => new Probe(leerlauf, gestoppt)));
        return (system, pid, gestoppt);
    }

    [Fact]
    public async Task Ein_Actor_ohne_Nachricht_stoppt_nach_dem_Leerlauf()
    {
        var (system, _, gestoppt) = Starte(TimeSpan.FromMilliseconds(200));
        await gestoppt.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Jede_Nachricht_schiebt_den_Leerlauf_hinaus()
    {
        var (system, pid, gestoppt) = Starte(TimeSpan.FromMilliseconds(400));
        for (var i = 0; i < 6; i++)
        {
            await Task.Delay(150);
            system.Root.Send(pid, "Weckruf");
        }
        gestoppt.Task.IsCompleted.Should().BeFalse("900 ms lang kam alle 150 ms eine Nachricht");
        await gestoppt.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Null_heisst_nie_passivieren()
    {
        var (system, _, gestoppt) = Starte(TimeSpan.Zero);
        await Task.Delay(500);
        gestoppt.Task.IsCompleted.Should().BeFalse();
        await system.ShutdownAsync();
    }
}
