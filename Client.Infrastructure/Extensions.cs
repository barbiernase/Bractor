using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Infrastructure;

/// <summary>
/// DI-Registrierung für Client.Infrastructure.
///
/// Registriert den Bus + alle Infrastruktur-Module als Singleton.
/// Domain-Registrierung (Stores, Handler, ViewModels) passiert in
/// GeneratedWiring.AddClientDomain() — wird separat aufgerufen.
///
/// WICHTIG: Blazor Server nutzt diese Extension NICHT — dort wird
/// alles manuell als Scoped registriert (pro Circuit).
/// Diese Extension ist für Desktop/MAUI/Test-Szenarien.
///
/// Typische Startup-Reihenfolge:
///   services.AddClientInfrastructure(syncContext);  // Bus + Module
///   services.AddClientDomain();                     // Generiert: Stores + Handler + ViewModels
/// </summary>
public static class ClientInfrastructureExtensions
{
    /// <summary>
    /// Registriert Bus, ConnectionModule, VersioningModule, QueryBridge,
    /// FileBridge und den ClientStartupService.
    /// </summary>
    /// <param name="syncContext">
    /// Der SynchronizationContext des UI-Threads.
    /// null in Tests (dann dispatcht der Bus direkt).
    /// </param>
    public static IServiceCollection AddClientInfrastructure(
        this IServiceCollection services,
        SynchronizationContext? syncContext = null)
    {
        // Bus: Singleton, der SyncContext wird einmal beim Start gesetzt
        services.AddSingleton<ClientBus>(_ => new ClientBus(syncContext));
        services.AddSingleton<IBus>(sp => sp.GetRequiredService<ClientBus>());

        // gRPC Proxy — über Interface für Testbarkeit
        services.AddSingleton<GrpcProxy>();
        services.AddSingleton<IGrpcProxy>(sp => sp.GetRequiredService<GrpcProxy>());

        // Infrastruktur-Module
        services.AddSingleton<VersioningModule>();
        services.AddSingleton<IVersioningModule>(sp =>
            sp.GetRequiredService<VersioningModule>());

        services.AddSingleton<ConnectionModule>();
        services.AddSingleton<QueryBridge>();
        services.AddSingleton<FileBridge>();

        // Startup-Orchestrierung
        services.AddSingleton<ClientStartupService>();

        return services;
    }
}