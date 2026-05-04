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
using Infrastructure.Extensions;
using Infrastructure.GrpcClient;
using Infrastructure.Pipeline;

var builder = WebApplication.CreateBuilder(args);

// ─── Konfiguration lesen ───

var grpcPort = builder.Configuration.GetValue<int>("Grpc:Port", 5001);
var watchPath = builder.Configuration.GetValue<string>("Pipeline:WatchPath")
    ?? "/data/input";
var preprocessedPath = builder.Configuration.GetValue<string>("Pipeline:PreprocessedPath");

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

    // gRPC Client Service
    opts.EnableGrpc = true;
});

// ─── Domain Pipeline ───
// WatchPath kommt jetzt aus der Konfiguration statt hardcoded.

builder.Services.AddDomainPipelineServices(
    watchPath: watchPath,
    preprocessedPath: preprocessedPath);
GeneratedPipelines.RegisterAllPipelines(builder.Services);

// ─── Build + Run ───

var app = builder.Build();

app.MapCqrsGrpcService();

Console.WriteLine();
Console.WriteLine("==========================================================");
Console.WriteLine("  CQRS/ES Server");
Console.WriteLine($"  gRPC listening on http://0.0.0.0:{grpcPort}");
Console.WriteLine($"  Pipeline WatchPath:      {watchPath}");
Console.WriteLine($"  Pipeline Preprocessed:   {preprocessedPath ?? "(default: WatchPath/.preprocessed)"}");
Console.WriteLine("==========================================================");
Console.WriteLine();

app.Run();