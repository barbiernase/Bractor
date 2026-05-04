using System;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote.GrpcNet;
using Abstractions;
using Domain.Profil;
using Domain.Lagerartikel;
using Infrastructure.Aggregate.ActorSystem;
using Infrastructure;
using Infrastructure.Extensions;
using Proto.Remote;
/*
namespace DebugApp.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registriert die Basis Infrastructure (EventStore und AggregateHandlerFactory)
        /// </summary>
        public static IServiceCollection AddBasisInfrastructure(this IServiceCollection services)
        {
            Console.WriteLine("[1] Registriere Basis Infrastructure:");
            
            // AggregateHandlerFactory - MUSS VOR EventStore registriert werden!
            services.AddSingleton<IAggregateHandlerFactory>(provider =>
            {
                Console.WriteLine("    > Erstelle AggregateHandlerFactory");
                return new AggregateHandlerFactory();
            });
            Console.WriteLine("    ✓ IAggregateHandlerFactory als Singleton");
            
            // InMemoryEventStore - benötigt IAggregateHandlerFactory
            services.AddSingleton<IEventStoreRepository>(provider =>
            {
                Console.WriteLine("    > Erstelle InMemoryEventStore");
                var factory = provider.GetRequiredService<IAggregateHandlerFactory>();
                return new InMemoryEventStore(factory);
            });
            Console.WriteLine("    ✓ IEventStoreRepository als Singleton\n");
            
            return services;
        }

        /// <summary>
        /// Registriert die Aggregate Handlers mit Factory
        /// </summary>
        public static IServiceCollection AddAggregateHandlers(this IServiceCollection services)
        {
            Console.WriteLine("[2] Registriere Aggregate Handlers:");
            
            // ProfilAggregateHandler - nutzt Factory für korrekte Erstellung
            services.AddTransient<ProfilAggregateHandler>(provider =>
            {
                var factory = provider.GetRequiredService<IAggregateHandlerFactory>();
                var initialState = new Profil(); // Leerer Initial-State
                
                // Factory erstellt Handler mit allen Dependencies (state, decider, applier)
                var handler = factory.CreateHandler(initialState) as ProfilAggregateHandler;
                
                if (handler == null)
                    throw new InvalidOperationException("Factory returned wrong handler type for Profil");
                
                return handler;
            });
            Console.WriteLine("    ✓ ProfilAggregateHandler als Transient (via Factory)");
            
            // LagerartikelAggregateHandler - auch via Factory
            services.AddTransient<LagerartikelAggregateHandler>(provider =>
            {
                var factory = provider.GetRequiredService<IAggregateHandlerFactory>();
                var initialState = new Lagerartikel();
                
                var handler = factory.CreateHandler(initialState) as LagerartikelAggregateHandler;
                
                if (handler == null)
                    throw new InvalidOperationException("Factory returned wrong handler type for Lagerartikel");
                
                return handler;
            });
            Console.WriteLine("    ✓ LagerartikelAggregateHandler als Transient (via Factory)\n");
            
            return services;
        }

        /// <summary>
        /// Registriert die Actors (optional für debugging)
        /// </summary>
        public static IServiceCollection AddActors(this IServiceCollection services)
        {
            Console.WriteLine("[3] Registriere Actors (optional):");
            
            services.AddTransient<ProfilActor>(provider =>
            {
                var handler = provider.GetRequiredService<ProfilAggregateHandler>();
                var eventStore = provider.GetRequiredService<IEventStoreRepository>();
                return new ProfilActor(handler, eventStore);
            });
            Console.WriteLine("    ✓ ProfilActor als Transient");
            
            services.AddTransient<LagerartikelActor>(provider =>
            {
                var handler = provider.GetRequiredService<LagerartikelAggregateHandler>();
                var eventStore = provider.GetRequiredService<IEventStoreRepository>();
                return new LagerartikelActor(handler, eventStore);
            });
            Console.WriteLine("    ✓ LagerartikelActor als Transient\n");
            
            return services;
        }

        /// <summary>
        /// Registriert die ClusterKinds für das Actor System
        /// </summary>
        public static ClusterConfig AddClusterKinds(this ClusterConfig clusterConfig, IServiceProvider provider)
        {
            // ClusterKind für Profil
            Console.WriteLine("    > Füge ClusterKind 'Profil' hinzu");
            clusterConfig = clusterConfig.WithClusterKind(
                "Profil",  // KIND NAME - muss mit CommandEnvelope.AggregateType übereinstimmen!
                Props.FromProducer(() =>
                {
                    // Diese Lambda wird aufgerufen, wenn ein neuer Actor erstellt wird
                    Console.WriteLine("        [Props.FromProducer] Creating ProfilActor...");
                    
                    // Hole Handler (wird via Factory mit allen Dependencies erstellt)
                    var handler = provider.GetRequiredService<ProfilAggregateHandler>();
                    var eventStore = provider.GetRequiredService<IEventStoreRepository>();
                    
                    var actor = new ProfilActor(handler, eventStore);
                    Console.WriteLine("        [Props.FromProducer] ProfilActor created!");
                    
                    return actor;
                })
            );
            
            // ClusterKind für Lagerartikel
            Console.WriteLine("    > Füge ClusterKind 'Lagerartikel' hinzu");
            clusterConfig = clusterConfig.WithClusterKind(
                "Lagerartikel",
                Props.FromProducer(() =>
                {
                    Console.WriteLine("        [Props.FromProducer] Creating LagerartikelActor...");
                    
                    var handler = provider.GetRequiredService<LagerartikelAggregateHandler>();
                    var eventStore = provider.GetRequiredService<IEventStoreRepository>();
                    
                    var actor = new LagerartikelActor(handler, eventStore);
                    Console.WriteLine("        [Props.FromProducer] LagerartikelActor created!");
                    
                    return actor;
                })
            );
            
            return clusterConfig;
        }

        /// <summary>
        /// Erstellt das ClusterConfig mit Consul Provider
        /// </summary>
        public static ClusterConfig CreateClusterConfig(this IServiceProvider provider)
        {
            Console.WriteLine("    > Erstelle Consul Provider");
            var consulProvider = new ConsulProvider(new ConsulProviderConfig());
            
            Console.WriteLine("    > Erstelle ClusterConfig");
            var clusterConfig = ClusterConfig
                .Setup("all-in-one-cluster", consulProvider, new PartitionIdentityLookup());
            
            // Füge die ClusterKinds hinzu
            clusterConfig = clusterConfig.AddClusterKinds(provider);
            
            return clusterConfig;
        }

        /// <summary>
        /// Registriert das komplette Actor System mit Remote und Cluster
        /// </summary>
        public static IServiceCollection AddActorSystem(this IServiceCollection services)
        {
            Console.WriteLine("[4] Konfiguriere Proto.Actor Cluster System:");
            
            services.AddSingleton<ActorSystem>(provider =>
            {
                Console.WriteLine("    > Erstelle ActorSystem");
                var system = new ActorSystem();
                
                // Erstelle ClusterConfig mit allen Kinds
                var clusterConfig = provider.CreateClusterConfig();
                
                Console.WriteLine("    > Erstelle Remote Config");
                var remoteConfig = GrpcNetRemoteConfig
                    .BindToLocalhost()
                    .WithAdvertisedHost("localhost");
                
                Console.WriteLine("    > Konfiguriere ActorSystem mit Remote und Cluster");
                // WICHTIG: WithRemote und WithCluster geben ein neues System zurück!
                system = system
                    .WithRemote(remoteConfig)
                    .WithCluster(clusterConfig);
                
                Console.WriteLine("    > ActorSystem vollständig konfiguriert");
                return system;
            });
            Console.WriteLine("    ✓ ActorSystem als Singleton\n");
            
            return services;
        }

        /// <summary>
        /// Registriert den Dispatcher
        /// </summary>
        public static IServiceCollection AddAggregateDispatcher(this IServiceCollection services)
        {
            Console.WriteLine("[5] Registriere Dispatcher:");
            
            services.AddSingleton<IAggregateDispatcher>(provider =>
            {
                Console.WriteLine("    > Erstelle ProtoActorAggregateDispatcher");
                var system = provider.GetRequiredService<ActorSystem>();
                return new ProtoActorAggregateDispatcher(system);
            });
            Console.WriteLine("    ✓ IAggregateDispatcher als Singleton\n");
            
            return services;
        }

        /// <summary>
        /// Convenience Method: Registriert alles in einem Aufruf
        /// </summary>
        public static IServiceCollection AddCompleteActorSystemSetup(this IServiceCollection services)
        {
            Console.WriteLine("\n╔══════════════════════════════════════════╗");
            Console.WriteLine("║   ALL IN ONE - KOMPLETTE REGISTRIERUNG   ║");
            Console.WriteLine("╚══════════════════════════════════════════╝\n");
            
            services.AddBasisInfrastructure()
                   .AddAggregateHandlers()
                   .AddActors()
                   .AddActorSystem()
                   .AddAggregateDispatcher();
            
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║   REGISTRIERUNG ABGESCHLOSSEN            ║");
            Console.WriteLine("╚══════════════════════════════════════════╝\n");
            
            return services;
        }
    }
}*/