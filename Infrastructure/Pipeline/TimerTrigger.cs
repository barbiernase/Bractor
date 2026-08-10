using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Zeit-gesteuerter Trigger-Ingress (Zielbild §6): ein Intervall feuert einen konfigurierten
/// <see cref="IPipelineTrigger"/> an die Ziel-Pipeline. Bewusst PUSH — ein Tick ist KEIN Log-Event
/// (kein Cursor, kein Fold, verlierbar per Invariante 2/6). Ein verlorener Tick heilt der nächste.
///
/// Der Kern (<see cref="TickAsync"/>) ist von Proto.Actor entkoppelt und deterministisch prüfbar:
/// zwei Nähte — die reine Trigger-Fabrik und der Send-Seam. Das TIMING selbst (der
/// <c>ReenterAfter</c>-Loop) lebt allein im <see cref="TimerTriggerActor"/> und wird NICHT im
/// Prüfstand getestet (flaky); der Loop ruft nur diesen Kern.
/// </summary>
public static class TimerTrigger
{
    /// <summary>
    /// Ein Tick: die Fabrik erzeugt einen frischen Trigger, der Send-Seam stellt ihn zu.
    /// Rein — keine Zeit, kein Actor, kein Cluster. Ein geworfener Send heilt der nächste Tick.
    /// </summary>
    public static async Task TickAsync(
        Func<IPipelineTrigger> erzeugeTrigger,
        Func<IPipelineTrigger, Task> sende)
    {
        var trigger = erzeugeTrigger();
        await sende(trigger);
    }

    /// <summary>
    /// Registriert einen Timer-Trigger für den <see cref="TriggerStartupService"/>. Der Send-Seam ist
    /// live <see cref="PipelineTriggerSender.SendAsync"/> (bounded/W2). <paramref name="erzeugeTrigger"/>
    /// bleibt reine Fabrik (keine Proto-/Cluster-Abhängigkeit) → deterministisch testbar.
    /// </summary>
    public static ITriggerRegistration Registrierung(
        string name,
        TimeSpan intervall,
        Func<IPipelineTrigger> erzeugeTrigger)
        => new TriggerRegistration(name, (sp, cluster) =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger($"TimerTrigger:{name}");
            return Props.FromProducer(() => new TimerTriggerActor(
                name,
                intervall,
                erzeugeTrigger,
                trigger => PipelineTriggerSender.SendAsync(cluster, trigger, logger),
                logger));
        });
}
