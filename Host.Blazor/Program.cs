using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Host.Blazor;
using Host.Blazor.Shell;
using ApexCharts;
using Microsoft.AspNetCore.Components.Server.Circuits;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var blazorUrls = builder.Configuration.GetValue<string>("Blazor:Urls") ?? "http://localhost:5010";
var grpcAddress = builder.Configuration.GetValue<string>("GrpcServer:Address") ?? "http://localhost:5001";
var watchPath = builder.Configuration.GetValue<string>("Pipeline:WatchPath") ?? "/data/input";

// Preprocessed-Pfad: identischer Config-Key wie im Server (Host.Grpc).
// Fallback auf WatchPath/.preprocessed — exakt wie DomainPipelineExtensions.
var preprocessedPath = builder.Configuration.GetValue<string>("Pipeline:PreprocessedPath")
    ?? Path.Combine(watchPath, ".preprocessed");

builder.WebHost.UseUrls(blazorUrls);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddApexCharts();   // Live-Trainingskurven im Training-Dashboard (M9)

// ── Infrastruktur ──
builder.Services.AddSingleton(new GrpcServerConfig(grpcAddress));
builder.Services.AddScoped<ClientBus>(_ => new ClientBus(null));
builder.Services.AddScoped<IBus>(sp => sp.GetRequiredService<ClientBus>());
builder.Services.AddScoped<GrpcProxy>();
builder.Services.AddScoped<IGrpcProxy>(sp => sp.GetRequiredService<GrpcProxy>());
builder.Services.AddScoped<VersioningModule>();
builder.Services.AddScoped<IVersioningModule>(sp => sp.GetRequiredService<VersioningModule>());
builder.Services.AddScoped<ConnectionModule>();
builder.Services.AddScoped<QueryBridge>();
builder.Services.AddScoped<IFilePathResolver, LocalFilePathResolver>();
builder.Services.AddScoped<FileBridge>();
builder.Services.AddScoped<ClientStartupService>();
builder.Services.AddTransient<EffectScope>();

// ── Shell ──
builder.Services.AddScoped<ShellState>();
builder.Services.AddScoped<CircuitHandler, CircuitHandlerBase>();

// ── Domain + Module (generiert) ──
builder.Services.AddClientDomain_Domain_Client_Modules_Blazor();
builder.Services.AddModules_Domain_Client_Modules_Blazor();
var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();

// ── File-Endpunkt: liefert Bilder aus WatchPath + PreprocessedPath ──
// Ohne diesen Aufruf existiert /api/files/... nicht → Resolver-URLs → 404.
app.MapCqrsFileEndpoint(watchPath, preprocessedPath);
Console.WriteLine($"[Host.Blazor] File-Endpunkt /api/files erlaubt: {watchPath} | {preprocessedPath}");

app.MapFallbackToPage("/_Host");
app.Run();
