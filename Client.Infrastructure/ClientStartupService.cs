using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Infrastructure;

/// <summary>
/// Bootstrap-Phasen des Clients. Die Shell gated auf Ready.
/// </summary>
public enum BootstrapState
{
    /// <summary>Verbindung wird aufgebaut.</summary>
    Connecting,
    /// <summary>Verbunden; wartet auf die Start-Daten der HydrationStores.</summary>
    Hydrating,
    /// <summary>Alle HydrationStores gefüllt — Module dürfen mounten.</summary>
    Ready,
    /// <summary>Verbindung oder Hydration fehlgeschlagen.</summary>
    Failed
}

/// <summary>
/// Orchestriert Client-Startup/-Shutdown und den Bootstrap-Lifecycle.
///
/// Der Bootstrap koppelt die Daten-Timeline an die View-Timeline: die Shell
/// mountet die Module erst, wenn State == Ready — und Ready bedeutet, dass ALLE
/// als IHydrationStore markierten Stores ihre Start-Daten erhalten haben. Damit
/// ist das "leere Liste beim Start"-Rennen konstruktiv ausgeschlossen: die View
/// startet nie, bevor die Daten da sind.
///
/// Ablauf StartAsync:
///   1. SubscribeAll    — Stores/Handler auf dem Bus (AttachToBus je Store)
///   2. RegisterQueries — Query→Response-Mappings
///   3. Activate        — ConnectionModule, VersioningModule, FileBridge
///   4. State=Connecting, ConnectAsync (publiziert ConnectionEstablished →
///      RefreshHandler dispatchen die Start-Queries)
///   5. State=Hydrating, warten bis alle HydrationStores.IsHydrated (mit Timeout)
///   6. State=Ready
///
/// Die konkreten Typen kommen als Delegates/Listen aus dem GeneratedWiring —
/// kein Domain-Import nötig.
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

    // ═══════════════════════════════════════════════════
    // BOOTSTRAP-STATE — von der Shell beobachtet
    // ═══════════════════════════════════════════════════

    private BootstrapState _state = BootstrapState.Connecting;

    /// <summary>Aktuelle Bootstrap-Phase.</summary>
    public BootstrapState State => _state;

    /// <summary>Feuert bei jedem State-Wechsel (Shell → StateHasChanged).</summary>
    public event Action? StateChanged;

    /// <summary>Maximale Wartezeit auf Hydration, bevor trotzdem Ready gesetzt wird.</summary>
    public TimeSpan HydrationTimeout { get; set; } = TimeSpan.FromSeconds(15);

    private void SetState(BootstrapState next)
    {
        if (_state == next) return;
        _state = next;
        StateChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════
    // START
    // ═══════════════════════════════════════════════════

    public async Task StartAsync(
        Action<IServiceProvider, IBus> subscribeAll,
        Action<QueryBridge, ClientBus> registerQueries,
        IReadOnlyList<Type> serverEventTypes,
        IReadOnlyList<Type> commandTypes,
        string serverAddress,
        IReadOnlyList<Type> hydrationStores,
        IReadOnlyDictionary<Type, string>? commandAggregateTypes = null,
        Action<IServiceProvider>? unsubscribeAll = null,
        CancellationToken ct = default)
    {
        _unsubscribeAll = unsubscribeAll;
        _hydrationStoreTypes = hydrationStores;

        // 1. Stores + Handler subscriben (Stores vor Handlern; AttachToBus je Store)
        subscribeAll(_serviceProvider, _bus);

        // 2. Query→Response-Mappings registrieren
        registerQueries(_queryBridge, _bus);

        // 3. Module aktivieren
        _connectionModule.Activate(_bus, commandTypes, commandAggregateTypes);
        _versioningModule.Activate(_bus, serverEventTypes);
        _fileBridge.Activate(_bus);

        // 4. Verbinden. Bei ConnectionEstablished dispatchen die RefreshHandler
        //    die Start-Queries (SucheImagePairs, GetProduktionsTage, …).
        SetState(BootstrapState.Connecting);
        var eventTypeNames = serverEventTypes.Select(t => t.Name).ToList();

        try
        {
            await _connectionModule.ConnectAsync(serverAddress, eventTypeNames, ct);
        }
        catch
        {
            SetState(BootstrapState.Failed);
            throw;
        }

        // 5. Auf Hydration warten: alle markierten Stores müssen ihre
        //    Start-Daten erhalten haben (IsHydrated). Timeout-geschützt.
        SetState(BootstrapState.Hydrating);
        await AwaitHydrationAsync(hydrationStores, ct);

        // 6. Fertig — Shell mountet jetzt die Module.
        SetState(BootstrapState.Ready);
    }

    /// <summary>
    /// Wartet, bis jeder HydrationStore IsHydrated meldet. Nutzt die
    /// HydrationChanged-Events der Stores plus einen initialen Check (falls die
    /// Daten schon vor dem Subscribe ankamen). Nach HydrationTimeout wird
    /// trotzdem fortgesetzt, damit die App nie ewig im Ladezustand hängt.
    /// </summary>
    private async Task AwaitHydrationAsync(
        IReadOnlyList<Type> hydrationStores, CancellationToken ct)
    {
        if (hydrationStores.Count == 0) return;

        var stores = hydrationStores
            .Select(t => _serviceProvider.GetService(t))
            .OfType<IHydrationStore>()
            .ToList();

        if (stores.Count == 0) return;

        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Check()
        {
            if (stores.All(s => s.IsHydrated))
                tcs.TrySetResult(true);
        }

        // Auf jede Store-Hydration hören, dann einmal initial prüfen (Daten
        // könnten schon vor dem Subscribe eingetroffen sein).
        foreach (var s in stores)
            s.HydrationChanged += Check;

        try
        {
            Check();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(HydrationTimeout);

            using (timeoutCts.Token.Register(() => tcs.TrySetResult(false)))
            {
                await tcs.Task;
            }
        }
        finally
        {
            foreach (var s in stores)
                s.HydrationChanged -= Check;
        }
    }

    // Gespeichert beim Start, genutzt beim Stop
    private Action<IServiceProvider>? _unsubscribeAll;

    // ═══════════════════════════════════════════════════
    // STOP
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Fährt alle Module herunter und setzt die Hydration der Stores zurück,
    /// damit bei einem erneuten StartAsync (Reconnect) wieder korrekt auf die
    /// frischen Start-Daten gewartet wird statt veraltete anzuzeigen.
    /// </summary>
    public async Task StopAsync()
    {
        // Hydration zurücksetzen, solange die Stores noch am Bus/DI hängen.
        // (Idempotent; ResetHydration ist billig.)
        ResetHydrationStores();

        SetState(BootstrapState.Connecting);

        // Stores vom Bus trennen — kein Flush-Leak nach Circuit-Close
        _unsubscribeAll?.Invoke(_serviceProvider);

        await _connectionModule.DisconnectAsync();
        await _connectionModule.DisposeAsync();
        await _fileBridge.DisposeAsync();
        _versioningModule.Deactivate();
    }

    private IReadOnlyList<Type>? _hydrationStoreTypes;

    private void ResetHydrationStores()
    {
        if (_hydrationStoreTypes == null) return;
        foreach (var t in _hydrationStoreTypes)
            if (_serviceProvider.GetService(t) is IHydrationStore s)
                s.ResetHydration();
    }
}
