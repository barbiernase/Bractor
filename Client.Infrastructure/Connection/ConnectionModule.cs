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
    private readonly IAusgangskorb _korb;
    private Task? _nachsendenTask;

    private ClientBus? _bus;
    private IReadOnlyDictionary<Type, string>? _commandAggregateTypes;
    private CancellationTokenSource? _cts;
    private Task? _eventReadTask;
    private Task? _stateReadTask;
    private Task? _aktualisierungsTask;
    private readonly List<IDisposable> _subscriptions = new();

    // ★ Retry-on-Silence (docs/08 §8.4). Der Loop sendet mit DERSELBEN (deterministischen) CommandId erneut,
    //   bis eine korrelierte Antwort kommt — server-seitig idempotent (Inbox vor OCC).
    private readonly PendingCommandTracker _pending = new();
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(5);
    private const int MaxSendAttempts = 3;

    public ConnectionModule(
        IGrpcProxy proxy,
        IVersioningModule versioning,
        string defaultAggregateType = "",
        IAusgangskorb? korb = null)
    {
        _proxy = proxy;
        _versioning = versioning;
        _defaultAggregateType = defaultAggregateType;
        _korb = korb ?? new Ausgangskorb();
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
        string vertrag,
        string vertragHash,
        CancellationToken ct = default)
    {
        if (_bus == null)
            throw new InvalidOperationException("Activate must be called before ConnectAsync");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            var capabilities = await _proxy.ConnectAsync(serverAddress, vertrag, vertragHash, _cts.Token);
            // Der Server nennt den angenommenen Vertrag mit SEINEM Hash: ohne Annahme läuft die Leitung nicht als dieser Client.
            if (capabilities.Vertrag != vertrag)
                throw new InvalidOperationException($"Der Server hat den Vertrag '{vertrag}' nicht angenommen (Antwort: '{capabilities.Vertrag}').");
            if (capabilities.VertragHash != vertragHash)
                Console.Error.WriteLine($"[Client] Vertrag {vertrag}: Hash {vertragHash} ≠ Server {capabilities.VertragHash} — Client und Server "
                    + "sind gegen verschiedene Stände gebaut.");

            _eventReadTask = EventReadLoopAsync(_cts.Token);
            _stateReadTask = StateChangeLoopAsync(_cts.Token);
            _aktualisierungsTask = AktualisierungsLoopAsync(_cts.Token);
            // Was beim letzten Mal offen blieb (Verbindung weg, Client neu gestartet): noch einmal, mit derselben Command-Id.
            _nachsendenTask = SendeOffeneAsync();

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
        if (_aktualisierungsTask != null)
            try { await _aktualisierungsTask; } catch { /* cancelled */ }
        if (_nachsendenTask != null)
            try { await _nachsendenTask; } catch { /* cancelled */ }

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
                    command.Ziel,
                    "Not connected")));
            return;
        }

        // Read-Your-Writes: dieses Aggregat als "gerade geschrieben" merken → künftige Queries tragen dafür „mindestens S auf v“
        // (IVersioning.Mindestens), der Reader wartet begrenzt, bis seine Projektion so weit ist (docs/08 §8.3).
        _versioning.MarkWritten(command.Ziel);

        try
        {
            // ExpectedVersion-Logik:
            //   ICreationCommand → immer 0 (Aggregat darf noch nicht existieren).
            //   Sonst die Version, die der Client kennt — aus einem Event-Push oder aus einer Antwort (eine Version je Aggregat)
            //     → OCC (CommandModus.Client).
            //   Kennt er das Aggregat gar nicht → Sentinel -1: der Server mappt negativ auf CommandModus.Emittiert (kein OCC, Inbox-Dedup).
            var expectedVersion = command is ICreationCommand
                ? 0
                : _versioning.GetVersion(command.Ziel) ?? -1;

            var envelope = new CommandEnvelope
            {
                // ★ deterministische CommandId — EINMAL erzeugt, über Retries wiederverwendet →
                //   der Server dedupliziert einen Doppel-Send idempotent (Inbox vor OCC).
                CommandId = Guid.NewGuid(),
                Payload = command,
                Modus = new CommandModus.Client(expectedVersion),
                CorrelationId = !string.IsNullOrEmpty(ctx.CorrelationId)
                    ? ctx.CorrelationId
                    : Guid.NewGuid().ToString(),
                AggregateType = ResolveAggregateType(command),
                OriginSessionId = _proxy.SessionId,
            };

            _korb.Lege(envelope);   // VOR dem Senden: bricht die Verbindung, geht er nach dem Wiederverbinden mit derselben Id noch einmal
            await SendWithRetryAsync(command, envelope);
        }
        catch (Exception ex)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new CommandSendFailed(
                    command.GetType().Name,
                    command.Ziel,
                    ex.Message)));
        }
    }

    /// <summary>
    /// Sendet den Command und wiederholt bei STILLE (keine korrelierte Antwort binnen <see cref="AckTimeout"/>)
    /// mit DERSELBEN Hülle (gleiche CommandId → server-seitig idempotent, docs/08 §8.4). Kam eine Antwort (ein Event
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
                {
                    _korb.Erledigt(envelope.CommandId);
                    return;   // korrelierte Antwort (Event / Ablehnung / CommandFailed) kam
                }
            }

            // Verbunden und trotzdem still: der Ausgang bleibt unbekannt, aber ein Nachsenden nach dem Wiederverbinden brächte nichts Neues.
            _korb.Erledigt(envelope.CommandId);
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new CommandUnbestaetigt(
                    command.GetType().Name,
                    command.Ziel,
                    MaxSendAttempts)));
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            _pending.Forget(envelope.CorrelationId);
        }
    }

    /// <summary>
    /// Sendet die offenen Commands des Ausgangskorbs erneut — mit ihrer Command-Id und Korrelation, an die neue Sitzung (ihre Ablehnung soll sie
    /// erreichen). Die Inbox des Ziels lässt jeden genau einmal wirken, auch wenn er beim ersten Mal schon ankam.
    /// </summary>
    private async Task SendeOffeneAsync()
    {
        foreach (var offen in _korb.Offen())
        {
            if (_cts?.IsCancellationRequested ?? true) return;
            try { await SendWithRetryAsync(offen.Payload, offen with { OriginSessionId = _proxy.SessionId }); }
            catch (Exception) { /* bleibt im Korb, bis zum nächsten Verbinden */ }
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
                // ★ eine korrelierte Antwort (Domänen-Event ODER CommandFailed) beendet den Retry-Loop
                //   des zugehörigen Commands. CommandFailed reist als normales Event über denselben Kanal.
                if (!string.IsNullOrEmpty(envelope.CorrelationId))
                    _pending.Ack(envelope.CorrelationId);

                var context = new MessageContext
                {
                    AggregateId = envelope.AggregateId,
                    AggregateType = envelope.AggregateType,
                    AggregateVersion = envelope.AggregateVersion,
                    StreamHeadVersion = envelope.StreamHeadVersion,
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

    /// <summary>
    /// Fragt neu, was der Server nennt (eine Projektion hat weiterverarbeitet, was die letzte Antwort zeigte — auch transitiv, etwa ein fremdes
    /// Label in einer Datensatz-Ansicht): die Query geht wieder auf den Bus, die QueryBridge stellt sie, die Antwort landet in den Stores.
    /// </summary>
    private async Task AktualisierungsLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var query in _proxy.Aktualisierungen.ReadAllAsync(ct))
                _bus!.PostToSyncContext(() => _bus.Publish(query, MessageContext.Local()));
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (ChannelClosedException) { /* proxy disposed */ }
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

    private int _freigegeben;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _freigegeben, 1) == 1) return;
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        _cts?.Cancel();
        _cts?.Dispose();
    }
}