using System;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Lebenszyklus;

/// <summary>
/// Die eine Passivierungs-Regel (PF-5, im Rahmen jedes Actors, <see cref="RahmenActor"/>): ein Actor ohne Nachricht stoppt nach dem Leerlauf;
/// jede Nachricht schiebt den Leerlauf hinaus; 0 heißt nie.
/// Lokales <see cref="ActorSystem"/> ohne Cluster — dass Aggregat und Adapter nach der Reaktivierung denselben Stand haben, prüft die Prüfdomäne
/// (<c>LebenszyklusGarantieTests</c>). Zeitkritisch (echte Leerlauf-Uhr): läuft nicht parallel zu den übrigen Tests.
/// </summary>
[Collection(Zeitkritisch.Name)]
public class PassivierungTests
{
    private sealed class Probe(TimeSpan leerlauf, TaskCompletionSource gestoppt) : RahmenActor(leerlauf: leerlauf)
    {
        protected override Task BehandleAsync(IContext context) => Task.CompletedTask;

        protected override Task BeimStoppenAsync(IContext context)
        {
            gestoppt.TrySetResult();
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
        // 1,8 s lang alle 150 ms eine Nachricht — dreimal so lang wie der Leerlauf; Spielraum 450 ms Verzug je Weckruf (paralleler Testlauf).
        var (system, pid, gestoppt) = Starte(TimeSpan.FromMilliseconds(600));
        for (var i = 0; i < 12; i++)
        {
            await Task.Delay(150);
            system.Root.Send(pid, "Weckruf");
        }
        gestoppt.Task.IsCompleted.Should().BeFalse("1,8 s lang kam alle 150 ms eine Nachricht, der Leerlauf ist 600 ms");
        await gestoppt.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await system.ShutdownAsync();
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(0, false)]
    public void Ein_zu_kurzer_Leerlauf_des_Prozess_Managers_ist_ein_Start_Fehler(int sekunden, bool fehler)
    {
        var akt = () => Passivierung.Aus(new Abstractions.LebenszyklusKonfig { ProzessManagerLeerlaufSekunden = sekunden });
        if (fehler) akt.Should().Throw<InvalidOperationException>().WithMessage("*Backstop*");
        else akt.Should().NotThrow();
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

/// <summary>Tests mit echter Uhr (Leerlauf, Fristen im Millisekunden-Bereich): nie parallel zu anderen — ein CPU-hungriger Nachbar (Analyzer-
/// Kompilate) verschiebt sonst ihre Takte.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Zeitkritisch
{
    public const string Name = "Zeitkritisch (echte Uhr)";
}
