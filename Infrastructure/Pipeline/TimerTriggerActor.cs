using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Pipeline;

/// <summary>
/// Wiederverwendbarer Timer-Actor: schedult sich neben der Mailbox (Rahmen, <see cref="RahmenActor.Neben"/>) in festem
/// Intervall selbst und feuert bei jedem Tick über <see cref="TimerTrigger.TickAsync"/>. Mailbox-safe
/// (kein <c>Task.Run</c>, kein Timer-Thread) — genau wie der FileWatcher-Poll.
///
/// Die eigentliche Arbeit (Fabrik + Send) liegt im entkoppelten <see cref="TimerTrigger.TickAsync"/>;
/// dieser Actor trägt nur die Zeitschleife. Beide Nähte sind Konstruktor-Parameter → live geht der
/// Send über den Cluster, im Test über einen Fake.
/// </summary>
public class TimerTriggerActor : RahmenActor
{
    private readonly string _name;
    private readonly TimeSpan _intervall;
    private readonly Func<IPipelineTrigger> _erzeugeTrigger;
    private readonly Func<IPipelineTrigger, Task> _sende;
    private readonly ILogger? _logger;

    /// <summary>Interne Tick-Message — löst genau einen <see cref="TimerTrigger.TickAsync"/> aus.</summary>
    private record Tick;

    public TimerTriggerActor(
        string name,
        TimeSpan intervall,
        Func<IPipelineTrigger> erzeugeTrigger,
        Func<IPipelineTrigger, Task> sende,
        ILogger? logger = null)
        : base(logger)
    {
        _name = name;
        _intervall = intervall;
        _erzeugeTrigger = erzeugeTrigger;
        _sende = sende;
        _logger = logger;
    }

    protected override Task BeginneAsync(IContext context)
    {
        _logger?.LogInformation("TimerTrigger '{Name}' gestartet (Intervall {Intervall})", _name, _intervall);
        Schedule(context);
        return Task.CompletedTask;
    }

    protected override async Task BehandleAsync(IContext context)
    {
        if (context.Message is not Tick) return;
        try
        {
            await TimerTrigger.TickAsync(_erzeugeTrigger, _sende);
        }
        catch (Exception ex)
        {
            // Ein Tick-Fehler darf die Zeitschleife nie abreißen lassen — der nächste Tick heilt.
            _logger?.LogWarning(ex, "TimerTrigger '{Name}': Tick fehlgeschlagen", _name);
        }
        Schedule(context);
    }

    protected override Task BeimStoppenAsync(IContext context)
    {
        _logger?.LogInformation("TimerTrigger '{Name}' gestoppt", _name);
        return Task.CompletedTask;
    }

    private void Schedule(IContext context)
        => Neben(context, stopp => Task.Delay(_intervall, stopp), _ =>
        {
            if (!Stopp.IsCancellationRequested) context.Send(context.Self, new Tick());
            return Task.CompletedTask;
        });
}
