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
            // ★ Audit-Fix #2: expliziter, konfigurierbarer Command-Timeout (statt implizitem Npgsql-Default ~30s).
            //   Store-weite, gefahrlose Grenze für einen DB-Stall im Actor-Turn (keine per-Call-Write-Abbrüche →
            //   kein mehrdeutiger-Commit-Hazard). Aufrufer-Seite zusätzlich über #1 (Dispatcher-Dead-Letter) gedeckt.
            //   Über den Npgsql-Connection-String gesetzt (version-unabhängig) — CommandTimeout in Sekunden.
            var eventStoreConn = new Npgsql.NpgsqlConnectionStringBuilder(builder.EventStoreConnectionString)
            {
                CommandTimeout = builder.CommandTimeoutSeconds
            };
            options.Connection(eventStoreConn.ConnectionString);
            options.Events.DatabaseSchemaName = builder.EventStoreSchema;
            options.DatabaseSchemaName = builder.EventStoreSchema;
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

            MartenEventTypeRegistration.RegisterEventTypes(options);

            // ★ Phase 1: Event-Metadaten aktivieren, damit CorrelationId/CausationId/
            //   Header (aggregate_type) beim Append persistiert und nach dem Log-Read
            //   (ReadStreamAsync) wieder verfügbar sind.
            options.Events.MetadataConfig.CorrelationIdEnabled = true;
            options.Events.MetadataConfig.CausationIdEnabled = true;
            options.Events.MetadataConfig.HeadersEnabled = true;

            // Fortschrittsmarke der Projektionen (Phase 0 / Phase 2):
            // durables Checkpoint-Dokument pro (Projektion, Stream).
            options.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);

            // Durabler Poll-Backstop-Cursor pro Pull-Pfad (Phase 4b): der Poller setzt nach
            // einem Neustart hier wieder auf, statt beim Boot die ganze Historie zu re-scannen.
            options.Schema.For<PollCursor>().Identity(x => x.Id);

            // Emittenten-Cursor (P4): best-effort Fortschritt eines EMITTIERENDEN Konsumenten
            // (Reaktion / Pipeline-Event) pro Partition — kein Co-Commit, Verlust heilt der Voll-Fold.
            options.Schema.For<EmittentenCursorDoc>().Identity(x => x.Id);

            // Durabler Offen-Index der Prozesse (§3-Backstop): eine kleine O(offen)-Menge, aus der ein
            // periodischer Scan hängende Prozesse (verlorene terminale/kompensierende Selbst-Weckung) heilt.
            options.Schema.For<ProzessOffen>().Identity(x => x.Id);

            // Dead-Letter-Queue (§5): nicht zustellbare ausgehende Pipeline-Commands, beobachtbar + replay-bar.
            options.Schema.For<DeadLetter>().Identity(x => x.Id).DatabaseSchemaName("dlq");

            // ★ Snapshots (docs/snapshot-konzept.md): ein jsonb-Dokument je Aggregat-Typ
            //   (Snapshot<Konto> → es.mt_doc_snapshot_konto). Registrierung generiert, reflection-frei.
            Persistence.RegisteredSnapshotTypes.Register(options);
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

        // Durabler Poll-Cursor (Backstop): setzt nach Neustart bei der letzten HWM auf.
        services.AddSingleton<IPollCursorStore>(provider =>
            new MartenPollCursorStore(provider.GetRequiredService<IDocumentStore>()));

        // Emittenten-Cursor (P4, Achse B = emittierend): best-effort Fortschritt für Reaktion/Pipeline-Event,
        // damit ein emittierender Konsument nicht bei jeder Weckung ab 0 re-faltet (O(N²)→O(Tail)); die
        // Korrektheit trägt weiter die Empfänger-Inbox, nicht dieser Cursor (kein Reset → kein blind-Replay).
        services.AddSingleton<IEmittentenCursor>(provider =>
            new MartenEmittentenCursor(provider.GetRequiredService<IDocumentStore>()));

        // Durabler Offen-Index der Prozesse (§3-Backstop): Grundlage des Scans, der hängende Prozesse heilt.
        services.AddSingleton<IProzessOffenIndex>(provider =>
            new MartenProzessOffenIndex(provider.GetRequiredService<IDocumentStore>()));

        // Dead-Letter-Senke (§5): nicht zustellbare Pipeline-Commands beobachtbar machen statt still droppen.
        services.AddSingleton<IDeadLetterSink>(provider =>
            new MartenDeadLetterSink(
                provider.GetRequiredService<IDocumentStore>(),
                provider.GetRequiredService<ILogger<MartenDeadLetterSink>>()));

        // DLQ-Ops-/Read-Pfad (Feature-Strom): tote Commands auflisten/je Korrelation abfragen/auflösen.
        services.AddSingleton<IDeadLetterReadStore>(provider =>
            new MartenDeadLetterReadStore(provider.GetRequiredService<IDocumentStore>()));

        // ★ Snapshot-Store (abgeleiteter jsonb-Cache): der Aggregat-Actor seedet daraus seine Rehydration.
        services.AddSingleton<ISnapshotStore>(provider =>
            new MartenSnapshotStore(
                provider.GetRequiredService<IDocumentStore>(),
                provider.GetService<ILogger<MartenSnapshotStore>>()));

        // ★ Snapshot-Schwellwert in DI → der generierte Actor injiziert ihn (Default 200, Tests klein).
        services.AddSingleton(new SnapshotOptions(builder.SnapshotThreshold, builder.InboxCap));
        
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
    /// Deps-Index-Naht registrieren.
    ///
    /// (B1) Die Push-Subscriber-Maschinerie (SubscriberActorBase-Dispatch + generierte Push-Actors +
    /// SubscriberStartupService) wurde entfernt — alle durablen Handler laufen auf dem Pull-Pfad
    /// (AddGeneratedPullPaths). Der Broker-Kanal bleibt (Signal-Zustellung + Re-Publish reaktiver Events).
    /// Der Deps-Index-Writer bleibt ebenfalls: der Pull-Adapter nutzt ihn als <see cref="Core.IReadModelDepsSink"/>.
    /// </summary>
    public static IServiceCollection AddCqrsSubscribers(this IServiceCollection services)
    {
        Console.WriteLine("[CQRS] Registriere Deps-Index-Naht...");
        services.AddSingleton<Infrastructure.Projections.ReadModelDepsWriter>();
        services.AddSingleton<Core.IReadModelDepsSink>(
            sp => sp.GetRequiredService<Infrastructure.Projections.ReadModelDepsWriter>());
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
                clientConfiguration: c => c.Address = new Uri(
                    builder.ConsulAddress.StartsWith("http") 
                        ? builder.ConsulAddress 
                        : $"http://{builder.ConsulAddress}"));
            
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

            // ★ Phase 4a: per-Stream-Adapter-Kinds der auf Pull migrierten Projektionen
            //   (VOR StartMemberAsync, sonst nicht registrierbar).
            var kindContributors = provider
                .GetServices<Infrastructure.Projections.IClusterKindContributor>().ToList();
            foreach (var contributor in kindContributors)
            {
                clusterConfig = clusterConfig.WithClusterKind(contributor.CreateKind(system, provider));
            }
            Console.WriteLine($"  + {kindContributors.Count} Adapter-Kinds registriert");

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
            var logger = provider.GetService<ILogger<ProtoActorAggregateDispatcher>>();
            var deadLetters = provider.GetService<IDeadLetterSink>();   // ★ #1: nicht-zustellbare Commands durabel
            return new ProtoActorAggregateDispatcher(system, logger, deadLetters);
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

        // (B1) SubscriberStartupService entfernt — keine Push-Subscriber mehr; alle Handler laufen
        //   über den Pull-Pfad (GenericPullStartupService via AddGeneratedPullPaths).

        services.AddHostedService<Infrastructure.Pipeline.PipelineStartupService>();
        Console.WriteLine("  + PipelineStartupService");

        // Trigger-Ingress (Push): spawnt alle im DI registrierten ITriggerRegistration-Instanzen
        //   (FileWatcher, Timer, …). Nach den Pipelines, weil Trigger sofort Messages senden.
        //   Ohne Registrierungen ein No-op — daher unbedenklich immer registriert.
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

    /// <summary>
    /// ★ Audit-Fix #2: EXPLIZITER, konfigurierbarer Command-Timeout (Sekunden) für alle Marten-/Npgsql-Befehle.
    /// Bisher galt still Npgsqls Default (~30s); ein DB-Stall blockierte den Actor-Turn unkontrolliert lange.
    /// Jetzt app-seitig gesetzt und tunebar. BEWUSST NICHT als per-Call-CancellationToken um den Append gelöst:
    /// ein abgebrochener SaveChanges ist mehrdeutig (committet?), auf dem OCC-Pfad ergäbe das ein falsches
    /// CommandFailed + ein transientes Konflikt-Fenster. Ein Store-weiter Timeout ist die gefahrlose Grenze;
    /// die Aufrufer-Seite ist zusätzlich über den Dispatcher-Dead-Letter (#1) abgedeckt.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    public string RedisConnectionString { get; set; } = "localhost:6379";
    public int RedisDatabase { get; set; } = 1;

    /// <summary>
    /// Snapshot-Schwellwert (docs/snapshot-konzept.md): der Aggregat-Actor schreibt best-effort nach je so
    /// vielen Events einen Snapshot. Default 200 — hoch genug, dass kurze Aggregate nie snapshotten. Tests
    /// setzen ihn klein, um den Snapshot-Pfad billig auszulösen. 0 schaltet das Schwellwert-Schreiben ab.
    /// </summary>
    public int SnapshotThreshold { get; set; } = 200;

    /// <summary>
    /// ★ Audit-Fix #4/H3: harte Obergrenze der Framework-Inbox (verarbeitete CommandIds des idempotenten
    /// Reaktions-/Prozess-Pfads). Nur die letzten so vielen Ids bleiben in Speicher + Snapshot, statt monoton
    /// zu wachsen. Default 10 000 — weit über dem kurzen Re-Delivery-Fenster (Poll ~30s / Prozess feuert einen
    /// Vorgang nur bis zur ersten Marke), also praktisch dedup-sicher; höher = sicherer, größerer Snapshot.
    /// </summary>
    public int InboxCap { get; set; } = 10_000;
    
    public CqrsFrameworkBuilder(IServiceCollection services)
    {
        Services = services;
    }
}