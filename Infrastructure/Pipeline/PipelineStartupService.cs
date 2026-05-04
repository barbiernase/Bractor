using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Spawnt alle Pipeline-Actors.
///
/// Analog zu SubscriberStartupService.
/// Nutzt GeneratedPipelines.GetPipelineSpawnInfos() — kein Domain-Import nötig.
///
/// Startup-Reihenfolge:
///   1. ClusterStartupService
///   2. PubSubStartupService
///   3. SubscriberStartupService
///   4. PipelineStartupService ← hier
///   5. TriggerStartupService
///
/// Pipelines nach Subscribern — weil Pipeline-Commands Events erzeugen,
/// die von Projektionen verarbeitet werden sollen. Die Projektionen müssen bereit sein.
/// </summary>
public class PipelineStartupService : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<PID> _pipelinePids = new();

    public PipelineStartupService(
        ActorSystem actorSystem,
        IServiceProvider serviceProvider)
    {
        _actorSystem = actorSystem;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Pipeline Startup ===");

        var cluster = _actorSystem.Cluster();

        var spawnInfos = GeneratedPipelines.GetPipelineSpawnInfos(
            _serviceProvider, cluster);

        foreach (var (name, pipelineId, props) in spawnInfos)
        {
            var pid = _actorSystem.Root.Spawn(props);
            _pipelinePids.Add(pid);
            Console.WriteLine($"  ✓ {name} (id: {pipelineId})");
        }

        Console.WriteLine($"  ✓ {_pipelinePids.Count} Pipeline(s) gestartet");
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Pipeline Shutdown ===");

        foreach (var pid in _pipelinePids)
        {
            await _actorSystem.Root.StopAsync(pid);
        }
        _pipelinePids.Clear();

        Console.WriteLine("  ✓ Alle Pipelines gestoppt");
        Console.WriteLine();
    }
}