// ═══════════════════════════════════════════════════════════════════
// SERVER — ASP.NET + Kestrel + gRPC.
//
// Startet das komplette CQRS/ES-Backend:
//   - Marten/PostgreSQL (EventStore)
//   - Redis (VersionTracker + ReadModel-Deps)
//   - Proto.Actor Cluster (Consul)
//   - PubSub (Broker-Actors + Subscriber-Actors)
//   - Pipeline (ImageProcessing + FileWatcher)
//   - gRPC bidirektionaler Stream für Clients
//
// Konfiguration über CqrsFrameworkBuilder.
// Domain-Pipeline-Services (IClassifierService, Trigger) separat.
//
// ═══════════════════════════════════════════════════════════════════
// KONFIGURATION:
//   Alle Werte kommen aus appsettings.json / appsettings.Production.json
//   oder Umgebungsvariablen (ASPNETCORE_-Prefix).
//
//   Umgebungsvariablen überschreiben appsettings:
//     Grpc__Port=5001
//     ConnectionStrings__EventStore=Host=db;...
//     Redis__Endpoint=redis:6379
//     Consul__Address=consul:8500
//     Pipeline__WatchPath=/data/input
//     Cluster__AdvertisedHost=server1.local
// ═══════════════════════════════════════════════════════════════════

using Domain.Pipeline.Infrastructure;
using Infrastructure.Deadlines;
using Infrastructure.Extensions;
using Infrastructure.GrpcClient;
using Infrastructure.Monitoring;
using Infrastructure.Pipeline;
using Infrastructure.Projections;
using Infrastructure.Projections.Generated;
using Infrastructure.Prozess;

var builder = WebApplication.CreateBuilder(args);

// ─── Konfiguration lesen ───

var grpcPort = builder.Configuration.GetValue<int>("Grpc:Port", 5001);
var watchPath = builder.Configuration.GetValue<string>("Pipeline:WatchPath")
    ?? "/data/input";
var preprocessedPath = builder.Configuration.GetValue<string>("Pipeline:PreprocessedPath");

// ─── Marten-Schema-Rolle (Multi-Node-Cold-Start, docs/multi-node-deployment.md) ───
//   Cluster__Role = migrator | member | standalone (default).
//   migrator: legt ALLE Schema-Objekte (inkl. lazy Snapshot-Tabellen) eager + lock-gesichert an → exit 0.
//   member:   AutoCreate.None (kein Runtime-Lazy-Create → kein Cold-Start-Race; Migrator lief vorher).
//   (unset):  standalone = unverändertes Single-Node-Verhalten.
var clusterRole = (builder.Configuration.GetValue<string>("Cluster:Role") ?? "standalone")
    .Trim().ToLowerInvariant();
var schemaRole = clusterRole switch
{
    "migrator" => MartenSchemaRole.Migrator,
    "member" => MartenSchemaRole.Member,
    _ => MartenSchemaRole.Standalone,
};

// ─── Kestrel: HTTP/2 (gRPC braucht HTTP/2) ───
// FIX: ListenAnyIP statt ListenLocalhost — sonst ist der Server
// von anderen Hosts/Containern nicht erreichbar.

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// ─── gRPC ───

builder.Services.AddGrpc();

// ─── CQRS-Framework (verdrahtet Infrastruktur + Aggregates + Subscribers + PubSub) ───

builder.Services.AddCqrsFramework(opts =>
{
    // EventStore (Marten/PostgreSQL)
    opts.EventStoreConnectionString =
        builder.Configuration.GetConnectionString("EventStore")
        ?? "Host=localhost;Database=cqrs_events;Username=postgres;Password=postgres";
    opts.EventStoreSchema =
        builder.Configuration.GetValue<string>("EventStore:Schema") ?? "es";

    // VersionTracker (Redis)
    opts.RedisConnectionString =
        builder.Configuration.GetValue<string>("Redis:Endpoint") ?? "localhost:6379";
    opts.RedisDatabase =
        builder.Configuration.GetValue<int?>("Redis:Database") ?? 1;

    // Cluster
    opts.ClusterName =
        builder.Configuration.GetValue<string>("Cluster:Name") ?? "cqrs-cluster";
    opts.ConsulAddress =
        builder.Configuration.GetValue<string>("Consul:Address") ?? "localhost:8500";
    opts.AdvertisedHost =
        builder.Configuration.GetValue<string>("Cluster:AdvertisedHost") ?? "localhost";

    // Schema-Rolle (Multi-Node-Cold-Start): steuert AutoCreate/Apply der Marten-Kette.
    opts.SchemaRole = schemaRole;

    // gRPC Client Service
    opts.EnableGrpc = true;
});

