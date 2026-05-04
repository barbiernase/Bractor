using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Spawnt alle Trigger-Actors.
///
/// Iteriert ITriggerRegistration-Instanzen aus dem DI-Container.
/// Domain.Infrastructure registriert konkrete Trigger (FileWatcher, Timer, etc.).
/// Kein Domain-Import nötig — nur das Interface aus Infrastructure.
///
/// Startup-Reihenfolge:
///   4. PipelineStartupService ← Pipelines müssen erreichbar sein
///   5. TriggerStartupService  ← hier (Trigger senden sofort Messages)
/// </summary>
public class TriggerStartupService : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<ITriggerRegistration> _registrations;
    private readonly List<PID> _triggerPids = new();

    public TriggerStartupService(
        ActorSystem actorSystem,
        IServiceProvider serviceProvider,
        IEnumerable<ITriggerRegistration> registrations)
    {
        _actorSystem = actorSystem;
        _serviceProvider = serviceProvider;
        _registrations = registrations;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Trigger Startup ===");

        var cluster = _actorSystem.Cluster();

        foreach (var registration in _registrations)
        {
            var props = registration.CreateProps(_serviceProvider, cluster);
            var pid = _actorSystem.Root.Spawn(props);
            _triggerPids.Add(pid);
            Console.WriteLine($"  ✓ {registration.Name}");
        }

        Console.WriteLine($"  ✓ {_triggerPids.Count} Trigger gestartet");
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Trigger Shutdown ===");

        foreach (var pid in _triggerPids)
        {
            await _actorSystem.Root.StopAsync(pid);
        }
        _triggerPids.Clear();

        Console.WriteLine("  ✓ Alle Trigger gestoppt");
        Console.WriteLine();
    }
}