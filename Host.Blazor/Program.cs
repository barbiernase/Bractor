using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Abstractions;
using Host.Blazor;
using Infrastructure.Umgebung;
using Host.Blazor.Shell;
using ApexCharts;
using Microsoft.AspNetCore.Components.Server.Circuits;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Umgebung (docs/06 §6.5): wohin sich diese App verbindet (das Backend der Umgebung), mit welchem Token, wo sie lauscht (Rechner und Port) und
//    welche Dateien sie zeigen darf, steht in ihrer Instanz (apps/<instanz>.json; --umgebung <name> und --app <instanz> bzw. CQRS_UMGEBUNG, CQRS_APP).
//    Die App ist die dieses Codes ([assembly: IstApp<ArbeitsplatzApp>] → GeneratedWiring.Vertrag; am Handshake nennt sie sich so).
var umgebungsName = UmgebungLader.NameAus(args)
    ?? throw new InvalidOperationException("Host.Blazor braucht eine Umgebung: --umgebung <name> (oder CQRS_UMGEBUNG).");
var umgebung = UmgebungLader.Lade(builder.Configuration, umgebungsName, args);
var vertrag = GeneratedWiring_Domain_Client_Modules_Blazor.Vertrag;
var (instanz, client) = AppInstanzwahl.Waehle(umgebung, vertrag, args);
var url = client.Url(umgebung)
    ?? throw new InvalidOperationException($"Umgebung {umgebungsName}: die App-Instanz {instanz} nennt keinen Port — eine App mit Oberfläche braucht einen.");
var grpcAddress = umgebung.GrpcAdresse();   // das Backend der Umgebung (Rechner + gRPC-Port)

// Auf einem Rechner mit Adresse localhost nur lokal; sonst auf allen Schnittstellen (erreichbar ist er unter seiner Adresse im Pool).
builder.WebHost.UseUrls(umgebung.Adresse(client.Rechner) is "localhost" or "127.0.0.1" ? url : $"http://*:{client.Port}");
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddApexCharts();   // Live-Trainingskurven im Training-Dashboard

// ── Infrastruktur ──
builder.Services.AddSingleton(new GrpcServerConfig(grpcAddress));
builder.Services.AddScoped<ClientBus>(_ => new ClientBus(null));
builder.Services.AddScoped<IBus>(sp => sp.GetRequiredService<ClientBus>());
// Akteur-Token aus der App-Instanz (Abschnitt Identitaeten: sein Akteur-Satz verkörpert die Akteure des Vertrags);
//   leer = kein Header (Umgebung ohne Identitäten: das Tor ist offen, der Vertrag gilt trotzdem).
builder.Services.AddScoped<GrpcProxy>(_ => new GrpcProxy(Domain.Leitung.GeneratedNutzlast.Instanz) { AkteurToken = client.Token is { Length: > 0 } t ? t : null });
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

// ── File-Endpunkt: liefert Bilder aus den Verzeichnissen der App-Instanz (Dateien) ──
// Ohne diesen Aufruf existiert /api/files/... nicht → Resolver-URLs → 404.
app.MapCqrsFileEndpoint(client.Dateien);
Console.WriteLine($"[Host.Blazor] {vertrag} als {instanz} ({url}) · Umgebung {umgebungsName} · gRPC {grpcAddress} · Token {(client.Token.Length > 0 ? client.Token : "—")} · "
    + $"Dateien: {string.Join(" | ", client.Dateien)}");

app.MapFallbackToPage("/_Host");
app.Run();
