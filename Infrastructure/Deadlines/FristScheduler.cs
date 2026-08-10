using Abstractions;
using Infrastructure.PubSub;   // CommandEmitter
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Deadlines;

/// <summary>
/// Der DB-Uhr-getriebene Deadline-Scheduler (Feature-Strom, Zielbild §12). Er pollt periodisch die fälligen
/// Fristen — Fälligkeit IMMER gegen die <see cref="IDbClock"/>, nie gegen Node-Zeit — und feuert je Frist den
/// konfigurierten Command über das EINE Emit-Primitiv (<see cref="CommandEmitter"/>): deterministische
/// CommandId (<see cref="FristId.FürZustellung"/>) → der Empfänger dedupliziert (exactly-once wirksam), auch
/// wenn ein Crash zwischen Emit und Entfernen den nächsten Tick erneut feuern lässt.
///
/// Bewusst STANDALONE (keine Prozess-/Marking-Kopplung — die hängt am zurückgestellten P5(b)). Der Kern
/// (<see cref="TickAsync"/>) ist von Cluster/Store entkoppelt → deterministisch prüfbar; das Zeit-Intervall
/// (der Loop) läuft nur live.
/// </summary>
public sealed class FristScheduler : IHostedService
{
    private static readonly TimeSpan Intervall = TimeSpan.FromSeconds(5);

    private readonly IDbClock _clock;
    private readonly IFristplan _plan;
    private readonly ActorSystem _system;
    private readonly Func<Frist, ICommand> _baueCommand;
    private readonly ILogger<FristScheduler>? _logger;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public FristScheduler(
        IDbClock clock,
        IFristplan plan,
        ActorSystem system,
        Func<Frist, ICommand> baueCommand,
        ILogger<FristScheduler>? logger = null)
    {
        _clock = clock;
        _plan = plan;
        _system = system;
        _baueCommand = baueCommand;
        _logger = logger;
    }

    /// <summary>
    /// Ein Scheduler-Durchlauf: die aktuelle DB-Zeit holen, die fälligen Fristen laden und je Frist feuern +
    /// entfernen. Rein — <paramref name="feuere"/> ist der Send-Seam (live Emit, im Test ein Fake). Ein
    /// Feuer-Fehler lässt die Frist stehen (nächster Tick wiederholt, dedup-sicher), stoppt aber die anderen nicht.
    /// </summary>
    public static async Task TickAsync(
        IDbClock clock,
        IFristplan plan,
        Func<Frist, Task> feuere,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var jetzt = await clock.JetztAsync(ct);
        var fällige = await plan.FälligeAsync(jetzt, ct);
        foreach (var frist in fällige)
        {
            try
            {
                await feuere(frist);
                await plan.EntferneAsync(frist.Id, ct);   // erst nach dem Feuern → at-least-once
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[Frist] Zustellung {FristId} fehlgeschlagen — nächster Tick wiederholt", frist.Id);
            }
        }
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = new CancellationTokenSource();
        var emitter = new CommandEmitter(_system.Cluster(), _logger);
        Func<Frist, Task> feuere = frist =>
            emitter.EmitAsync(_baueCommand(frist), FristId.FürZustellung(frist.Id), frist.Id, _cts!.Token);

        _loop = LoopAsync(feuere, _cts.Token);
        _logger?.LogInformation("Deadline-Scheduler gestartet (Intervall {Intervall}s, DB-Uhr)", Intervall.TotalSeconds);
        return Task.CompletedTask;
    }

    private async Task LoopAsync(Func<Frist, Task> feuere, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(Intervall, ct); }
            catch (OperationCanceledException) { break; }

            try { await TickAsync(_clock, _plan, feuere, _logger, ct); }
            catch (Exception ex) { _logger?.LogWarning(ex, "[Frist] Tick fehlgeschlagen — nächster Tick wiederholt"); }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts?.Cancel();
        if (_loop != null) { try { await _loop; } catch { /* egal */ } }
    }
}
