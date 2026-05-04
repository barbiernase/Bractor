using Abstractions;
using Domain.Infrastructure;
using Infrastructure.Aggregate.ActorSystem;
using Infrastructure.GrpcClient;
using Infrastructure.Persistence;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Extensions;
using Infrastructure.PubSub.Startup;
using Infrastructure.Startup;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.DependencyInjection;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using StackExchange.Redis;
using Weasel.Core;

namespace Infrastructure.Extensions;

/// <summary>
/// Zentrale Service-Registrierung fuer das CQRS-Framework.
/// 
/// WICHTIG: Diese Klasse hat KEIN Domain-Wissen!
/// Domain-Komponenten werden ueber Domain.Infrastructure registriert:
///   services.AddDomainProjectionServices()  -- Stores + Reader
///
/// Generierte Registrierungen:
///   GeneratedAggregates   -- Aggregate-Actors + Factory
///   GeneratedSubscribers  -- Subscriber-Actors (Projektionen/Writer)
/// </summary>
public static class CqrsServiceExtensions
{
    /// <summary>
    /// Registriert das komplette CQRS-Framework.
    /// </summary>
    public static IServiceCollection AddCqrsFramework(
        this IServiceCollection services,
        Action<CqrsFrameworkBuilder>? configure = null)
    {
        var builder = new CqrsFrameworkBuilder(services);
        configure?.Invoke(builder);
        
        Console.WriteLine();
        Console.WriteLine("==========================================================");
        Console.WriteLine("              CQRS Framework - Service Registration        ");
        Console.WriteLine("==========================================================");
        Console.WriteLine();
        
        // 1. Infrastruktur (Marten + Redis)
        services.AddCqrsInfrastructure(builder);
        
        // 2. Domain-Stores + Reader (aus Domain.Infrastructure)
        //    MUSS VOR Subscribers -- Projektionen brauchen WriteStores!
        services.AddDomainProjectionServices();
        
        // 3. Aggregate-Components -- GENERIERT
        services.AddCqrsAggregates();
        
        // 4. Subscribers (Projektionen/Writer) -- GENERIERT
        //    DI loest WriteStore-Konstruktor automatisch auf (Stores sind registriert)
        services.AddCqrsSubscribers();
        
        // 5. PubSub
        services.AddCqrsPubSub();
        
        // 6. Actor System mit Cluster
        services.AddCqrsActorSystem(builder);
        
        // 7. Query Infrastructure (DepsReader)
        services.AddCqrsQueryService();
        
        // 8. gRPC Client Services (optional)
        if (builder.EnableGrpc)
        {
            services.AddGrpcClientServices();
        }
        
        // 9. Hosted Services
        services.AddCqrsHostedServices();
        
        return services;
    }
    
