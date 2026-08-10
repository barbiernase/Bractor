using Abstractions;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Pipeline;

/// <summary>
/// Wiederverwendbarer Timer-Actor: schedult sich per <see cref="IContext.ReenterAfter"/> in festem
/// Intervall selbst und feuert bei jedem Tick über <see cref="TimerTrigger.TickAsync"/>. Mailbox-safe
/// (kein <c>Task.Run</c>, kein Timer-Thread) — genau wie der FileWatcher-Poll.
///
/// Die eigentliche Arbeit (Fabrik + Send) liegt im entkoppelten <see cref="TimerTrigger.TickAsync"/>;
/// dieser Actor trägt nur die Zeitschleife. Beide Nähte sind Konstruktor-Parameter → live geht der
/// Send über den Cluster, im Test über einen Fake.
/// </summary>
public class TimerTriggerActor : IActor
{
    private readonly string _name;
    private readonly TimeSpan _intervall;
    private readonly Func<IPipelineTrigger> _erzeugeTrigger;
    private readonly Func<IPipelineTrigger, Task> _sende;
    private readonly ILogger? _logger;
    private bool _gestoppt;

    /// <summary>Interne Tick-Message — löst genau einen <see cref="TimerTrigger.TickAsync"/> aus.</summary>
    private record Tick;

    public TimerTriggerActor(
        string name,
        TimeSpan intervall,
        Func<IPipelineTrigger> erzeugeTrigger,
        Func<IPipelineTrigger, Task> sende,
        ILogger? logger = null)
    {
        _name = name;
        _intervall = intervall;
        _erzeugeTrigger = erzeugeTrigger;
        _sende = sende;
        _logger = logger;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                _logger?.LogInformation(
                    "TimerTrigger '{Name}' gestartet (Intervall {Intervall})", _name, _intervall);
                Schedule(context);
                break;

            case Tick:
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
                break;

            case Stopping:
                _gestoppt = true;
                _logger?.LogInformation("TimerTrigger '{Name}' gestoppt", _name);
                break;
        }
    }

    private void Schedule(IContext context)
    {
        if (_gestoppt) return;
        context.ReenterAfter(Task.Delay(_intervall), () => context.Send(context.Self, new Tick()));
    }
}
