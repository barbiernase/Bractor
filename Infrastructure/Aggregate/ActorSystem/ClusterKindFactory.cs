/*using Abstractions;
using Domain.Lagerartikel;
using Domain.Profil;
using Infrastructure.PubSub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Factory für ClusterKinds - wird später durch Code-Generierung ersetzt.
/// 
/// ★ Phase 4: IVersionTracker wird per GetService aufgelöst (optional).
/// </summary>
public static class ClusterKindsFactory
{
    public static ClusterKind[] CreateAllKinds(IServiceProvider serviceProvider)
    {
        var handlerFactory = serviceProvider.GetRequiredService<IAggregateHandlerFactory>();
        var eventStore = serviceProvider.GetRequiredService<IEventStoreRepository>();
        var versionTracker = serviceProvider.GetService<IVersionTracker>();  // ★ Optional
    
        return new[]
        {
            new ClusterKind(
                nameof(Profil),
                Props.FromProducer(() =>
                {
                    Console.WriteLine("[Factory] Creating ProfilActor");
                    var publisher = serviceProvider.GetService<BrokerPublisher>();
                    var logger = serviceProvider.GetService<ILogger<ProfilActor>>();
                    return new ProfilActor(handlerFactory, eventStore, versionTracker, publisher, logger);
                })
            ),
            
            new ClusterKind(
                nameof(Lagerartikel),
                Props.FromProducer(() =>
                {
                    Console.WriteLine("[Factory] Creating LagerartikelActor");
                    var publisher = serviceProvider.GetService<BrokerPublisher>();
                    var logger = serviceProvider.GetService<ILogger<LagerartikelActor>>();
                    return new LagerartikelActor(handlerFactory, eventStore, versionTracker, publisher, logger);
                })
            )
        };
    }
}*/