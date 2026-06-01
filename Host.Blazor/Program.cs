// ── Host.Blazor/Program.cs ──
// Composition Root — KEIN Domain-Import.

using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Host.Blazor;
using Microsoft.AspNetCore.Components.Server.Circuits;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Konfiguration ───

var blazorUrls = builder.Configuration.GetValue<string>("Blazor:Urls")
    ?? "http://localhost:5010";
var grpcServerAddress = builder.Configuration.GetValue<string>("GrpcServer:Address")
    ?? "http://localhost:5001";
var watchPath = builder.Configuration.GetValue<string>("Pipeline:WatchPath")
    ?? "/data/input";
var preprocessedPath = builder.Configuration.GetValue<string>("Pipeline:PreprocessedPath");

builder.WebHost.UseUrls(blazorUrls);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// ═══════════════════════════════════════════════════════
// Infrastruktur — Scoped (pro Circuit)
// ═══════════════════════════════════════════════════════

builder.Services.AddSingleton(new GrpcServerConfig(grpcServerAddress));

builder.Services.AddScoped<ClientBus>(_ => new ClientBus(null));
builder.Services.AddScoped<IBus>(sp => sp.GetRequiredService<ClientBus>());

builder.Services.AddScoped<GrpcProxy>();
builder.Services.AddScoped<IGrpcProxy>(sp => sp.GetRequiredService<GrpcProxy>());

builder.Services.AddScoped<VersioningModule>();
builder.Services.AddScoped<IVersioningModule>(sp => sp.GetRequiredService<VersioningModule>());
builder.Services.AddScoped<ConnectionModule>();
builder.Services.AddScoped<QueryBridge>();

builder.Services.AddScoped<IFilePathResolver>(_ => new LocalFilePathResolver());
builder.Services.AddScoped<FileBridge>();

builder.Services.AddScoped<ClientStartupService>();

// ═══════════════════════════════════════════════════════
// Domäne — generiert, kein manuelles Import nötig
// ═══════════════════════════════════════════════════════

builder.Services.AddImagePairDomain();               // ← kein using nötig

// ═══════════════════════════════════════════════════════
// Circuit Lifecycle — generisch
// ═══════════════════════════════════════════════════════

builder.Services.AddScoped<CircuitHandler, CircuitHandlerBase>();

// ═══════════════════════════════════════════════════════

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();

var allowedPaths = new[] { watchPath, preprocessedPath }
    .Where(p => !string.IsNullOrEmpty(p))
    .ToArray();
app.MapCqrsFileEndpoint(allowedPaths);

app.MapFallbackToPage("/_Host");

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Host.Blazor                                              ║");
Console.WriteLine($"║  {blazorUrls,-55}║");
Console.WriteLine($"║  gRPC Server: {grpcServerAddress,-42}║");
Console.WriteLine($"║  WatchPath:   {watchPath,-42}║");
Console.WriteLine($"║  Preprocessed:{(preprocessedPath ?? "(nicht gesetzt)"),-42}║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

app.Run();

public record GrpcServerConfig(string Address);