// ─── Pull-Pfad (generiert) für alle IPullSubscriber ───
// Signal/Poll → SignalReceiverActor → per-Stream-Adapter → Store (Effekt + Marke).
// EIN generierter Aufruf: Kind-Contributors + Receiver/Poller + Push-Abkopplung.
// NACH AddCqrsFramework, damit der Pull-Startup nach dem Cluster-Start läuft.
builder.Services.AddGeneratedPullPaths();

// Prozess-Schicht (Event-Regel-DAG): generisches Manager-Kind + Korrelations-Router aus GeneratedProzessRegeln.
builder.Services.AddGeneratedProzesse();

// ─── Domain Pipeline ───
// WatchPath kommt jetzt aus der Konfiguration statt hardcoded.

builder.Services.AddDomainPipelineServices(
    watchPath: watchPath,
    preprocessedPath: preprocessedPath);
GeneratedPipelines.RegisterAllPipelines(builder.Services);

// P6.2: der EVENT-Pfad der Pipelines läuft über die geordnete Pull-Maschine (nicht mehr Push-Broker).
builder.Services.AddGeneratedPipelineEventPulls();

// Monitoring (Scheibe 1): HealthCheck + Prozess-/DLQ-Zähler (Read-Pfad auf Offen-Index + DLQ-Read-Store).
builder.Services.AddBackendMonitoring();

// Deadlines (Feature-Strom): DB-Uhr-getriebener Scheduler feuert fällige Fristen als Command.
// Die Domänen-Wahl (welcher Command) trifft der Host; DB-Uhr + Fristplan kommen aus dem Framework-Kern.
// Routing über den Frist-Kontext: Trainings-Timeout → MarkiereAlsHaengengeblieben, sonst Erinnerung.
builder.Services.AddDeadlines(f =>
    f.Kontext == Domain.Pipeline.Trainingslauf.TrainingFristPipeline.Kontext
        ? new Domain.Trainingslauf.MarkiereAlsHaengengeblieben(f.ZielAggregatId)
        : new Domain.Erinnerung.FristFaellig(f.ZielAggregatId, f.Kontext));

// ─── Build + Run ───

var app = builder.Build();

// ─── Migrator-Modus (Multi-Node-Cold-Start): migrieren und exiten ───
// Ein dedizierter Migrator legt ALLE Marten-Objekte (inkl. der sonst lazy erzeugten Snapshot-Tabellen)
// eager + advisory-lock-gesichert an, dann exit 0. Bewusst OHNE app.Run(): kein Cluster-/Consul-/PubSub-
// Start, der Migrator berührt nur Postgres. Die Member (AutoCreate.None) warten in Compose auf diesen
// erfolgreichen Abschluss (depends_on: service_completed_successfully) und finden das Schema fertig vor.
if (schemaRole == MartenSchemaRole.Migrator)
{
    Console.WriteLine();
    Console.WriteLine("==========================================================");
    Console.WriteLine("  CQRS/ES Schema-Migrator (Cluster__Role=migrator)");
    Console.WriteLine("  Lege alle Marten-Objekte eager + lock-gesichert an …");
    Console.WriteLine("==========================================================");
    await CqrsSchemaMigrator.ApplyAllAsync(app.Services);
    Console.WriteLine("  ✓ Marten-Schema vollständig migriert — exit 0.");
    Console.WriteLine();
    return;
}

app.MapCqrsGrpcService();

// ─── Webhook-Trigger (Host-Glue, Trigger-Ingress bleibt Push) ───
// POST /webhook/datei mit JSON { pfad, dateiname, dateigroesseBytes } → DateiErkannt an die Pipeline.
// Die generische Verdrahtung liegt in Infrastructure (testbar); die Domänen-Wahl trifft der Host.
app.MapPipelineWebhook<Domain.Pipeline.ImageProcessing.DateiErkannt>(
    "/webhook/datei",
    baueTrigger: r => r);   // DateiErkannt IST bereits der Trigger — Identität

// ─── Monitoring-Endpoints (GET /health, GET /monitoring/metrics) ───
app.MapBackendMonitoring();

Console.WriteLine();
Console.WriteLine("==========================================================");
Console.WriteLine("  CQRS/ES Server");
Console.WriteLine($"  gRPC listening on http://0.0.0.0:{grpcPort}");
Console.WriteLine($"  Pipeline WatchPath:      {watchPath}");
Console.WriteLine($"  Pipeline Preprocessed:   {preprocessedPath ?? "(default: WatchPath/.preprocessed)"}");
Console.WriteLine("==========================================================");
Console.WriteLine();

app.Run();