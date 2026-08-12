using System.Threading.Channels;
using Abstractions;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Versioning;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Brücke zwischen Client-Bus und gRPC-Server.
///
/// Upstream (Commands → gRPC):
///   Subscribt async auf alle Command-Typen via bus.SubscribeAsync(Type, handler).
///   Baut CommandEnvelope (AggregateId aus E1, ExpectedVersion aus VersioningModule).
///   Löst AggregateType aus CommandAggregateTypes Dictionary auf.
///   Sendet fire-and-forget via IGrpcProxy.SendCommandAsync.
///
/// Downstream (gRPC → Events → Bus):
///   Liest Events aus IGrpcProxy.Events Channel.
///   Extrahiert MessageContext aus EventEnvelope.
///   Postet via PostToSyncContext auf den UI-Thread → bus.Publish(object).
///
/// Kein Reflection — alles über nicht-generische Bus-API.
/// </summary>
public class ConnectionModule : IAsyncDisposable
{
    private readonly IGrpcProxy _proxy;
    private readonly IVersioningModule _versioning;
    private readonly string _defaultAggregateType;

    private ClientBus? _bus;
    private IReadOnlyDictionary<Type, string>? _commandAggregateTypes;
    private CancellationTokenSource? _cts;
    private Task? _eventReadTask;
    private Task? _stateReadTask;
    private readonly List<IDisposable> _subscriptions = new();

    // ★ T2b: Retry-on-Silence. Der Loop sendet mit DERSELBEN (deterministischen) CommandId erneut,
    //   bis eine korrelierte Antwort kommt — server-seitig idempotent (Inbox vor OCC).
    private readonly PendingCommandTracker _pending = new();
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(5);
    private const int MaxSendAttempts = 3;

    public ConnectionModule(
        IGrpcProxy proxy,
        IVersioningModule versioning,
        string defaultAggregateType = "")
    {
        _proxy = proxy;
        _versioning = versioning;
        _defaultAggregateType = defaultAggregateType;
    }

    // ═══════════════════════════════════════════════════════════
    // ACTIVATE — subscribt auf Command-Typen (kein Reflection)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Aktiviert das Modul: subscribt async auf alle Command-Typen.
    /// Muss VOR ConnectAsync aufgerufen werden.
    ///
    /// commandTypes: typeof(MarkiereAlsErledigt), typeof(ErstelleTodo), ...
    ///   Kommt aus GeneratedWiring.CommandTypes.
    ///
    /// commandAggregateTypes: Command-Typ → AggregateType-Name.
    ///   Kommt aus GeneratedWiring.CommandAggregateTypes.
    ///   Wenn null, wird _defaultAggregateType verwendet (Abwärtskompatibilität).
    /// </summary>
    public void Activate(
        ClientBus bus,
        IReadOnlyList<Type> commandTypes,
        IReadOnlyDictionary<Type, string>? commandAggregateTypes = null)
    {
        _bus = bus;
        _commandAggregateTypes = commandAggregateTypes;

        foreach (var cmdType in commandTypes)
        {
            // Nicht-generisch: Handler bekommt object, castet zu ICommand
            var sub = bus.SubscribeAsync(cmdType, async (obj, ctx) =>
            {
                if (obj is ICommand command)
                    await HandleCommandAsync(command, ctx);
            });
            _subscriptions.Add(sub);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // CONNECT / DISCONNECT
    // ═══════════════════════════════════════════════════════════

    public async Task ConnectAsync(
        string serverAddress,
        IReadOnlyList<string> serverEventTypes,
        CancellationToken ct = default)
    {
        if (_bus == null)
            throw new InvalidOperationException("Activate must be called before ConnectAsync");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            var capabilities = await _proxy.ConnectAsync(
                serverAddress, serverEventTypes, _cts.Token);

            _eventReadTask = EventReadLoopAsync(_cts.Token);
            _stateReadTask = StateChangeLoopAsync(_cts.Token);

            _bus.PostToSyncContext(() =>
                _bus.Publish(new ConnectionEstablished(capabilities.SessionId)));
        }
        catch (Exception ex)
        {
            _bus.PostToSyncContext(() =>
                _bus.Publish(new ConnectionLost(ex.Message)));
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();

        await _proxy.DisconnectAsync();

        if (_eventReadTask != null)
            try { await _eventReadTask; } catch { /* cancelled */ }
        if (_stateReadTask != null)
            try { await _stateReadTask; } catch { /* cancelled */ }

        _bus?.PostToSyncContext(() =>
            _bus.Publish(new ConnectionLost("Disconnected by client")));
    }

    // ═══════════════════════════════════════════════════════════
    // UPSTREAM: Command → Envelope → gRPC
    // ═══════════════════════════════════════════════════════════

    private async Task HandleCommandAsync(ICommand command, MessageContext ctx)
    {
        if (!_proxy.IsConnected)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new CommandSendFailed(
                    command.GetType().Name,
                    command.AggregateId,
                    "Not connected")));
            return;
        }

