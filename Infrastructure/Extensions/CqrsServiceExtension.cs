using Abstractions;
using Domain.Infrastructure;
using Infrastructure.Aggregate.ActorSystem;
using Infrastructure.GrpcClient;
using Infrastructure.Persistence;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.PubSub;
using Infrastructure.Serialization;
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

            // ★ OPT-IN (Messung): STJ-Source-Gen-Kontext für Events in Martens Serializer einstecken.
            //   Der Source-Gen-Resolver liefert reflection-freie JsonTypeInfo für alle persistierbaren Events;
            //   alles andere (Dokumente: Checkpoints/Cursor/Snapshots/…) fällt auf den Reflection-Resolver
            //   zurück (Metadata-Mode-Kontext ist chain-kompatibel). Format-neutral bei STJ-Default (Marten 8).
            if (builder.UseGeneratedJsonSerializer)
            {
                // Source-Gen ZUERST (Events → reflection-freie JsonTypeInfo), danach der Reflection-Fallback
                // für ALLES andere: Dokumente (Checkpoints/Cursor/Snapshots/…) UND Martens Event-Header
                // (Dictionary<string,object>). Ohne den Fallback wirft jeder Append auf den Headers.
                options.UseSystemTextJsonForSerialization(configure: o =>
                    o.TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                        Serialization.EventJsonSerializerContext.Default,
                        new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()));
                Console.WriteLine("  + STJ Source-Gen Serializer für Events aktiv (UseGeneratedJsonSerializer)");
            }

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

            // Durabler Fristplan (Feature-Strom, Deadlines): eine Frist je offener Deadline; der
            // DB-Uhr-getriebene Scheduler feuert die fälligen und entfernt sie danach.
            options.Schema.For<Frist>().Identity(x => x.Id);

            // ★ Snapshots (docs/snapshot-konzept.md): ein jsonb-Dokument je Aggregat-Typ
            //   (Snapshot<Konto> → es.mt_doc_snapshot_konto). Registrierung generiert, reflection-frei.
            Persistence.RegisteredSnapshotTypes.Register(options);
        });
        // Anm. Multi-Node-Cold-Start: Marten legt die Snapshot-Tabelle eines Aggregat-Typs erst beim ERSTEN
        //   Zugriff LAZY an (ohne Advisory-Lock). Aktivieren mehrere Nodes gleichzeitig denselben, bisher
        //   ungenutzten Aggregat-Typ, können sie auf `CREATE TABLE mt_doc_snapshot_<typ>` rennen (transient,
        //   selbstheilend über Proto-Reaktivierung; Geld bleibt erhalten). `ApplyAllDatabaseChangesOnStartup`
        //   löst den Lazy-Race, führt aber Migrations-Lock-Contention beim gleichzeitigen Start EIN (Verlierer
        //   crasht) → der saubere Fix ist ein EINZELNER Migrator-Job (AutoCreate.None auf den übrigen Nodes),
        //   siehe docs/multi-node-deployment.md. Bewusst nicht hier verdrahtet (per-Node-Config nötig).

        services.AddSingleton<IEventStoreRepository>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var factory = provider.GetRequiredService<IAggregateHandlerFactory>();
            var logger = provider.GetRequiredService<ILogger<MartenEventStore>>();
            var marten = new MartenEventStore(store, factory, logger);

            if (!builder.AppendBatching)
                return marten;

            // Node-lokaler Group-Commit vor dem Marten-Store: bündelt die Aggregat-Appends dieses Nodes
            // in eine Transaktion. Lesezugriffe reicht der Dekorator unverändert durch; OCC/Single-Writer/
            // exactly-once bleiben, der Isolations-Retry fällt auf den Einzel-Append (marten) zurück.
            var writer = new MartenEventBatchWriter(
                store, provider.GetRequiredService<ILogger<MartenEventBatchWriter>>());
            return new BatchingEventAppender(
                marten, writer, provider.GetRequiredService<ILogger<BatchingEventAppender>>(),
                maxBatch: builder.AppendBatchMaxSize, lingerMs: builder.AppendBatchLingerMs,
                drainParallelism: builder.AppendDrainParallelism);
        });
        Console.WriteLine($"  + IEventStoreRepository (Marten/PostgreSQL)"
            + (builder.AppendBatching ? $" + Group-Commit-Batching (max {builder.AppendBatchMaxSize})" : ""));
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

        // DB-Uhr + durabler Fristplan (Feature-Strom, Deadlines): der Unterbau des Deadline-Schedulers.
        // Harmlos ohne Scheduler (kein aktiver Effekt) → immer registriert; der Scheduler ist opt-in (AddDeadlines).
        services.AddSingleton<IDbClock>(provider =>
            new MartenDbClock(provider.GetRequiredService<IDocumentStore>()));
        services.AddSingleton<IFristplan>(provider =>
            new MartenFristplan(provider.GetRequiredService<IDocumentStore>()));

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

        if (builder.EnableVersionTracking)
        {
            services.AddSingleton<IVersionTracker, RedisVersionTracker>();
            Console.WriteLine($"  + IVersionTracker (Redis)");
            Console.WriteLine($"    Endpoint: {builder.RedisConnectionString}, DB: {builder.RedisDatabase}");
        }
        else
        {
            // Messung/Redis-los: der Index ist aus (No-op). Der Actor-Track wird folgenlos, der eine
            // Leser (ReadModelDepsWriter) bekommt leere Ergebnisse — beides tolerierbar (nicht-autoritativ).
            services.AddSingleton<IVersionTracker, NullVersionTracker>();
            Console.WriteLine($"  + IVersionTracker (No-op — Version-Index AUS)");
        }

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
            return new BrokerPublisher(actorSystem.Cluster(),
                provider.GetService<ILogger<BrokerPublisher>>());
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
            // ★ MULTI-NODE: Wire-Serializer für den internen Actor-Plane registrieren (VOR WithRemote).
            //   Ohne diesen Serializer sind CommandEnvelope/CommandResult/Wake/… rohe CLR-Objekte
            //   → de facto single-node. Strikte Whitelist (GeneratedWire.CanSerialize) lässt PID &
            //   andere Protobuf-IMessage weiter beim Default-Serializer (id 0).
            var remoteConfig = GrpcNetRemoteConfig
                .BindTo("0.0.0.0")
                .WithAdvertisedHost(builder.AdvertisedHost)
                .WithSerializer(CqrsWireSerializer.SerializerId, CqrsWireSerializer.Priority, new CqrsWireSerializer());

            system
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            // ★ Boot-Check (Roadmap K1): fehlt einem internen Typ die Serializer-Abdeckung → Start-Abbruch,
            //   statt dass die Lücke erst cross-node zur Laufzeit still aufliegt.
            WireSerializerBootCheck.Verify();

            Console.WriteLine("  + ActorSystem erstellt (+ Wire-Serializer id=" + CqrsWireSerializer.SerializerId + ")");
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

    /// <summary>
    /// Node-lokaler Group-Commit für Aggregat-Appends (Batching): bündelt die Appends der auf diesem Node
    /// ansässigen Actors in EINE Postgres-Transaktion → amortisiert die gemessen dominante Postgres-CPU pro
    /// Command. Standardmäßig AN. OCC/Single-Writer/exactly-once bleiben unverändert (siehe
    /// <see cref="Persistence.BatchingEventAppender"/>). Abschalten stellt den Ein-Append-pro-Command-Pfad her.
    /// </summary>
    public bool AppendBatching { get; set; } = true;

    /// <summary>Maximale Anzahl Appends pro Group-Commit-Transaktion (Obergrenze der Batch-/Lock-Dauer).</summary>
    public int AppendBatchMaxSize { get; set; } = 256;

    /// <summary>
    /// Optionales Linger-Fenster (ms), um unter Last größere Batches zu formen (bounded Zusatz-Latenz).
    /// Default 0 = opportunistisch: bei leichter Last sofortiger Flush (Verhalten wie ohne Batching), unter
    /// Last coalesct die Queue von selbst, während der vorige Commit läuft.
    /// </summary>
    public int AppendBatchLingerMs { get; set; } = 0;

    /// <summary>
    /// Anzahl paralleler Commit-Drain-Loops im <see cref="Persistence.BatchingEventAppender"/>. Default 1
    /// (serieller Drain). Profil: der Schreibpfad ist commit-WAIT-gebunden bei idler CPU → K>1 parallele
    /// Commit-Streams heben den Durchsatz. SICHER durch Single-Activation (ein Stream liegt nie gleichzeitig
    /// in zwei Batches). Jeder Loop nutzt eine eigene Connection → K an der Npgsql-Pool-Grenze bedenken.
    /// Default 4: gemessenes Optimum auf 4-Kern-localhost (+48% ggü. seriell, Exactly-once verifiziert);
    /// pro Deployment tunebar (mehr Kerne / remote-Postgres mit höherer Latenz begünstigen höheres K).
    /// </summary>
    public int AppendDrainParallelism { get; set; } = 4;

    /// <summary>
    /// OPT-IN (Messung): steckt den STJ-Source-Gen-Kontext (<see cref="Serialization.EventJsonSerializerContext"/>)
    /// vorn in Martens System.Text.Json-Resolver-Chain → Events serialisieren/deserialisieren reflection-frei
    /// (schnelle JsonTypeInfo statt Laufzeit-Reflection), Dokumente fallen unverändert auf den Reflection-Resolver
    /// zurück. Format-neutral, solange Marten ohnehin STJ nutzt (Marten 8 Default). Zweck: messen, wie viel
    /// Serialisierung am Append-Durchsatz hängt (Amdahl-Input für die COPY-Entscheidung). Default AUS.
    /// </summary>
    public bool UseGeneratedJsonSerializer { get; set; } = false;

    /// <summary>
    /// Redis-Version-Index (nicht-autoritative Stale-Detection/Typ-Liste). Default AN. AUS = No-op-Tracker:
    /// isoliert für die Messung den synchronen Redis-Round-Trip aus dem Command-Turn — und erlaubt den Betrieb
    /// ganz ohne Redis (der Index ist ohnehin nicht die Wahrheit; sein einziger Leser verträgt Leere).
    /// </summary>
    public bool EnableVersionTracking { get; set; } = true;

    public CqrsFrameworkBuilder(IServiceCollection services)
    {
        Services = services;
    }
}