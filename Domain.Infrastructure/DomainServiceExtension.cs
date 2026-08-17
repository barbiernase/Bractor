using Domain.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Domain.Infrastructure;

/// <summary>
/// DI-Registrierung fuer alle Domain-Projektions-Komponenten.
///
/// Registriert:
///   1. ReadModel-Stores (InMemory oder PostgreSQL)
///   2. Reader (bekommen ReadStore via DI)
///
/// NICHT registriert (kommt von anderswo):
///   - Projektionen/Writer → GeneratedSubscribers (generiert)
///   - ProjectionQueryService → AddCqrsQueryService (generiert)
///   - ReadModelDepsReader → Infrastructure (Redis-Abhängigkeit)
/// </summary>
public static class DomainServiceExtensions
{
    public static IServiceCollection AddDomainProjectionServices(
        this IServiceCollection services)
    {
        Console.WriteLine("[Domain] Registriere Projection-Services...");

        // ═══════════════════════════════════════════════════════
        // ImagePair — PostgreSQL (Marten Document Store)
        //
        // Nutzt dieselbe IDocumentStore-Instanz wie der EventStore,
        // aber mit eigenem Schema "rm" (read models).
        // Nach Server-Neustart sind alle ReadModel-Daten sofort da.
        // ═══════════════════════════════════════════════════════

        services.ConfigureMarten(options =>
        {
            options.Schema.For<ImagePairReadModel>()
                .DatabaseSchemaName("rm")
                .Identity(x => x.Id)
                .UseOptimisticConcurrency(false);

            options.Schema.For<ImagePairHistorieReadModel>()
                .DatabaseSchemaName("rm")
                .Identity(x => x.Id)
                .UseOptimisticConcurrency(false);

            // ── Datensatz (Trainings-/Datensatz-Kontext) ──
            options.Schema.For<DatensatzReadModel>()
                .DatabaseSchemaName("rm")
                .Identity(x => x.Id)
                .UseOptimisticConcurrency(false);

            options.Schema.For<DatensatzSampleReadModel>()
                .DatabaseSchemaName("rm")
                .Identity(x => x.Id)
                .UseOptimisticConcurrency(false);

            // ── Trainingslauf ──
            options.Schema.For<TrainingslaufReadModel>()
                .DatabaseSchemaName("rm")
                .Identity(x => x.Id)
                .UseOptimisticConcurrency(false);
        });

        // Read-Seite: unverändert Singleton Postgres (eigene Query-Sessions).
        services.AddSingleton<ImagePairStorePostgres>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var logger = provider.GetRequiredService<ILogger<ImagePairStorePostgres>>();
            return new ImagePairStorePostgres(store, logger);
        });
        services.AddSingleton<IImagePairReadStore>(
            sp => sp.GetRequiredService<ImagePairStorePostgres>());
        Console.WriteLine("  + IImagePairReadStore (PostgreSQL/Marten, Singleton)");

        // Write-Seite: Pull-Pfad Co-Commit-Store, TRANSIENT (frisch pro Stream-Actor → isolierter Puffer).
        // Zugleich IProjectionTracker (Effekt + Marke in EINEM SaveChanges → exactly-once).
        services.AddTransient<ImagePairStore>(provider =>
            new ImagePairStore(provider.GetRequiredService<IDocumentStore>()));
        services.AddTransient<IImagePairWriteStore>(
            sp => sp.GetRequiredService<ImagePairStore>());
        Console.WriteLine("  + IImagePairWriteStore (Co-Commit, Transient)");
        Console.WriteLine("    Schema: rm");

        services.AddSingleton<ImagePairReader>();
        Console.WriteLine("  + ImagePairReader");

        // ═══════════════════════════════════════════════════════
        // ImagePair-Historie — PostgreSQL (Marten Document Store)
        //
        // Eigenständige Projektion: materialisiert die Event-Timeline
        // pro ImagePair als append-only Liste. Selbes Schema "rm".
        // ═══════════════════════════════════════════════════════

        // Pull-Pfad: Co-Commit-Store, TRANSIENT (frisch pro Stream-Actor → isolierter Puffer).
        // Er ist zugleich IProjectionTracker (Effekt + Marke in EINEM SaveChanges → exactly-once).
        services.AddTransient<ImagePairHistorieStore>(provider =>
            new ImagePairHistorieStore(provider.GetRequiredService<IDocumentStore>()));
        services.AddTransient<IImagePairHistorieWriteStore>(
            sp => sp.GetRequiredService<ImagePairHistorieStore>());
        services.AddTransient<IImagePairHistorieReadStore>(
            sp => sp.GetRequiredService<ImagePairHistorieStore>());
        Console.WriteLine("  + IImagePairHistorieWriteStore / IImagePairHistorieReadStore (Co-Commit, Transient)");

        services.AddSingleton<ImagePairHistorieReader>();
        Console.WriteLine("  + ImagePairHistorieReader");

        // ═══════════════════════════════════════════════════════
        // Datensatz — PostgreSQL (Marten Document Store, Schema "rm")
        //
        // Read-Seite: Singleton Postgres (eigene Query-Sessions).
        // Write-Seite: Co-Commit-Store, TRANSIENT (frisch pro Stream-Actor → isolierter
        // Puffer). Zugleich IProjectionTracker/ICoCommitTracker (Effekt + Marke in EINEM
        // SaveChanges → exactly-once; nötig für die IAppendProjektion beim Einfrieren).
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<DatensatzStorePostgres>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var logger = provider.GetRequiredService<ILogger<DatensatzStorePostgres>>();
            return new DatensatzStorePostgres(store, logger);
        });
        services.AddSingleton<IDatensatzReadStore>(
            sp => sp.GetRequiredService<DatensatzStorePostgres>());
        Console.WriteLine("  + IDatensatzReadStore (PostgreSQL/Marten, Singleton)");

        services.AddTransient<DatensatzStore>(provider =>
            new DatensatzStore(provider.GetRequiredService<IDocumentStore>()));
        services.AddTransient<IDatensatzWriteStore>(
            sp => sp.GetRequiredService<DatensatzStore>());
        Console.WriteLine("  + IDatensatzWriteStore (Co-Commit, Transient)");

        services.AddSingleton<DatensatzReader>();
        Console.WriteLine("  + DatensatzReader");

        // ═══════════════════════════════════════════════════════
        // Trainingslauf — PostgreSQL (Marten, Schema "rm")
        // Read-Singleton + Co-Commit-Write-Transient (IAppendProjektion: MetrikHistorie wächst).
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<TrainingslaufStorePostgres>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var logger = provider.GetRequiredService<ILogger<TrainingslaufStorePostgres>>();
            return new TrainingslaufStorePostgres(store, logger);
        });
        services.AddSingleton<ITrainingslaufReadStore>(
            sp => sp.GetRequiredService<TrainingslaufStorePostgres>());
        Console.WriteLine("  + ITrainingslaufReadStore (PostgreSQL/Marten, Singleton)");

        services.AddTransient<TrainingslaufStore>(provider =>
            new TrainingslaufStore(provider.GetRequiredService<IDocumentStore>()));
        services.AddTransient<ITrainingslaufWriteStore>(
            sp => sp.GetRequiredService<TrainingslaufStore>());
        Console.WriteLine("  + ITrainingslaufWriteStore (Co-Commit, Transient)");

        services.AddSingleton<TrainingslaufReader>();
        Console.WriteLine("  + TrainingslaufReader");

        // ═══════════════════════════════════════════════════════
        // ProjectionQueryService (generiert)
        // ═══════════════════════════════════════════════════════

        // Projektions-Logik-Instanzen: der ProjectionQueryService braucht sie für ihre SubscriberId
        // (Query → SubscriberId-Mapping). Früher registriert vom (mit B1 entfernten) Push-Generator;
        // hier domänenseitig, wo auch Reader/Stores/QueryService registriert sind.
        services.AddSingleton<ImagePairProjection>();
        services.AddSingleton<ImagePairHistorieProjection>();
        services.AddSingleton<DatensatzProjektion>();
        services.AddSingleton<TrainingslaufProjektion>();

        services.AddSingleton<ProjectionQueryService>();
        Console.WriteLine("  + ProjectionQueryService (generiert)");

        Console.WriteLine();
        return services;
    }
}