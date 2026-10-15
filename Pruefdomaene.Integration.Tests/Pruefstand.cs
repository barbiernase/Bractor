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

    /// <summary>
    /// Ein In-Process-Host mit der Verdrahtung von Host.Grpc (ohne gRPC): Framework, Pull-Pfade, Prozesse. <paramref name="inboxCache"/> ist
    /// der Inbox-Cache des Actors (klein = der Weg über den Schlüssel der Inbox-Tabelle); <paramref name="dienste"/> greift nach der Verdrahtung ein (Störungs-Seams der Garantie-Tests).
    /// </summary>
    public static IHost Cluster(string name, int snapshotSchwelle = 200, int inboxCache = SnapshotOptions.StandardInboxCache, Action<IServiceCollection>? dienste = null,
        string schema = "pruef_e2e")
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = false;
            opts.ClusterName = name + "-" + Guid.NewGuid().ToString("N")[..8];
            opts.EventStoreConnectionString = Verbindung;
            opts.EventStoreSchema = schema;
            opts.RedisDatabase = 2;
            opts.SnapshotThreshold = snapshotSchwelle;
            opts.InboxCache = inboxCache;
        });
        GeneratedPipelines.RegisterAllPipelines(builder.Services);
        builder.Services.AddGeneratedPullPaths();
        builder.Services.AddGeneratedProzesse();
        dienste?.Invoke(builder.Services);
        return builder.Build();
    }

    /// <summary>Ersetzt die Registrierung von <typeparamref name="T"/> durch einen Dekorator um die bisherige (Seam für Störungen).</summary>
    public static void Dekoriere<T>(IServiceCollection s, Func<IServiceProvider, T, T> dekorator) where T : class
    {
        var alt = s.Last(d => d.ServiceType == typeof(T));
        s.Remove(alt);
        Func<IServiceProvider, T> innen = alt.ImplementationFactory is { } f ? sp => (T)f(sp)
            : alt.ImplementationInstance is T i ? _ => i
            : throw new NotSupportedException($"{typeof(T).Name} ist per Typ registriert — dekorierbar sind Fabrik und Instanz (keine Reflection).");
        s.Add(new ServiceDescriptor(typeof(T), sp => dekorator(sp, innen(sp)), alt.Lifetime));
    }

    /// <summary>
    /// Schickt einen Command wie ein Emittent (Modus <c>Emittiert</c>, vorgegebene CommandId) und wartet auf das Ergebnis des Aggregats —
    /// derselbe Weg wie der JSON-Eingang (<c>ProtoActorAggregateDispatcher.FrageAsync</c>).
    /// </summary>
    public static Task<CommandResult?> FrageAsync(IHost host, ICommand cmd, Guid commandId)
        => Infrastructure.Aggregate.ActorSystem.ProtoActorAggregateDispatcher.FrageAsync(host.Services.GetRequiredService<Proto.ActorSystem>(),
            new CommandEnvelope
            {
                CommandId = commandId, AggregateId = cmd.AggregateId, AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(cmd),
                Payload = cmd, Modus = new CommandModus.Emittiert(), CorrelationId = Guid.NewGuid().ToString(),
            }, TimeSpan.FromSeconds(30), default);

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
