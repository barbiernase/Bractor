using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Extensions;
using Infrastructure.GrpcClient;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Client.Infrastructure.Reaktionen;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Gemeinsames Werkzeug der Prüfdomänen-Tests. Die Prüfdomäne schreibt in EIGENE Schemata (nie <c>es</c>): ihre Prozess-Backstops und
/// Poller sollen die Events der Beispielanwendung in derselben Datenbank nie sehen — und umgekehrt.
/// </summary>
internal static class Pruefstand
{
    public const string Verbindung = "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";

    /// <summary>
    /// Ein In-Process-Host mit der Verdrahtung von Host.Grpc: Framework, Pull-Pfade, Graphen, die Leitung (gRPC auf einem freien Port) und — wie im
    /// Betrieb neben dem Host — der Client, der auf die Fakten reagiert (<see cref="Pruefdomaene.Client.Worker.Reaktionslauf"/>, Schritt 6: ein
    /// Decide gibt nur Fakten aus, die Köpfe der Graphen speist er). <paramref name="mitReaktionen"/> = false lässt ihn weg. <paramref name="inboxCache"/> ist
    /// der Inbox-Cache des Actors (klein = der Weg über den Schlüssel der Inbox-Tabelle); <paramref name="dienste"/> greift nach der Verdrahtung ein (Störungs-Seams der Garantie-Tests).
    /// <paramref name="takt"/>: der Takt je Partition — kurz (<see cref="KurzerTakt"/>), damit Tests, die auf ihn warten (Nachholen, Position über
    /// fremde Events), nicht 30 s stehen; ein Latenz-Test des Weckers verlangt den Standard (<see cref="Konsum.StandardTakt"/>), sonst bestünde er
    /// auch ohne Wecker.
    /// </summary>
    public static IHost Cluster(string name, int snapshotSchwelle = 200, int inboxCache = SnapshotOptions.StandardInboxCache, Action<IServiceCollection>? dienste = null,
        string schema = "pruef_e2e", TimeSpan? takt = null, string? rechner = null, string? clusterName = null, bool mitReaktionen = true,
        int? port = null)
    {
        var builder = WebApplication.CreateBuilder();
        port ??= FreierPort();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port.Value, l => l.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddCqrsFramework(opts =>
        {
            opts.EnableGrpc = true;
            opts.ClusterName = clusterName ?? name + "-" + Guid.NewGuid().ToString("N")[..8];
            opts.Rechner = rechner;
            opts.EventStoreConnectionString = Verbindung;
            opts.EventStoreSchema = schema;
            opts.SnapshotThreshold = snapshotSchwelle;
            opts.InboxCache = inboxCache;
            opts.Konsum = Konsum.Standard with { Takt = takt ?? KurzerTakt };
        });
        builder.Services.AddGeneratedPullPaths();
        // Je Graph eine Instanz, die Technik wie im Host ohne Wahl: je eine die eine Umsetzung (Tests ersetzen sie über dienste).
        global::Infrastructure.Funktionen.TechnikExtensions.BindeTechnik(builder.Services,
            global::Infrastructure.Funktionen.GraphInstanzen.JeGraphEine(global::Infrastructure.Komposition.Anwendung.Funktionen.Graphen));
        if (mitReaktionen)
        {
            var stand = Reaktionsstand(schema);
            builder.Services.AddHostedService(sp => new Reaktionsdienst(sp.GetRequiredService<IHostApplicationLifetime>(), $"http://127.0.0.1:{port}", stand));
        }
        dienste?.Invoke(builder.Services);
        var app = builder.Build();
        app.MapCqrsGrpcService();
        return app;
    }

    private static readonly ConcurrentDictionary<string, ReaktionsStandImSpeicher> Staende = new();

    /// <summary>Ein eigener Stand der Reaktionen am Ende des Logs von <paramref name="schema"/> — für einen Client, den ein Test selbst laufen lässt.</summary>
    public static ReaktionsStandImSpeicher NeuerReaktionsstand(string schema) => AmEnde(schema);

    /// <summary>
    /// Der Stand der Reaktionen je Schema über alle Hosts dieses Prozesses — ein Host nach einem Absturz macht weiter, wo der vorige stand, wie ein
    /// Worker, der weiterläuft. Beim ersten Host eines Schemas beginnt er am Ende des Logs: was frühere Läufe dort schrieben, ist erledigt.
    /// </summary>
    private static ReaktionsStandImSpeicher Reaktionsstand(string schema) => Staende.GetOrAdd(schema, AmEnde);

