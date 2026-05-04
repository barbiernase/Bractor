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
        // Lagerbestand
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<LagerbestandStoreInMemory>();
        services.AddSingleton<ILagerbestandWriteStore>(
            sp => sp.GetRequiredService<LagerbestandStoreInMemory>());
        services.AddSingleton<ILagerbestandReadStore>(
            sp => sp.GetRequiredService<LagerbestandStoreInMemory>());
        Console.WriteLine("  + ILagerbestandWriteStore / ILagerbestandReadStore (InMemory)");

        services.AddSingleton<LagerbestandReader>();
        Console.WriteLine("  + LagerbestandReader");

        // ═══════════════════════════════════════════════════════
        // AuditLog
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<InMemoryAuditLogStore>();
        services.AddSingleton<IAuditLogWriteStore>(
            sp => sp.GetRequiredService<InMemoryAuditLogStore>());
        services.AddSingleton<IAuditLogReadStore>(
            sp => sp.GetRequiredService<InMemoryAuditLogStore>());
        Console.WriteLine("  + IAuditLogWriteStore / IAuditLogReadStore (InMemory)");

        services.AddSingleton<AuditLogReader>();
        Console.WriteLine("  + AuditLogReader");

        // ═══════════════════════════════════════════════════════
        // Todo
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<TodoStoreInMemory>();
        services.AddSingleton<ITodoWriteStore>(
            sp => sp.GetRequiredService<TodoStoreInMemory>());
        services.AddSingleton<ITodoReadStore>(
            sp => sp.GetRequiredService<TodoStoreInMemory>());
        Console.WriteLine("  + ITodoWriteStore / ITodoReadStore (InMemory)");

        services.AddSingleton<TodoReader>();
        Console.WriteLine("  + TodoReader");

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
        });

        services.AddSingleton<ImagePairStorePostgres>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var logger = provider.GetRequiredService<ILogger<ImagePairStorePostgres>>();
            return new ImagePairStorePostgres(store, logger);
        });
        services.AddSingleton<IImagePairWriteStore>(
            sp => sp.GetRequiredService<ImagePairStorePostgres>());
        services.AddSingleton<IImagePairReadStore>(
            sp => sp.GetRequiredService<ImagePairStorePostgres>());
        Console.WriteLine("  + IImagePairWriteStore / IImagePairReadStore (PostgreSQL/Marten)");
        Console.WriteLine("    Schema: rm");

        services.AddSingleton<ImagePairReader>();
        Console.WriteLine("  + ImagePairReader");

        // ═══════════════════════════════════════════════════════
        // ImagePair-Historie — PostgreSQL (Marten Document Store)
        //
        // Eigenständige Projektion: materialisiert die Event-Timeline
        // pro ImagePair als append-only Liste. Selbes Schema "rm".
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<ImagePairHistorieStorePostgres>(provider =>
        {
            var store = provider.GetRequiredService<IDocumentStore>();
            var logger = provider.GetRequiredService<ILogger<ImagePairHistorieStorePostgres>>();
            return new ImagePairHistorieStorePostgres(store, logger);
        });
        services.AddSingleton<IImagePairHistorieWriteStore>(
            sp => sp.GetRequiredService<ImagePairHistorieStorePostgres>());
        services.AddSingleton<IImagePairHistorieReadStore>(
            sp => sp.GetRequiredService<ImagePairHistorieStorePostgres>());
        Console.WriteLine("  + IImagePairHistorieWriteStore / IImagePairHistorieReadStore (PostgreSQL/Marten)");

        services.AddSingleton<ImagePairHistorieReader>();
        Console.WriteLine("  + ImagePairHistorieReader");

        // ═══════════════════════════════════════════════════════
        // ProjectionQueryService (generiert)
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<ProjectionQueryService>();
        Console.WriteLine("  + ProjectionQueryService (generiert)");

        Console.WriteLine();
        return services;
    }
}