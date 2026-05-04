// ═══════════════════════════════════════════════════════════════════
// Host.Blazor — Blazor Server Host für ImagePair
//
// Scoped statt Singleton — jeder Circuit bekommt eigene Instanzen.
// SynchronizationContext ist null — kein UI-Thread-Dispatch nötig.
//
// ═══════════════════════════════════════════════════════════════════
// KONFIGURATION:
//   Alle Werte kommen aus appsettings.json / appsettings.Production.json
//   oder Umgebungsvariablen.
//
//   Umgebungsvariablen:
//     Blazor__Urls=http://0.0.0.0:5010
//     GrpcServer__Address=http://server:5001
//     Pipeline__WatchPath=/data/input
// ═══════════════════════════════════════════════════════════════════

using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Domain.Client.ImagePair;
using Host.Blazor;
using Microsoft.AspNetCore.Components.Server.Circuits;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Konfiguration lesen ───

var blazorUrls = builder.Configuration.GetValue<string>("Blazor:Urls")
    ?? "http://localhost:5010";
var grpcServerAddress = builder.Configuration.GetValue<string>("GrpcServer:Address")
    ?? "http://localhost:5001";
var watchPath = builder.Configuration.GetValue<string>("Pipeline:WatchPath")
    ?? "/data/input";
var preprocessedPath = builder.Configuration.GetValue<string>("Pipeline:PreprocessedPath");

// FIX: URL aus Konfiguration statt hardcoded.
// Produktion: "http://0.0.0.0:5010" oder "http://*:5010"
builder.WebHost.UseUrls(blazorUrls);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// ═══════════════════════════════════════════════════════
// MudBlazor
// ═══════════════════════════════════════════════════════

builder.Services.AddMudServices();

// ═══════════════════════════════════════════════════════
// gRPC-Server-Adresse als Service registrieren
// ═══════════════════════════════════════════════════════

builder.Services.AddSingleton(new GrpcServerConfig(grpcServerAddress));

// ═══════════════════════════════════════════════════════
// Client-Infrastruktur — Scoped (pro Circuit)
// ═══════════════════════════════════════════════════════

// Bus: null SyncContext — Blazor Server rendert serverseitig
builder.Services.AddScoped<ClientBus>(_ => new ClientBus(null));
builder.Services.AddScoped<IBus>(sp => sp.GetRequiredService<ClientBus>());

// gRPC Proxy
builder.Services.AddScoped<GrpcProxy>();
builder.Services.AddScoped<IGrpcProxy>(sp => sp.GetRequiredService<GrpcProxy>());

// Infrastruktur-Module
builder.Services.AddScoped<VersioningModule>();
builder.Services.AddScoped<IVersioningModule>(sp => sp.GetRequiredService<VersioningModule>());
builder.Services.AddScoped<ConnectionModule>();
builder.Services.AddScoped<QueryBridge>();

// FileBridge — Scoped (pro Circuit), Resolver ebenfalls
builder.Services.AddScoped<IFilePathResolver>(_ =>
    new LocalFilePathResolver());
builder.Services.AddScoped<FileBridge>();

// Startup-Orchestrierung
builder.Services.AddScoped<ClientStartupService>();

// ═══════════════════════════════════════════════════════
// Client-Domäne — Scoped (pro Circuit)
// ═══════════════════════════════════════════════════════

builder.Services.AddScoped<ImagePairStore>();
builder.Services.AddScoped<ImagePairStatistikHandler>();

// ═══════════════════════════════════════════════════════
// CircuitHandler für Lifecycle
// ═══════════════════════════════════════════════════════

builder.Services.AddScoped<CircuitHandler, ImagePairCircuitHandler>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();

// File-Endpunkt — erlaubt Zugriff auf Originale UND preprocessed Bilder.
// Beide Pfade werden als erlaubte Verzeichnisse registriert.
var allowedPaths = new[] { watchPath, preprocessedPath }
    .Where(p => !string.IsNullOrEmpty(p))
    .ToArray();
app.MapCqrsFileEndpoint(allowedPaths);

app.MapFallbackToPage("/_Host");

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Host.Blazor — ImagePair (MudBlazor)                     ║");
Console.WriteLine($"║  {blazorUrls,-55}║");
Console.WriteLine($"║  gRPC Server: {grpcServerAddress,-42}║");
Console.WriteLine($"║  WatchPath:   {watchPath,-42}║");
Console.WriteLine($"║  Preprocessed:{(preprocessedPath ?? "(nicht gesetzt)"),-42}║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

app.Run();

// ═══════════════════════════════════════════════════════
// Config-Record für DI
// ═══════════════════════════════════════════════════════

/// <summary>
/// Kapselt die gRPC-Server-Adresse für DI-Injection.
/// Wird vom CircuitHandler konsumiert.
/// </summary>
public record GrpcServerConfig(string Address);