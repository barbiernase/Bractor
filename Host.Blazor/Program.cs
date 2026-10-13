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

// ── Umgebung (docs/konzept-werkbank.md §4): wohin sich dieser Client verbindet, mit welchem Token, wo er lauscht und welche Dateien er
//    zeigen darf, steht unter Clients:<Vertrag> der Umgebung (--umgebung <name> bzw. CQRS_UMGEBUNG). Der Vertrag ist der des Codes
//    ([assembly: IstClient<IArbeitsplatz>] → GeneratedWiring.Vertrag).
var umgebungsName = UmgebungLader.NameAus(args)
    ?? throw new InvalidOperationException("Host.Blazor braucht eine Umgebung: --umgebung <name> (oder CQRS_UMGEBUNG).");
var umgebung = UmgebungLader.Lade(builder.Configuration, umgebungsName, args);
var vertrag = GeneratedWiring_Domain_Client_Modules_Blazor.Vertrag;
var client = umgebung.Clients.GetValueOrDefault(vertrag)
    ?? throw new InvalidOperationException($"Umgebung {umgebungsName}: Abschnitt {UmgebungsAbschnitte.Clients}:{vertrag} fehlt (Token, Adresse, Urls, Dateien).");
var grpcAddress = umgebung.GrpcAdresse(client);

builder.WebHost.UseUrls(client.Urls);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddApexCharts();   // Live-Trainingskurven im Training-Dashboard (M9)

// ── Infrastruktur ──
builder.Services.AddSingleton(new GrpcServerConfig(grpcAddress));
builder.Services.AddScoped<ClientBus>(_ => new ClientBus(null));
builder.Services.AddScoped<IBus>(sp => sp.GetRequiredService<ClientBus>());
// Akteur-Token aus Clients:<Vertrag>:Token der Umgebung (Abschnitt Identitaeten: sein Akteur-Satz verkörpert die Akteure des Vertrags);
//   leer = kein Header (Umgebung ohne Identitäten: das Tor ist offen, der Vertrag gilt trotzdem).
builder.Services.AddScoped<GrpcProxy>(_ => new GrpcProxy { AkteurToken = client.Token is { Length: > 0 } t ? t : null });
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

// ── File-Endpunkt: liefert Bilder aus den Verzeichnissen Clients:<Vertrag>:Dateien der Umgebung ──
// Ohne diesen Aufruf existiert /api/files/... nicht → Resolver-URLs → 404.
app.MapCqrsFileEndpoint(client.Dateien);
Console.WriteLine($"[Host.Blazor] {vertrag} · Umgebung {umgebungsName} · gRPC {grpcAddress} · Token {(client.Token.Length > 0 ? client.Token : "—")} · "
    + $"Dateien: {string.Join(" | ", client.Dateien)}");

app.MapFallbackToPage("/_Host");
app.Run();