        try
        {
            // ExpectedVersion-Logik:
            //   ICreationCommand → immer 0 (Aggregat darf noch nicht existieren)
            //   Alle anderen     → aus VersioningModule (Client kennt Version aus Events/Deps)
            var expectedVersion = command is ICreationCommand
                ? 0
                : _versioning.GetVersion(command.AggregateId) ?? 0;

            var envelope = new CommandEnvelope
            {
                // ★ T2b: deterministische CommandId — EINMAL erzeugt, über Retries wiederverwendet →
                //   der Server dedupliziert einen Doppel-Send idempotent (Inbox vor OCC).
                CommandId = Guid.NewGuid(),
                AggregateId = command.AggregateId,
                Payload = command,
                Modus = new CommandModus.Client(expectedVersion),
                CorrelationId = !string.IsNullOrEmpty(ctx.CorrelationId)
                    ? ctx.CorrelationId
                    : Guid.NewGuid().ToString(),
                AggregateType = ResolveAggregateType(command),
                OriginSessionId = _proxy.SessionId,
            };

            await SendWithRetryAsync(command, envelope);
        }
        catch (Exception ex)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new CommandSendFailed(
                    command.GetType().Name,
                    command.AggregateId,
                    ex.Message)));
        }
    }

    /// <summary>
    /// Sendet den Command und wiederholt bei STILLE (keine korrelierte Antwort binnen <see cref="AckTimeout"/>)
    /// mit DERSELBEN Hülle (gleiche CommandId → server-seitig idempotent, T2b). Kam eine Antwort (ein Event
    /// ODER ein CommandFailed mit dieser CorrelationId), ist der Ausgang bekannt → fertig. Bleibt es nach allen
    /// Versuchen still, meldet der Loop <see cref="CommandUnbestaetigt"/> (Ausgang unbekannt, sicher
    /// wiederholbar) — NICHT „fehlgeschlagen".
    /// </summary>
    private async Task SendWithRetryAsync(ICommand command, CommandEnvelope envelope)
    {
        var ct = _cts?.Token ?? CancellationToken.None;
        _pending.Register(envelope.CorrelationId);   // VOR dem Senden, sonst ginge ein früher Ack verloren
        try
        {
            for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
            {
                await _proxy.SendCommandAsync(envelope);
                if (await _pending.WaitAsync(envelope.CorrelationId, AckTimeout, ct))
                    return;   // korrelierte Antwort (Event / CommandFailed) kam
            }

            _bus?.PostToSyncContext(() =>
                _bus.Publish(new CommandUnbestaetigt(
                    command.GetType().Name,
                    command.AggregateId,
                    MaxSendAttempts)));
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            _pending.Forget(envelope.CorrelationId);
        }
    }

    /// <summary>
    /// Löst den AggregateType für einen Command auf.
    /// Erst Dictionary-Lookup, dann Fallback auf _defaultAggregateType.
    /// </summary>
    private string ResolveAggregateType(ICommand command)
    {
        if (_commandAggregateTypes != null &&
            _commandAggregateTypes.TryGetValue(command.GetType(), out var aggregateType))
            return aggregateType;

        return _defaultAggregateType;
    }

    // ═══════════════════════════════════════════════════════════
    // DOWNSTREAM: gRPC → EventEnvelope → Bus (kein Reflection)
    // ═══════════════════════════════════════════════════════════

    private async Task EventReadLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var envelope in _proxy.Events.ReadAllAsync(ct))
            {
                // ★ T2b: eine korrelierte Antwort (Domänen-Event ODER CommandFailed) beendet den Retry-Loop
                //   des zugehörigen Commands. CommandFailed reist als normales Event über denselben Kanal.
                if (!string.IsNullOrEmpty(envelope.CorrelationId))
                    _pending.Ack(envelope.CorrelationId);

                var context = new MessageContext
                {
                    AggregateId = envelope.AggregateId,
                    AggregateType = envelope.AggregateType,
                    AggregateVersion = envelope.AggregateVersion,
                    CorrelationId = envelope.CorrelationId,
                    CreatedAtUtc = envelope.CreatedAtUtc,
                };

                // Nicht-generisch: bus.Publish(object, context)
                // Routet intern via message.GetType() — kein Reflection
                var payload = (object)envelope.Payload;
                _bus!.PostToSyncContext(() =>
                    _bus.Publish(payload, context));
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (ChannelClosedException) { /* proxy disposed */ }
        catch (Exception ex)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new ConnectionLost(ex.Message)));
        }
    }

    private async Task StateChangeLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var state in _proxy.StateChanges.ReadAllAsync(ct))
            {
                switch (state)
                {
                    case ConnectionState.Failed:
                        _bus!.PostToSyncContext(() =>
                            _bus.Publish(new ConnectionLost("Connection failed")));
                        break;

                    case ConnectionState.Reconnecting:
                        _bus!.PostToSyncContext(() =>
                            _bus.Publish(new ReconnectAttempt(1)));
                        break;
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (ChannelClosedException) { /* proxy disposed */ }
    }

    // ═══════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════

    public async ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        _cts?.Cancel();
        _cts?.Dispose();
    }
}