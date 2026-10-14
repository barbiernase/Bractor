using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Extensions;
using Infrastructure.Persistence;
using Infrastructure.Pipeline;
using Infrastructure.Projections.Generated;
using Infrastructure.Prozess;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Gemeinsames Werkzeug der Prüfdomänen-Tests. Die Prüfdomäne schreibt in EIGENE Schemata (nie <c>es</c>): ihre Prozess-Backstops und
/// Poller sollen die Events der Beispielanwendung in derselben Datenbank nie sehen — und umgekehrt.
/// </summary>
internal static class Pruefstand
{
    public const string Verbindung = "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";

    /// <summary>Ein In-Process-Host mit der Verdrahtung von Host.Grpc (ohne gRPC): Framework, Pull-Pfade, Prozesse.</summary>
    public static IHost Cluster(string name, int snapshotSchwelle = 200)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = name + "-" + Guid.NewGuid().ToString("N")[..8];
            opts.EventStoreConnectionString = Verbindung;
            opts.EventStoreSchema = "pruef_e2e";
            opts.RedisDatabase = 2;
            opts.SnapshotThreshold = snapshotSchwelle;
        });
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        builder.Services.AddGeneratedProzesse();
        return builder.Build();
    }

    /// <summary>Ein Marten-Store auf eigenem Schema mit der Produktions-Registrierung (Events samt Upcasting, Snapshots, Metadaten).</summary>
    public static IDocumentStore Store(string schema) => DocumentStore.For(opts =>
    {
        opts.Connection(Verbindung);
        opts.DatabaseSchemaName = schema;
        opts.Events.DatabaseSchemaName = schema;
        opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
        opts.Events.MetadataConfig.CorrelationIdEnabled = true;
        opts.Events.MetadataConfig.CausationIdEnabled = true;
        opts.Events.MetadataConfig.HeadersEnabled = true;
        MartenEventTypeRegistration.RegisterEventTypes(opts);
        RegisteredSnapshotTypes.Register(opts);   // die GENERIERTE Registrierung der Komposition
    });

    /// <summary>Der Zustand eines Aggregats aus dem Log (Voll-Fold, ohne Snapshot) — die Wahrheit.</summary>
    public static async Task<T> Zustand<T>(IServiceProvider sp, Guid id) where T : class, IState, new()
        => (await AggregateRehydrator.LoadAsync<T>(id, null, sp.GetRequiredService<IEventStoreRepository>(),
            sp.GetRequiredService<IAggregateHandlerFactory>(), null, default)).State;

    /// <summary>Ist der Vorgang <paramref name="korrelation"/> beendet (Manager-Log), mit dem erwarteten Ausgang?</summary>
    public static async Task<bool> Beendet(IEventStoreRepository store, Guid korrelation, bool erfolg)
        => (await store.ReadStreamAsync(korrelation, 0, default)).Any(e => e.Payload is ProzessBeendet b && b.Erfolg == erfolg);

    public static async Task Warte(Func<Task<bool>> bedingung, TimeSpan frist, string meldung)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            try { if (await bedingung()) return; } catch { /* Stream noch leer / Cluster bootet */ }
            await Task.Delay(200);
        }
        throw new TimeoutException(meldung);
    }
}
