using Infrastructure.GrpcClient;
using Infrastructure.PubSub.Extensions;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.PubSub.Startup;

/// <summary>
/// Aktiviert Broker für alle Event-Typen.
/// 
/// WICHTIG: Nutzt EventTypeResolver um ALLE Event-Typen zu finden.
/// Kein Domain-Import nötig!
/// </summary>
public class PubSubStartupService : IHostedService
{
    private readonly ActorSystem _actorSystem;

    public PubSubStartupService(ActorSystem actorSystem)
    {
        _actorSystem = actorSystem;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Broker Activation ===");

        var cluster = _actorSystem.Cluster();
        
        // Nutzt den EventTypeResolver - KEIN Domain-Import!
        var eventTypes = EventTypeResolver.GetAllEventTypes().ToList();
        
        Console.WriteLine($"  Aktiviere Broker für {eventTypes.Count} Event-Typen...");
        
        foreach (var type in eventTypes)
        {
            await cluster.ActivateBrokerAsync(type, ct);
            Console.WriteLine($"    ✓ {type.Name}");
        }

        Console.WriteLine($"  ✓ {eventTypes.Count} Broker aktiviert");
        Console.WriteLine();
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}