    private static ReaktionsStandImSpeicher AmEnde(string schema)
    {
        var stand = new ReaktionsStandImSpeicher();
        using var conn = new NpgsqlConnection(Verbindung);
        conn.Open();
        using var cmd = new NpgsqlCommand($"select coalesce(max(seq_id), 0) from \"{schema}\".mt_events", conn);
        long ende;
        try { ende = Convert.ToInt64(cmd.ExecuteScalar()); }
        catch (PostgresException e) when (e.SqlState == "42P01") { ende = 0; }   // das Schema entsteht erst
        stand.SichereAsync(Reaktionsname, ende, default).GetAwaiter().GetResult();
        return stand;
    }

    public const string Reaktionsname = "pruefstand";

    /// <summary>
    /// Der reagierende Client neben dem Host: verbindet sich, sobald der Host läuft, und endet, sobald er zu stoppen beginnt — vor dem Server, der
    /// sonst auf den offenen Stream des Clients wartete (bis zu 30 s).
    /// </summary>
    private sealed class Reaktionsdienst(IHostApplicationLifetime leben, string adresse, IReaktionsStand stand) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var gestartet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (leben.ApplicationStarted.Register(() => gestartet.TrySetResult()))
            using (ct.Register(() => gestartet.TrySetCanceled()))
            {
                try { await gestartet.Task; } catch (OperationCanceledException) { return; }
            }
            using var ende = CancellationTokenSource.CreateLinkedTokenSource(ct, leben.ApplicationStopping);
            await Pruefdomaene.Client.Worker.Reaktionslauf.LaufeAsync(adresse, null, stand, Reaktionsname, ende.Token, TimeSpan.FromMilliseconds(200));
        }
    }

    public static int FreierPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>Der Takt der Prüfdomänen-Tests, wenn ein Test nichts anderes verlangt.</summary>
    public static readonly TimeSpan KurzerTakt = TimeSpan.FromSeconds(2);

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
    /// derselbe Weg wie der JSON-Eingang (<c>ProtoActorAggregateDispatcher.FrageAsync</c>). Mit <paramref name="version"/> wie ein Client mit
    /// Versionsprüfung (Modus <c>Client</c>, OCC gegen diese Version).
    /// </summary>
    public static Task<CommandResult?> FrageAsync(IHost host, IAggregatCommand cmd, Guid commandId, int? version = null)
        => global::Infrastructure.Aggregate.ActorSystem.ProtoActorAggregateDispatcher.FrageAsync(host.Services.GetRequiredService<Proto.ActorSystem>(),
            new CommandEnvelope
            {
                CommandId = commandId, AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(cmd),
                Payload = cmd, Modus = version is int v ? new CommandModus.Client(v) : new CommandModus.Emittiert(), CorrelationId = Guid.NewGuid().ToString(),
            }, TimeSpan.FromSeconds(30), default);

    /// <summary>Ein Marten-Store auf eigenem Schema mit der Produktions-Registrierung (Events samt Upcasting, Snapshots, Metadaten).</summary>
    public static IDocumentStore Store(string schema) => DocumentStore.For(opts =>
    {
        opts.Connection(Verbindung);
        opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;   // wie der Host; der Horizont setzt ihn voraus
        opts.DatabaseSchemaName = schema;
        opts.Events.DatabaseSchemaName = schema;
        opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
        opts.Events.MetadataConfig.CorrelationIdEnabled = true;
        opts.Events.MetadataConfig.CausationIdEnabled = true;
        opts.Events.MetadataConfig.HeadersEnabled = true;
        MartenEventTypeRegistration.RegisterEventTypes(opts);
        RegisteredSnapshotTypes.Register(opts);   // die GENERIERTE Registrierung der Komposition
        IdSerialisierung.Registriere(opts);       // typisierte Ids als UUID-String, wie der Host schreibt
    });

    /// <summary>Der Zustand eines Aggregats aus dem Log (Voll-Fold, ohne Snapshot) — die Wahrheit.</summary>
    public static async Task<T> Zustand<T>(IServiceProvider sp, Guid id) where T : class, IState, new()
        => (await AggregateRehydrator.LoadAsync<T>(id, null, sp.GetRequiredService<IEventStoreRepository>(),
            sp.GetRequiredService<IAggregateHandlerFactory>(), null, default)).State;

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
