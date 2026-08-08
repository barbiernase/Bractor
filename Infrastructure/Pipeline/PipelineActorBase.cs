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
    private readonly IDeadLetterSink? _deadLetters;   // ★ §5: tote Commands beobachtbar machen (optional)

    private Infrastructure.PubSub.BrokerSubscription? _subscription;

    /// <summary>
    /// Lokaler Version-Cache. Wird gefüttert aus:
    /// - CommandResult.NewVersion (nach jedem gesendeten Command)
    /// - IAggregateEnvelope.AggregateVersion (passiv aus PubSub-Events)
    /// Kein Redis nötig — Conflicts werden per Retry aufgelöst.
    /// </summary>
    private readonly Dictionary<Guid, int> _versionCache = new();

    /// <summary>
    /// Token → CancellationTokenSource für geplante Self-Messages.
    /// Ermöglicht deterministisches Cancel: gleiches Token → altes Schedule verworfen.
    /// </summary>
    private readonly Dictionary<string, CancellationTokenSource> _scheduledTokens = new();

    private const int MaxRetries = 3;

    protected PipelineActorBase(
        THandler logic,
        Cluster cluster,
        Infrastructure.PubSub.BrokerPublisher? publisher = null,
        ILogger? logger = null,
        IDeadLetterSink? deadLetters = null)
    {
        _logic = logic ?? throw new ArgumentNullException(nameof(logic));
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _publisher = publisher;
        _logger = logger;
        _deadLetters = deadLetters;
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

                // Kanal 0: Self-Messages (ScheduleSelf → eigene Mailbox)
                case IPipelineSelfMessage selfMsg:
                    await OnSelfMessageAsync(selfMsg, context);
                    break;

                // Kanal 1: Direkte Trigger-Messages von nativen Actors oder anderen Pipelines
                case IPipelineTrigger trigger:
                    await OnTriggerAsync(trigger, context);
                    break;

                // Kanal 2: Events via PubSub (identisch zu SubscriberActorBase)
                case IAggregateEnvelope envelope:
                    await OnEnvelopeAsync(envelope, context, context.CancellationToken);
                    break;

                case Stopping:
                    await OnStoppingAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
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
        _logger?.LogInformation("[Pipeline:{PipelineId}] Starting", _logic.PipelineId);

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
                _logger?.LogDebug("[Pipeline:{PipelineId}] Subscribed to {EventType}", _logic.PipelineId, type.Name);
            }
        }

        // Init-Context mit ScheduleSelf für periodische Ticks
        var ctx = CreatePipelineContext(context);
        await _logic.OnInitializeAsync(ctx);
        _logger?.LogInformation("[Pipeline:{PipelineId}] Ready", _logic.PipelineId);
    }

    private async Task OnStoppingAsync()
    {
        _logger?.LogInformation("[Pipeline:{PipelineId}] Stopping", _logic.PipelineId);
        await _logic.OnShutdownAsync();

        if (_subscription != null)
        {
            await _subscription.UnsubscribeAllAsync();
        }

        _logger?.LogInformation("[Pipeline:{PipelineId}] Stopped", _logic.PipelineId);
    }

    // ═══════════════════════════════════════════════════════
    // Kanal 1: Trigger-Verarbeitung
    // ═══════════════════════════════════════════════════════

    private async Task OnTriggerAsync(IPipelineTrigger trigger, IContext context)
    {
        _logger?.LogDebug("[Pipeline:{PipelineId}] Trigger: {Trigger}", _logic.PipelineId, trigger.GetType().Name);

        var ctx = CreatePipelineContext(context, correlationId: Guid.NewGuid().ToString());

        try
        {
            await DispatchTriggerAsync(trigger, ctx,
                cmd => SendCommandAsync(cmd, ctx.CorrelationId),
                trig => SendTriggerAsync(trig, ctx.CorrelationId),
                te => BroadcastTransientAsync(te, ctx));
            context.Respond(new PipelineAck(Accepted: true));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Pipeline:{PipelineId}] Trigger failed", _logic.PipelineId);
            _logger?.LogError(ex, "[Pipeline:{PipelineId}] Trigger {TriggerType} failed",
                _logic.PipelineId, trigger.GetType().Name);
            context.Respond(new PipelineAck(Accepted: false));
        }
    }

    // ═══════════════════════════════════════════════════════
    // Kanal 2: Event-Verarbeitung (PubSub)
    // ═══════════════════════════════════════════════════════

    private async Task OnEnvelopeAsync(IAggregateEnvelope envelope, IContext actorCtx, CancellationToken ct)
    {
        try
        {
            _logger?.LogDebug("[Pipeline:{PipelineId}] Event: {Event}", _logic.PipelineId, envelope.Payload.GetType().Name);

            // Passives Version-Tracking (Stufe 2)
            if (envelope is EventEnvelope eventEnvelope && eventEnvelope.AggregateVersion > 0)
            {
                TrackVersion(eventEnvelope.AggregateId, eventEnvelope.AggregateVersion);
            }

            var ctx = CreatePipelineContext(actorCtx,
                correlationId: envelope.CorrelationId,
                sourceAggregateId: envelope.AggregateId,
                sourceAggregateType: envelope.AggregateType,
                sourceAggregateVersion: envelope is EventEnvelope ee
                    ? ee.AggregateVersion
                    : null);

            await DispatchEventAsync(envelope, ctx,
                cmd => SendCommandAsync(cmd, ctx.CorrelationId),
                trig => SendTriggerAsync(trig, ctx.CorrelationId),
                te => BroadcastTransientAsync(te, ctx));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Pipeline:{PipelineId}] Event handling failed", _logic.PipelineId);
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
            await DeadLetterAsync(command, null, correlationId, "Kein AggregateType-Mapping (Command nicht routbar)", 0);
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
                _logger?.LogDebug("[Pipeline:{PipelineId}] ✔ {Command} → v{Version}", _logic.PipelineId, command.GetType().Name, result.NewVersion);
                return;
            }

            // Concurrency-Conflict: Version korrigieren und Retry
            if (result.NewVersion > 0)
            {
                _logger?.LogDebug("[Pipeline:{PipelineId}] Conflict: v{Version} → v{NewVersion}, retrying", _logic.PipelineId, version, result.NewVersion);
                TrackVersion(aggregateId, result.NewVersion);
                version = result.NewVersion;
                continue;
            }

            // Anderer Fehler — kein Retry. Domänen-Ablehnung (RejectionEvent gesetzt) ist ein GÜLTIGES
            // Geschäftsergebnis → nur loggen. Ein TECHNISCHER Fehler (kein RejectionEvent) ist ein echter
            // Verlust → DLQ (§5).
            _logger?.LogWarning("[Pipeline:{PipelineId}] {CommandType} rejected: {Error}",
                _logic.PipelineId, command.GetType().Name, result.ErrorMessage);
            if (result.RejectionEvent is null)
                await DeadLetterAsync(command, aggregateType, correlationId,
                    $"Technischer Fehler: {result.ErrorMessage}", attempt + 1);
            return;
        }

        // ★ §5: Retries erschöpft (Timeout/Conflict-Schleife) → beobachtbarer DLQ-Eintrag statt stillem Drop.
        _logger?.LogError("[Pipeline:{PipelineId}] {CommandType} failed after {MaxRetries} attempts",
            _logic.PipelineId, command.GetType().Name, MaxRetries);
        await DeadLetterAsync(command, aggregateType, correlationId,
            $"Nach {MaxRetries} Versuchen kein Erfolg (Timeout/Conflict)", MaxRetries);
    }

    /// <summary>§5: Schreibt einen toten Command in die DLQ (best-effort, no-op wenn keine Senke registriert).</summary>
    private Task DeadLetterAsync(ICommand command, string? aggregateType, string? correlationId, string grund, int versuche)
    {
        if (_deadLetters is null) return Task.CompletedTask;
        return _deadLetters.WriteAsync(new DeadLetter
        {
            Id = Guid.NewGuid(),
            Quelle = _logic.PipelineId,
            CommandType = command.GetType().Name,
            AggregateId = command.AggregateId,
            AggregateType = aggregateType,
            CorrelationId = correlationId,
            Grund = grund,
            Versuche = versuche,
            ErfasstUtc = DateTimeOffset.UtcNow
        });
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
    // PipelineContext mit Live-Implementierung
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Konkrete PipelineContext-Implementierung mit echtem ScheduleSelf/CancelScheduled.
    /// Nur im Actor verwendet — Pipeline-Handler sehen nur die Basis-API.
    /// </summary>
    private class LivePipelineContext : PipelineContext
    {
        private readonly PipelineActorBase<THandler> _actor;
        private readonly IContext _actorCtx;

        public LivePipelineContext(PipelineActorBase<THandler> actor, IContext actorCtx)
        {
            _actor = actor;
            _actorCtx = actorCtx;
        }

        public override void ScheduleSelf<T>(T payload, TimeSpan delay, string? token = null)
        {
            var cts = new CancellationTokenSource();

            if (token is not null)
            {
                if (_actor._scheduledTokens.Remove(token, out var existing))
                    existing.Cancel();
                _actor._scheduledTokens[token] = cts;
            }

            _actorCtx.ReenterAfter(Task.Delay(delay, cts.Token), () =>
            {
                if (cts.IsCancellationRequested) return;
                _actorCtx.Send(_actorCtx.Self, payload);
                if (token is not null) _actor._scheduledTokens.Remove(token);
            });
        }

        public override bool CancelScheduled(string token)
        {
            if (!_actor._scheduledTokens.Remove(token, out var cts)) return false;
            cts.Cancel();
            return true;
        }
    }

    private PipelineContext CreatePipelineContext(
        IContext actorCtx,
        string? correlationId = null,
        Guid? sourceAggregateId = null,
        string? sourceAggregateType = null,
        int? sourceAggregateVersion = null)
    {
        return new LivePipelineContext(this, actorCtx)
        {
            CorrelationId = correlationId ?? "",
            SourceAggregateId = sourceAggregateId,
            SourceAggregateType = sourceAggregateType,
            SourceAggregateVersion = sourceAggregateVersion,
        };
    }

    // ═══════════════════════════════════════════════════════
    // Kanal 0: Self-Message-Verarbeitung
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Verarbeitet eine Self-Message die via ScheduleSelf geplant wurde.
    /// Context mit ScheduleSelf verdrahtet — Handler kann nächsten Tick planen.
    /// </summary>
    private async Task OnSelfMessageAsync(IPipelineSelfMessage selfMsg, IContext context)
    {
        _logger?.LogDebug("[Pipeline:{PipelineId}] Self: {Message}", _logic.PipelineId, selfMsg.GetType().Name);

        var ctx = CreatePipelineContext(context, correlationId: Guid.NewGuid().ToString());

        try
        {
            await DispatchSelfAsync(selfMsg, ctx,
                cmd => SendCommandAsync(cmd, ctx.CorrelationId),
                trig => SendTriggerAsync(trig, ctx.CorrelationId),
                te => BroadcastTransientAsync(te, ctx));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Pipeline:{PipelineId}] Self-message failed", _logic.PipelineId);
            _logger?.LogError(ex, "[Pipeline:{PipelineId}] Self {SelfType} failed",
                _logic.PipelineId, selfMsg.GetType().Name);
        }
    }

    // ═══════════════════════════════════════════════════════
    // Trigger-Sending (Pipeline → Pipeline)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Sendet einen Trigger an die Ziel-Pipeline.
    /// Nutzt GeneratedPipelines.TriggerToPipelineId für das Routing.
    /// </summary>
    private async Task SendTriggerAsync(IPipelineTrigger trigger, string correlationId)
    {
        var triggerType = trigger.GetType();

        if (!Infrastructure.Pipeline.GeneratedPipelines.TriggerToPipelineId
                .TryGetValue(triggerType, out var targetPipelineId))
        {
            _logger?.LogError(
                "[Pipeline:{PipelineId}] No TriggerToPipelineId mapping for {TriggerType}",
                _logic.PipelineId, triggerType.Name);
            return;
        }

        var identity = ClusterIdentity.Create(targetPipelineId, $"Pipeline-{targetPipelineId}");

        var ack = await _cluster.RequestAsync<PipelineAck>(
            identity, trigger, CancellationToken.None);

        if (ack?.Accepted == true)
        {
            _logger?.LogDebug("[Pipeline:{PipelineId}] ✔ Trigger {TriggerType} → {Target}", _logic.PipelineId, triggerType.Name, targetPipelineId);
        }
        else
        {
            _logger?.LogWarning(
                "[Pipeline:{PipelineId}] Trigger {TriggerType} → {Target} not accepted",
                _logic.PipelineId, triggerType.Name, targetPipelineId);
        }
    }

    // ═══════════════════════════════════════════════════════
    // TransientEvent-Broadcast (Pipeline → PubSub)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Publiziert ein ITransientEvent über den BrokerPublisher.
    /// Kein Aggregat-Roundtrip — direkt ans PubSub.
    /// </summary>
    private async Task BroadcastTransientAsync(ITransientEvent evt, PipelineContext ctx)
    {
        if (_publisher == null)
        {
            _logger?.LogError(
                "[Pipeline:{PipelineId}] BrokerPublisher not available for transient broadcast",
                _logic.PipelineId);
            return;
        }

        var envelope = new EventEnvelope
        {
            Payload = evt,
            CorrelationId = ctx.CorrelationId,
            AggregateId = ctx.SourceAggregateId ?? Guid.Empty,
            AggregateType = ctx.SourceAggregateType ?? _logic.PipelineId,
        };

        await _publisher.PublishAsync(envelope);
        _logger?.LogDebug("[Pipeline:{PipelineId}] ✔ Broadcast {Event}", _logic.PipelineId, evt.GetType().Name);
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
        Func<ICommand, Task> sendCommand,
        Func<IPipelineTrigger, Task> sendTrigger,
        Func<ITransientEvent, Task> broadcastTransient);

    /// <summary>Dispatch für Events (PubSub).</summary>
    protected abstract Task DispatchEventAsync(
        IAggregateEnvelope envelope,
        PipelineContext ctx,
        Func<ICommand, Task> sendCommand,
        Func<IPipelineTrigger, Task> sendTrigger,
        Func<ITransientEvent, Task> broadcastTransient);

    /// <summary>Dispatch für Self-Messages (ScheduleSelf).</summary>
    protected abstract Task DispatchSelfAsync(
        IPipelineSelfMessage selfMsg,
        PipelineContext ctx,
        Func<ICommand, Task> sendCommand,
        Func<IPipelineTrigger, Task> sendTrigger,
        Func<ITransientEvent, Task> broadcastTransient);
}