    /// <summary>
    /// Basis-Infrastruktur: EventStore (Marten/PostgreSQL), VersionTracker (Redis).
    /// </summary>
    public static IServiceCollection AddCqrsInfrastructure(
        this IServiceCollection services,
        CqrsFrameworkBuilder builder)
    {
        Console.WriteLine("[CQRS] Registriere Infrastruktur...");
        
        // Marten (PostgreSQL EventStore)
        services.AddMarten(options =>
        {
            options.Connection(builder.EventStoreConnectionString);
            options.Events.DatabaseSchemaName = builder.EventStoreSchema;
            options.DatabaseSchemaName = builder.EventStoreSchema;
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

            MartenEventTypeRegistration.RegisterEventTypes(options);
        });

        services.AddSingleton<IEventStoreRepository>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var factory = provider.GetRequiredService<IAggregateHandlerFactory>();
            var logger = provider.GetRequiredService<ILogger<MartenEventStore>>();
            return new MartenEventStore(store, factory, logger);
        });
        Console.WriteLine($"  + IEventStoreRepository (Marten/PostgreSQL)");
        Console.WriteLine($"    Schema: {builder.EventStoreSchema}");
        
        // Redis (VersionTracker)
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var config = new ConfigurationOptions
            {
                EndPoints = { builder.RedisConnectionString },
                DefaultDatabase = builder.RedisDatabase,
                AbortOnConnectFail = false,
            };
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<IVersionTracker, RedisVersionTracker>();
        Console.WriteLine($"  + IVersionTracker (Redis)");
        Console.WriteLine($"    Endpoint: {builder.RedisConnectionString}, DB: {builder.RedisDatabase}");

        Console.WriteLine();
        return services;
    }

    /// <summary>
    /// Aggregate-Components registrieren -- GENERIERT.
    /// </summary>
    public static IServiceCollection AddCqrsAggregates(this IServiceCollection services)
    {
        Console.WriteLine("[CQRS] Registriere Aggregates...");
        GeneratedAggregates.RegisterAllAggregateComponents(services);
        Console.WriteLine("  + Aggregate-Components (generiert)");
        Console.WriteLine();
        return services;
    }
    
    /// <summary>
    /// Subscribers registrieren -- DELEGIERT an GeneratedSubscribers.
    /// Projektionen (Writer) werden hier registriert.
    /// DI loest deren WriteStore-Konstruktor automatisch auf.
    /// </summary>
    public static IServiceCollection AddCqrsSubscribers(this IServiceCollection services)
    {
        Console.WriteLine("[CQRS] Registriere Subscribers...");
        GeneratedSubscribers.RegisterAllSubscribers(services);
        Console.WriteLine();
        return services;
    }
    
    /// <summary>
    /// Query Infrastructure: ReadModelDepsReader (Redis).
    /// 
    /// Reader + ProjectionQueryService sind bereits ueber
    /// AddDomainProjectionServices() registriert (Domain.Infrastructure).
    /// Hier kommt nur das Infrastructure-Zeug (Redis DepsReader).
    /// </summary>
    public static IServiceCollection AddCqrsQueryService(this IServiceCollection services)
    {
        Console.WriteLine("[CQRS] Registriere Query Infrastructure...");
        
        // ReadModelDepsReader: Redis-Abhaengigkeit → gehoert zu Infrastructure
        services.AddSingleton<IReadModelDepsReader, ReadModelDepsReader>();
        Console.WriteLine("  + ReadModelDepsReader");
        
        Console.WriteLine();
        return services;
    }

    /// <summary>
    /// PubSub: BrokerPublisher (lazy)
    /// </summary>
    public static IServiceCollection AddCqrsPubSub(this IServiceCollection services)
    {
        Console.WriteLine("[CQRS] Registriere PubSub...");
        
        services.AddSingleton<BrokerPublisher>(provider =>
        {
            var actorSystem = provider.GetRequiredService<ActorSystem>();
            return new BrokerPublisher(actorSystem.Cluster());
        });
        Console.WriteLine("  + BrokerPublisher (lazy)");
        
        Console.WriteLine();
        return services;
    }
    
    /// <summary>
    /// Actor System mit Cluster.
    ///
    /// FIX: Nimmt jetzt den gesamten Builder statt nur clusterName.
    /// Damit wird ConsulAddress tatsaechlich an ConsulProviderConfig uebergeben
    /// und AdvertisedHost an GrpcNetRemoteConfig — beides fehlte vorher.
    /// </summary>
    public static IServiceCollection AddCqrsActorSystem(
        this IServiceCollection services,
        CqrsFrameworkBuilder builder)
    {
        Console.WriteLine("[CQRS] Registriere ActorSystem...");
        Console.WriteLine($"  Cluster: {builder.ClusterName}");
        Console.WriteLine($"  Consul:  {builder.ConsulAddress}");
        Console.WriteLine($"  Advertised Host: {builder.AdvertisedHost}");
    
        services.AddSingleton<ActorSystem>(provider =>
        {
            var loggerFactory = provider.GetService<ILoggerFactory>();
        
            var system = new ActorSystem()
                .WithServiceProvider(provider);

            // FIX: ConsulAddress aus Builder durchreichen (war vorher ignoriert!)
            // ConsulProviderConfig hat kein Address-Property —
            // die Adresse wird über den clientConfiguration-Callback gesetzt.
            var consulProvider = new ConsulProvider(
                new ConsulProviderConfig(),
                clientConfiguration: c => c.Address = new Uri($"http://{builder.ConsulAddress}"));
        
            var clusterConfig = ClusterConfig
                .Setup(
                    clusterName: builder.ClusterName,
                    clusterProvider: consulProvider,
                    identityLookup: new PartitionIdentityLookup())
                .WithBrokerKinds(loggerFactory);
        
            var aggregateKinds = GeneratedAggregates.GetAllKinds(system);
            foreach (var kind in aggregateKinds)
            {
                clusterConfig = clusterConfig.WithClusterKind(kind);
            }
        
            Console.WriteLine($"  + {aggregateKinds.Length} Aggregate-Kinds registriert");
        
            var pipelineKinds = Infrastructure.Pipeline.GeneratedPipelines.GetPipelineKinds(provider, system);
            foreach (var kind in pipelineKinds)
            {
                clusterConfig = clusterConfig.WithClusterKind(kind);
            }
            Console.WriteLine($"  + {pipelineKinds.Length} Pipeline-Kinds registriert");

            // FIX: BindTo("0.0.0.0") statt BindToLocalhost() — sonst ist der
            // Cluster-Node von anderen Hosts/Containern nicht erreichbar.
            // AdvertisedHost teilt anderen Nodes mit, unter welcher Adresse
            // dieser Node erreichbar ist.
            var remoteConfig = GrpcNetRemoteConfig
                .BindTo("0.0.0.0")
                .WithAdvertisedHost(builder.AdvertisedHost);
        
            system
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);
        
            Console.WriteLine("  + ActorSystem erstellt");
            return system;
        });
    
        services.AddSingleton<IAggregateDispatcher>(provider =>
        {
            var system = provider.GetRequiredService<ActorSystem>();
            return new ProtoActorAggregateDispatcher(system);
        });
        Console.WriteLine("  + IAggregateDispatcher");
    
        Console.WriteLine();
        return services;
    }
    
    /// <summary>
    /// Hosted Services in korrekter Reihenfolge
    /// </summary>
    public static IServiceCollection AddCqrsHostedServices(this IServiceCollection services)
    {
        Console.WriteLine("[CQRS] Registriere Hosted Services...");
        
        services.AddHostedService<ClusterStartupService>();
        Console.WriteLine("  + ClusterStartupService");
        
        services.AddHostedService<PubSubStartupService>();
        Console.WriteLine("  + PubSubStartupService");
        
        services.AddHostedService<SubscriberStartupService>();
        Console.WriteLine("  + SubscriberStartupService");
        
        services.AddHostedService<Infrastructure.Pipeline.PipelineStartupService>();
        Console.WriteLine("  + PipelineStartupService");
        
        services.AddHostedService<Infrastructure.Pipeline.TriggerStartupService>();
        Console.WriteLine("  + TriggerStartupService");
        
        Console.WriteLine();
        return services;
    }
}

/// <summary>
/// Builder fuer Framework-Konfiguration.
/// </summary>
public class CqrsFrameworkBuilder
{
    internal IServiceCollection Services { get; }
    
    public string ClusterName { get; set; } = "cqrs-cluster";
    public string ConsulAddress { get; set; } = "localhost:8500";
    
    /// <summary>
    /// Host-Adresse die anderen Cluster-Mitgliedern mitgeteilt wird.
    /// Lokal: "localhost", Produktion: Hostname oder IP des Servers.
    /// </summary>
    public string AdvertisedHost { get; set; } = "localhost";
    
    public bool EnableGrpc { get; set; } = true;

    public string EventStoreConnectionString { get; set; } = 
        "Host=localhost;Database=cqrs_events;Username=postgres;Password=postgres";
    public string EventStoreSchema { get; set; } = "es";

    public string RedisConnectionString { get; set; } = "localhost:6379";
    public int RedisDatabase { get; set; } = 1;
    
    public CqrsFrameworkBuilder(IServiceCollection services)
    {
        Services = services;
    }
}