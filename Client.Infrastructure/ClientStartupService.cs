using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Infrastructure;

/// <summary>
/// Orchestriert den Client-Startup in korrekter Reihenfolge.
///
/// Wird vom CircuitHandler (Blazor) oder Application-Startup (Desktop) aufgerufen.
/// Nimmt alle Module per DI und aktiviert sie in der richtigen Reihenfolge:
///
///   1. SubscribeAll    — Stores und Handler auf dem Bus registrieren
///   2. RegisterQueries — Query→Response-Mappings auf der QueryBridge
///   3. Activate        — ConnectionModule, VersioningModule, FileBridge
///   4. ConnectAsync    — gRPC-Verbindung herstellen
///
/// Die konkreten Typen (Commands, Events, Queries) kommen als Delegates
/// aus dem generierten GeneratedWiring — kein Domain-Import nötig.
/// </summary>
public class ClientStartupService
{
    private readonly ClientBus _bus;
    private readonly ConnectionModule _connectionModule;
    private readonly QueryBridge _queryBridge;
    private readonly VersioningModule _versioningModule;
    private readonly FileBridge _fileBridge;
    private readonly IServiceProvider _serviceProvider;

    public ClientStartupService(
        ClientBus bus,
        ConnectionModule connectionModule,
        QueryBridge queryBridge,
        VersioningModule versioningModule,
        FileBridge fileBridge,
        IServiceProvider serviceProvider)
    {
        _bus = bus;
        _connectionModule = connectionModule;
        _queryBridge = queryBridge;
        _versioningModule = versioningModule;
        _fileBridge = fileBridge;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Startet alle Client-Module in korrekter Reihenfolge.
    ///
    /// subscribeAll und registerQueries kommen aus GeneratedWiring —
    /// das sind statische Methoden, die der WiringGenerator erzeugt.
    /// Der ClientStartupService kennt keine Domain-Typen.
    /// </summary>
    public async Task StartAsync(
        Action<IServiceProvider, IBus> subscribeAll,
        Action<QueryBridge, ClientBus> registerQueries,
        IReadOnlyList<Type> serverEventTypes,
        IReadOnlyList<Type> commandTypes,
        string serverAddress,
        IReadOnlyDictionary<Type, string>? commandAggregateTypes = null,
        CancellationToken ct = default)
    {
        // 1. Stores + Handler subscriben (Reihenfolge: Stores vor Handlern)
        subscribeAll(_serviceProvider, _bus);

        // 2. Query→Response-Mappings registrieren
        registerQueries(_queryBridge, _bus);

        // 3. Module aktivieren (subscriben auf dem Bus)
        _connectionModule.Activate(_bus, commandTypes, commandAggregateTypes);
        _versioningModule.Activate(_bus, serverEventTypes);
        _fileBridge.Activate(_bus);

        // 4. gRPC-Verbindung herstellen
        var eventTypeNames = serverEventTypes
            .Select(t => t.Name)
            .ToList();

        await _connectionModule.ConnectAsync(serverAddress, eventTypeNames, ct);
    }

    /// <summary>
    /// Fährt alle Module herunter.
    /// Wird vom CircuitHandler bei OnCircuitClosedAsync aufgerufen.
    /// </summary>
    public async Task StopAsync()
    {
        await _connectionModule.DisconnectAsync();
        await _connectionModule.DisposeAsync();
        await _fileBridge.DisposeAsync();
        _versioningModule.Deactivate();
    }
}