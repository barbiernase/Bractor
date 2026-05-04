/*using Abstractions;
using Domain.Projections;
using Infrastructure.Projections;
using Infrastructure.PubSub.Actors;
using Microsoft.Extensions.DependencyInjection;
using Proto;

namespace Infrastructure.PubSub.Startup;

/// <summary>
/// Registriert alle Subscriber.
/// 
/// ★ Phase 2: ReadModelDepsWriter wird registriert und an Actor-Props übergeben.
/// 
/// WIRD SPÄTER VOM GENERATOR ERZEUGT!
/// </summary>
public static class GeneratedSubscribers
{
    /// <summary>
    /// Registriert Subscriber-Logik und Actor-Typen im DI-Container.
    /// </summary>
    public static IServiceCollection RegisterAllSubscribers(IServiceCollection services)
    {
        // Logik-Klassen als Singleton (halten State/ReadModel)
        services.AddSingleton<LagerbestandProjection>();
        services.AddSingleton<AuditLog>();
        
        // ★ Phase 2: ReadModelDepsWriter
        services.AddSingleton<ReadModelDepsWriter>();
        
        Console.WriteLine("  ✓ LagerbestandProjection");
        Console.WriteLine("  ✓ AuditLog");
        Console.WriteLine("  ✓ ReadModelDepsWriter");

        return services;
    }

    /// <summary>
    /// Liefert Props für alle Subscriber-Actors.
    /// Wird von SubscriberStartupService genutzt.
    /// 
    /// ★ Phase 2: ReadModelDepsWriter wird an jeden Actor übergeben.
    /// </summary>
    public static IEnumerable<(string Name, Props Props)> GetSubscriberSpawnInfos(
        IServiceProvider provider,
        BrokerPublisher publisher)
    {
        var depsWriter = provider.GetRequiredService<ReadModelDepsWriter>();

        yield return (
            "LagerbestandProjection",
            Props.FromProducer(() =>
            {
                var logic = provider.GetRequiredService<LagerbestandProjection>();
                return new LagerbestandProjectionActor(logic, publisher, depsWriter);
            })
        );

        yield return (
            "AuditLog",
            Props.FromProducer(() =>
            {
                var logic = provider.GetRequiredService<AuditLog>();
                return new AuditLogActor(logic, publisher, depsWriter);
            })
        );
    }
}*/