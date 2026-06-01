using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;

namespace Client.Infrastructure;

/// <summary>
/// Orchestriert den Client-Startup und -Shutdown in korrekter Reihenfolge.
///
/// Wird vom CircuitHandler (Blazor) oder Application-Startup (Desktop) aufgerufen.
/// Nimmt alle Module per DI und aktiviert sie in der richtigen Reihenfolge:
///
///   StartAsync:
///     1. SubscribeAll    — Stores und Handler auf dem Bus registrieren
///     2. AttachAll       — Stores an Bus hängen (DispatchCompleted-Registrierung)
///     3. RegisterQueries — Query→Response-Mappings auf der QueryBridge
///     4. Activate        — ConnectionModule, VersioningModule, FileBridge
///     5. ConnectAsync    — gRPC-Verbindung herstellen
///
///   StopAsync:
///     1. UnsubscribeAll  — Stores vom Bus trennen (kein Flush-Leak)
///     2. Disconnect      — gRPC trennen
///
/// Die konkreten Typen (Commands, Events, Queries, Stores) kommen als Delegates
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
    /// subscribeAll, attachAll, unsubscribeAll und registerQueries kommen aus
    /// GeneratedWiring — statische Methoden ohne Reflection.
    ///
    /// attachAll ist separat von subscribeAll damit die Subscription-Reihenfolge
    /// (Stores vor Handlern) und die Bus-Attachment-Reihenfolge unabhängig bleiben.
    /// In der Praxis erzeugt der Generator beides in SubscribeAll — der Parameter
    /// wird übergeben damit ClientStartupService keine Domain-Typen kennen muss.
    /// </summary>
    public async Task StartAsync(
        Action<IServiceProvider, IBus> subscribeAll,
        Action<QueryBridge, ClientBus> registerQueries,
        IReadOnlyList<Type> serverEventTypes,
        IReadOnlyList<Type> commandTypes,
        string serverAddress,
        IReadOnlyDictionary<Type, string>? commandAggregateTypes = null,
        Action<IServiceProvider>? unsubscribeAll = null,
        CancellationToken ct = default)
    {
        _unsubscribeAll = unsubscribeAll;

        // 1. Stores + Handler subscriben (Reihenfolge: Stores vor Handlern)
        //    AttachToBus wird innerhalb von subscribeAll für jeden Store aufgerufen
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

    // Gespeichert beim Start, genutzt beim Stop
    private Action<IServiceProvider>? _unsubscribeAll;

    /// <summary>
    /// Fährt alle Module herunter.
    ///
    /// Ruft zuerst UnsubscribeAll auf — trennt alle Stores vom Bus
    /// damit kein Flush mehr nach dem Shutdown feuert.
    /// Danach gRPC trennen.
    ///
    /// Wird vom CircuitHandler bei OnCircuitClosedAsync aufgerufen.
    /// </summary>
    public async Task StopAsync()
    {
        // Stores vom Bus trennen — kein Flush-Leak nach Circuit-Close
        _unsubscribeAll?.Invoke(_serviceProvider);

        await _connectionModule.DisconnectAsync();
        await _connectionModule.DisposeAsync();
        await _fileBridge.DisposeAsync();
        _versioningModule.Deactivate();
    }
}
