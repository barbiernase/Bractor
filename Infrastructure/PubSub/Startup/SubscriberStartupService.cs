using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;

namespace Infrastructure.PubSub.Startup;

/// <summary>
/// Spawnt alle Subscriber-Actors.
/// 
/// WICHTIG: Nutzt GeneratedSubscribers.GetSubscriberSpawnInfos()
/// Kein Domain-Import nötig!
/// </summary>
public class SubscriberStartupService : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<PID> _subscriberPids = new();

    public SubscriberStartupService(
        ActorSystem actorSystem,
        IServiceProvider serviceProvider)
    {
        _actorSystem = actorSystem;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Subscriber Startup ===");

        var publisher = _serviceProvider.GetRequiredService<BrokerPublisher>();
        
        // Nutzt generierte Spawn-Infos - KEIN Domain-Import!
        var spawnInfos = GeneratedSubscribers.GetSubscriberSpawnInfos(_serviceProvider, publisher);
        
        foreach (var (name, props) in spawnInfos)
        {
            var pid = _actorSystem.Root.Spawn(props);
            _subscriberPids.Add(pid);
            Console.WriteLine($"  ✓ {name}");
        }

        Console.WriteLine($"  ✓ {_subscriberPids.Count} Subscriber gestartet");
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Subscriber Shutdown ===");

        foreach (var pid in _subscriberPids)
        {
            await _actorSystem.Root.StopAsync(pid);
        }
        _subscriberPids.Clear();

        Console.WriteLine("  ✓ Alle Subscriber gestoppt");
        Console.WriteLine();
    }
}