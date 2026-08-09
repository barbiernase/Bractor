using Abstractions;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Basis-Klasse für Pipeline-Actors.
/// Handhabt Dual-Input (Trigger + Events) und das Command-Sending über das EINE Emit-Primitiv (EM-1).
///
/// Analog zu SubscriberActorBase (Events → ReadModel-Mutations)
/// und AggregateActorBase (Commands → Events).
///
/// Pipeline-Actors sind das serverseitige Gegenstück zum gRPC-Client:
/// Beide empfangen Events und senden Commands.
///
/// Kein Versionsargument und kein OCC-Retry: Commands gehen über <see cref="CommandEmitter"/> als
/// <see cref="CommandModus.Emittiert"/> (deterministische CommandId → Empfänger-Inbox dedupliziert,
/// bounded Token) — die alte „Version 0 → Conflict → Retry"-Strategie ist mit P3/EM-1 entfallen (W1/W2).
/// </summary>
public abstract class PipelineActorBase<THandler> : IActor
    where THandler : IPipelineHandler
{
    protected readonly THandler _logic;
    private readonly Cluster _cluster;
    private readonly ICommandEmitter _emitter;        // ★ P3: das EINE Emit-Primitiv (EM-1) — Command→Fremd-Aggregat
    private readonly Infrastructure.PubSub.BrokerPublisher? _publisher;
    private readonly ILogger? _logger;

    private Infrastructure.PubSub.BrokerSubscription? _subscription;

    /// <summary>
    /// Token → CancellationTokenSource für geplante Self-Messages.
    /// Ermöglicht deterministisches Cancel: gleiches Token → altes Schedule verworfen.
    /// </summary>
    private readonly Dictionary<string, CancellationTokenSource> _scheduledTokens = new();

    /// <summary>Bounded-Frist für Pipeline→Pipeline-Trigger-Sends (W2: kein Infinit-Hang).</summary>
    private static readonly TimeSpan TriggerFrist = TimeSpan.FromSeconds(5);

    protected PipelineActorBase(
        THandler logic,
        Cluster cluster,
        Infrastructure.PubSub.BrokerPublisher? publisher = null,
        ILogger? logger = null)
    {
        _logic = logic ?? throw new ArgumentNullException(nameof(logic));
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _emitter = new Infrastructure.PubSub.CommandEmitter(cluster, logger);
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
                cmd => SendCommandAsync(cmd, ctx, context.CancellationToken),
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

            var ctx = CreatePipelineContext(actorCtx,
                correlationId: envelope.CorrelationId,
                sourceAggregateId: envelope.AggregateId,
                sourceAggregateType: envelope.AggregateType,
                sourceAggregateVersion: envelope is EventEnvelope ee
                    ? ee.AggregateVersion
                    : null);

            await DispatchEventAsync(envelope, ctx,
                cmd => SendCommandAsync(cmd, ctx, ct),
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

    private Task SendCommandAsync(ICommand command, PipelineContext ctx, CancellationToken ct)
    {
        // ★ P3: über das EINE Emit-Primitiv (EM-1) — deterministische CommandId (W1) + bounded Token (W2).
        //   Event-Pfad: die Auslöse-Position (SourceAggregateId/-Version) reist in die Kausalität → stabile
        //   Id über Re-Wakes, der Empfänger dedupliziert. Trigger-/Self-Pfad: kein Log-Event → best-effort
        //   frische Id (die volle Idempotenz-Zerlegung Event→Reaktion / Trigger→Push ist P6).
        var korrelation = Guid.TryParse(ctx.CorrelationId, out var kr) ? kr : Guid.Empty;
        var k = ctx.SourceAggregateId is Guid src
            ? new EmitKausalität(korrelation, src, $"{ctx.SourceAggregateVersion}:{command.GetType().Name}")
            : new EmitKausalität(korrelation, Guid.NewGuid(), command.GetType().Name);
        return _emitter.EmitAsync(command, k, ct);
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
                cmd => SendCommandAsync(cmd, ctx, context.CancellationToken),
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

        // W2: bounded — ein hängender Ziel-Actor darf den sendenden Turn nicht unbegrenzt blockieren.
        // At-least-once: ein Timeout heilt der nächste Auslöser/Re-Wake (Invariante 2).
        using var cts = new CancellationTokenSource(TriggerFrist);
        PipelineAck? ack;
        try
        {
            ack = await _cluster.RequestAsync<PipelineAck>(identity, trigger, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning(
                "[Pipeline:{PipelineId}] Trigger {TriggerType} → {Target} Timeout ({Frist}s) — Re-Wake heilt",
                _logic.PipelineId, triggerType.Name, targetPipelineId, TriggerFrist.TotalSeconds);
            return;
        }

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