using Abstractions;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Basis-Klasse für Pipeline-Actors.
/// Handhabt Dual-Input (Trigger + Events), Command-Sending mit Retry,
/// und lokales Version-Caching.
///
/// Analog zu SubscriberActorBase (Events → ReadModel-Mutations)
/// und AggregateActorBase (Commands → Events).
///
/// Pipeline-Actors sind das serverseitige Gegenstück zum gRPC-Client:
/// Beide empfangen Events und senden Commands. Der Unterschied:
/// gRPC = Fire-and-Forget, Pipeline = Request-Response mit CommandResult.
///
/// Versioning-Strategie (ohne Redis):
///   1. CommandResult.NewVersion → Cache-Update nach jedem Command
///   2. PubSub-Events → passives Tracking aus IAggregateEnvelope
///   3. Kaltstart → Version 0 provoziert Conflict → Retry mit korrekter Version
/// </summary>
public abstract class PipelineActorBase<THandler> : IActor
    where THandler : IPipelineHandler
{
    protected readonly THandler _logic;
    private readonly Cluster _cluster;
    private readonly Infrastructure.PubSub.BrokerPublisher? _publisher;
    private readonly ILogger? _logger;

    private Infrastructure.PubSub.BrokerSubscription? _subscription;

    /// <summary>
    /// Lokaler Version-Cache. Wird gefüttert aus:
    /// - CommandResult.NewVersion (nach jedem gesendeten Command)
    /// - IAggregateEnvelope.AggregateVersion (passiv aus PubSub-Events)
    /// Kein Redis nötig — Conflicts werden per Retry aufgelöst.
    /// </summary>
    private readonly Dictionary<Guid, int> _versionCache = new();

    private const int MaxRetries = 3;

    protected PipelineActorBase(
        THandler logic,
        Cluster cluster,
        Infrastructure.PubSub.BrokerPublisher? publisher = null,
        ILogger? logger = null)
    {
        _logic = logic ?? throw new ArgumentNullException(nameof(logic));
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _publisher = publisher;
        _logger = logger;
    }

    public async Task ReceiveAsync(IContext context)
    {
        try
        {
            switch (context.Message)
            {
                case Started:
                    await OnStartedAsync(context);
                    break;

                // Kanal 1: Direkte Trigger-Messages von nativen Actors
                case IPipelineTrigger trigger:
                    await OnTriggerAsync(trigger, context);
                    break;

                // Kanal 2: Events via PubSub (identisch zu SubscriberActorBase)
                case IAggregateEnvelope envelope:
                    await OnEnvelopeAsync(envelope, context.CancellationToken);
                    break;

                case Stopping:
                    await OnStoppingAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Pipeline:{_logic.PipelineId}] ERROR: {ex.Message}");
            _logger?.LogError(ex, "[Pipeline:{PipelineId}] Unhandled error", _logic.PipelineId);

            // Trigger erwartet eine Antwort — sonst Retry
            if (context.Message is IPipelineTrigger)
            {
                context.Respond(new PipelineAck(Accepted: false));
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════

    private async Task OnStartedAsync(IContext context)
    {
        Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Starting...");

        // PubSub-Subscriptions aufbauen (Kanal 2)
        var eventTypes = GetSubscribedEventTypes();
        if (eventTypes.Count > 0)
        {
            _subscription = new Infrastructure.PubSub.BrokerSubscription(
                context.System.Cluster(),
                _logic.PipelineId,
                context.Self);

            foreach (var type in eventTypes)
            {
                await _subscription.SubscribeAsync(type);
                Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Subscribed to {type.Name}");
            }
        }

        await _logic.OnInitializeAsync();
        Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Ready");
    }

    private async Task OnStoppingAsync()
    {
        Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Stopping...");
        await _logic.OnShutdownAsync();

        if (_subscription != null)
        {
            await _subscription.UnsubscribeAllAsync();
        }

        Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Stopped");
    }

    // ═══════════════════════════════════════════════════════
    // Kanal 1: Trigger-Verarbeitung
    // ═══════════════════════════════════════════════════════

    private async Task OnTriggerAsync(IPipelineTrigger trigger, IContext context)
    {
        Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Trigger: {trigger.GetType().Name}");

        var ctx = new PipelineContext
        {
            CorrelationId = Guid.NewGuid().ToString()
        };

        try
        {
            await DispatchTriggerAsync(trigger, ctx, cmd => SendCommandAsync(cmd, ctx.CorrelationId));
            context.Respond(new PipelineAck(Accepted: true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Trigger failed: {ex.Message}");
            _logger?.LogError(ex, "[Pipeline:{PipelineId}] Trigger {TriggerType} failed",
                _logic.PipelineId, trigger.GetType().Name);
            context.Respond(new PipelineAck(Accepted: false));
        }
    }

    // ═══════════════════════════════════════════════════════
    // Kanal 2: Event-Verarbeitung (PubSub)
    // ═══════════════════════════════════════════════════════

    private async Task OnEnvelopeAsync(IAggregateEnvelope envelope, CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Event: {envelope.Payload.GetType().Name}");

            // Passives Version-Tracking (Stufe 2)
            if (envelope is EventEnvelope eventEnvelope && eventEnvelope.AggregateVersion > 0)
            {
                TrackVersion(eventEnvelope.AggregateId, eventEnvelope.AggregateVersion);
            }

            var ctx = new PipelineContext
            {
                CorrelationId = envelope.CorrelationId,
                SourceAggregateId = envelope.AggregateId,
                SourceAggregateType = envelope.AggregateType,
                SourceAggregateVersion = envelope is EventEnvelope ee
                    ? ee.AggregateVersion
                    : null
            };

            await DispatchEventAsync(envelope, ctx, cmd => SendCommandAsync(cmd, ctx.CorrelationId));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Event handling failed: {ex.Message}");
            _logger?.LogError(ex, "[Pipeline:{PipelineId}] Event {EventType} failed",
                _logic.PipelineId, envelope.Payload.GetType().Name);
        }
    }

    // ═══════════════════════════════════════════════════════
    // Command-Sending mit Retry
    // ═══════════════════════════════════════════════════════

    private async Task SendCommandAsync(ICommand command, string correlationId)
    {
        var aggregateId = command.AggregateId;
        var commandAggregateTypes = GetCommandAggregateTypes();

        if (!commandAggregateTypes.TryGetValue(command.GetType(), out var aggregateType))
        {
            _logger?.LogError("[Pipeline:{PipelineId}] No AggregateType mapping for {CommandType}",
                _logic.PipelineId, command.GetType().Name);
            return;
        }

        var version = ResolveVersion(command);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            var envelope = new CommandEnvelope
            {
                AggregateId = aggregateId,
                Payload = command,
                ExpectedVersion = version,
                AggregateType = aggregateType,
                CorrelationId = correlationId,
                OriginSessionId = _logic.PipelineId,
            };

            var identity = ClusterIdentity.Create(
                aggregateId.ToString(), aggregateType);

            var result = await _cluster.RequestAsync<CommandResult>(
                identity, envelope, CancellationToken.None);

            if (result == null)
            {
                _logger?.LogWarning("[Pipeline:{PipelineId}] No response for {CommandType} (attempt {Attempt})",
                    _logic.PipelineId, command.GetType().Name, attempt + 1);
                continue;
            }

            if (result.Success)
            {
                // Stufe 1: Version aus CommandResult cachen
                TrackVersion(aggregateId, result.NewVersion);
                Console.WriteLine($"[Pipeline:{_logic.PipelineId}] ✔ {command.GetType().Name} → v{result.NewVersion}");
                return;
            }

            // Concurrency-Conflict: Version korrigieren und Retry
            if (result.NewVersion > 0)
            {
                Console.WriteLine($"[Pipeline:{_logic.PipelineId}] Conflict: v{version} → v{result.NewVersion}, retrying...");
                TrackVersion(aggregateId, result.NewVersion);
                version = result.NewVersion;
                continue;
            }

            // Anderer Fehler (Rejection, technisch) — kein Retry
            _logger?.LogWarning("[Pipeline:{PipelineId}] {CommandType} rejected: {Error}",
                _logic.PipelineId, command.GetType().Name, result.ErrorMessage);
            return;
        }

        _logger?.LogError("[Pipeline:{PipelineId}] {CommandType} failed after {MaxRetries} attempts",
            _logic.PipelineId, command.GetType().Name, MaxRetries);
    }

    // ═══════════════════════════════════════════════════════
    // Versioning (kein Redis, nur lokaler Cache)
    // ═══════════════════════════════════════════════════════

    private int ResolveVersion(ICommand command)
    {
        // ICreationCommand → immer 0
        if (command is ICreationCommand)
            return 0;

        // Lokaler Cache (aus CommandResult oder PubSub-Events)
        if (_versionCache.TryGetValue(command.AggregateId, out var cached))
            return cached;

        // Kein Cache-Hit → 0 senden, Conflict provozieren, Retry mit korrekter Version
        return 0;
    }

    private void TrackVersion(Guid aggregateId, int version)
    {
        if (version > 0)
        {
            _versionCache[aggregateId] = version;
        }
    }

    // ═══════════════════════════════════════════════════════
    // Abstrakte Methoden (vom Generator gefüllt)
    // ═══════════════════════════════════════════════════════

    /// <summary>Event-Typen für PubSub-Subscriptions.</summary>
    protected abstract IReadOnlyList<Type> GetSubscribedEventTypes();

    /// <summary>Trigger-Typen die dieser Actor akzeptiert (für Logging/Validierung).</summary>
    protected abstract IReadOnlyList<Type> GetTriggerTypes();

    /// <summary>Command-Typ → AggregateType-Name für Routing.</summary>
    protected abstract IReadOnlyDictionary<Type, string> GetCommandAggregateTypes();

    /// <summary>Dispatch für Trigger (direkte Messages).</summary>
    protected abstract Task DispatchTriggerAsync(
        IPipelineTrigger trigger,
        PipelineContext ctx,
        Func<ICommand, Task> send);

    /// <summary>Dispatch für Events (PubSub).</summary>
    protected abstract Task DispatchEventAsync(
        IAggregateEnvelope envelope,
        PipelineContext ctx,
        Func<ICommand, Task> send);
}