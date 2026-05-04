/*using Microsoft.Extensions.DependencyInjection;
using Proto;
using Proto.Cluster;

namespace Infrastructure.PubSub;

public static class PubSubServiceExtensions
{
    /// <summary>
    /// Registriert die PubSub-Infrastruktur (BrokerPublisher)
    /// </summary>
    public static IServiceCollection AddPubSub(this IServiceCollection services)
    {
        services.AddSingleton<BrokerPublisher>(provider =>
        {
            var actorSystem = provider.GetRequiredService<ActorSystem>();
            return new BrokerPublisher(actorSystem.Cluster());
        });
        
        return services;
    }
    
    
    using Application.Subscribers;
    using Microsoft.Extensions.DependencyInjection;
    using Proto;

    namespace Infrastructure.PubSub;

    public static class PubSubServiceExtensions
    {
        /// <summary>
        /// Registriert die PubSub-Infrastruktur (BrokerPublisher).
        /// </summary>
        public static IServiceCollection AddPubSub(this IServiceCollection services)
        {
            services.AddSingleton<BrokerPublisher>(provider =>
            {
                var actorSystem = provider.GetRequiredService<ActorSystem>();
                return new BrokerPublisher(actorSystem.Cluster());
            });
        
            return services;
        }

        /// <summary>
        /// Registriert die Startup-Services für Broker-Aktivierung und Subscriber.
        /// Reihenfolge: Broker zuerst, dann Subscriber.
        /// </summary>
        public static IServiceCollection AddPubSubHostedServices(this IServiceCollection services)
        {
            // Wichtig: Reihenfolge der Registrierung = Reihenfolge des Starts
            services.AddHostedService<PubSubStartupService>();      // 1. Broker aktivieren
            services.AddHostedService<SubscriberStartupService>();  // 2. Subscriber starten
        
            return services;
        }

        /// <summary>
        /// Convenience: Alles zusammen.
        /// </summary>
        public static IServiceCollection AddCompletePubSubSetup(this IServiceCollection services)
        {
            return services
                .AddPubSub()
                .AddSubscribers()
                .AddPubSubHostedServices();
        }
    }
}*/