using System.Linq;
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
    private readonly Infrastructure.Deadlines.FristPlaner? _fristPlaner;

    /// <summary>
    /// Token → CancellationTokenSource für geplante Self-Messages.
    /// Ermöglicht deterministisches Cancel: gleiches Token → altes Schedule verworfen.
    /// </summary>
    private readonly Dictionary<string, CancellationTokenSource> _scheduledTokens = new();
    // Selbst-Nachricht → Akteur, in dessen Auftrag sie geplant wurde (Referenz-Identität; mailbox-sicher, ein Thread).
    private readonly Dictionary<object, string> _selbstAkteur = new(ReferenceEqualityComparer.Instance);

    // P6.2: NUR für TRANSIENTE Events (ITransientEvent) — die sind nicht im Log und können daher nicht
    // auf den Pull-Pfad; sie bleiben per Invariante 6 auf dem verlierbaren Push-Broker. Persistierte
    // Events laufen über die Pull-Maschine (generierter {Name}EventPullKind).
    private Infrastructure.PubSub.BrokerSubscription? _subscription;

    protected PipelineActorBase(
        THandler logic,
        Cluster cluster,
        Infrastructure.PubSub.BrokerPublisher? publisher = null,
        ILogger? logger = null,
        Infrastructure.Deadlines.FristPlaner? fristPlaner = null)
    {
        _fristPlaner = fristPlaner;
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

                // Kanal 0: Self-Messages (Selbst<T>-Ausgang → eigene Mailbox)
                case IPipelineSelfMessage selfMsg:
                {
                    // Kausalkette über die eigene Mailbox: geplant im Auftrag eines Akteurs → so auch ausgeführt.
                    _selbstAkteur.Remove(selfMsg, out var ak);
                    using (Infrastructure.Akteure.AkteurHerkunft.Aus(ak))
                        await OnSelfMessageAsync(selfMsg, context);
                    break;
                }

                // Kanal 1: Direkte Trigger-Messages von nativen Actors oder anderen Pipelines
                case IPipelineTrigger trigger:
                    // Ein Trigger kommt ohne Envelope: sein Akteur ist der eine, der ihn per IDarf hineingibt (Ingress).
                    using (Infrastructure.Akteure.AkteurHerkunft.Aus(Infrastructure.Akteure.AkteurHerkunft.EindeutigerHalter(trigger.GetType())))
                        await OnTriggerAsync(trigger, context);
                    break;

                // Kanal 2: seit P6.2 nur noch TRANSIENTE Events via Push-Broker (persistierte laufen über Pull).
                case IAggregateEnvelope envelope:
                    using (Infrastructure.Akteure.AkteurHerkunft.Aus(envelope.UserId))
                        await OnEnvelopeAsync(envelope, context, context.CancellationToken);
                    break;

                // Aktivierung durch den PipelineStartupService (Started ist schon gelaufen): nur quittieren.
                case PipelineAktivieren:
                    context.Respond(new PipelineAck(Accepted: true));
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

        // P6.2: PERSISTIERTE Events laufen über die geordnete Pull-/Signal-Maschine (der generierte
        // {Name}EventPullKind weckt die PipelineEventPullBridge). Der Push-Broker trägt hier nur noch die
        // TRANSIENTEN Events (ITransientEvent) — die sind nicht im Log und gehören per Invariante 6 auf
        // den verlierbaren Kanal. Trigger-Ingress (Kanal 1) + Self-Messages (Kanal 0) bleiben ebenfalls hier.
        var transienteTypen = GetSubscribedEventTypes()
            .Where(t => typeof(ITransientEvent).IsAssignableFrom(t)).ToList();
        if (transienteTypen.Count > 0)
        {
            _subscription = new Infrastructure.PubSub.BrokerSubscription(
                context.System.Cluster(), _logic.PipelineId, context.Self);
            foreach (var type in transienteTypen)
            {
                await _subscription.SubscribeAsync(type);
                _logger?.LogDebug("[Pipeline:{PipelineId}] (transient) subscribed {EventType}", _logic.PipelineId, type.Name);
            }
        }

        var ctx = CreatePipelineContext(context);
        await _logic.OnInitializeAsync(ctx);
        // Der typisierte Ort für die erste Planung: Handle(PipelineGestartet, ctx) → OneOf<…, Selbst<T>>.
        await OnSelfMessageAsync(new PipelineGestartet(), context);
        _logger?.LogInformation("[Pipeline:{PipelineId}] Ready", _logic.PipelineId);
    }

    private async Task OnStoppingAsync()
    {
        _logger?.LogInformation("[Pipeline:{PipelineId}] Stopping", _logic.PipelineId);
        await _logic.OnShutdownAsync();
        if (_subscription != null)
            await _subscription.UnsubscribeAllAsync();
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
                te => BroadcastTransientAsync(te, ctx),
                p => PlaneAsync(p, context));
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
    // Kanal 2: TRANSIENTE Events via Push (persistierte laufen seit P6.2 über Pull)
    // ═══════════════════════════════════════════════════════

    private async Task OnEnvelopeAsync(IAggregateEnvelope envelope, IContext actorCtx, CancellationToken ct)
    {
        try
        {
            _logger?.LogDebug("[Pipeline:{PipelineId}] (transient) Event: {Event}", _logic.PipelineId, envelope.Payload.GetType().Name);

            var ctx = CreatePipelineContext(actorCtx,
                correlationId: envelope.CorrelationId,
                sourceAggregateId: envelope.AggregateId,
                sourceAggregateType: envelope.AggregateType,
                sourceAggregateVersion: envelope is EventEnvelope ee ? ee.AggregateVersion : null);

            await DispatchEventAsync(envelope, ctx,
                cmd => SendCommandAsync(cmd, ctx, ct),
                trig => SendTriggerAsync(trig, ctx.CorrelationId),
                te => BroadcastTransientAsync(te, ctx),
                p => PlaneAsync(p, actorCtx));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Pipeline:{PipelineId}] (transient) Event {EventType} failed",
                _logic.PipelineId, envelope.Payload.GetType().Name);
        }
    }

    // ═══════════════════════════════════════════════════════
    // Command-Sending (Trigger-/Self-Pfad)
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
    // Planung (typisierte Ausgänge Selbst<T> / Frist<TCmd>)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Die Senke der Planungs-Ausgänge: <see cref="ISelbstPlanung"/> → eigene Mailbox nach der Verzögerung
    /// (ReenterAfter, mailbox-sicher; gleiches Token ersetzt), <see cref="FristAuftrag"/> → durabler Fristplan.
    /// </summary>
    private Task PlaneAsync(IPlanung planung, IContext actorCtx)
    {
        switch (planung)
        {
            case ISelbstPlanung s:
                var cts = new CancellationTokenSource();
                if (s.Token is { } token)
                {
                    if (_scheduledTokens.Remove(token, out var existing)) existing.Cancel();
                    _scheduledTokens[token] = cts;
                }
                var nachricht = s.Nachricht;
                if (ImAuftrag.IstAkteur(ImAuftrag.Akteur)) _selbstAkteur[nachricht] = ImAuftrag.Akteur!;
                actorCtx.ReenterAfter(Task.Delay(s.Verzoegerung, cts.Token), () =>
                {
                    if (cts.IsCancellationRequested) { _selbstAkteur.Remove(nachricht); return; }
                    actorCtx.Send(actorCtx.Self, nachricht);
                    if (s.Token is { } t) _scheduledTokens.Remove(t);
                });
                return Task.CompletedTask;
            case FristAuftrag f when _fristPlaner != null:
                return _fristPlaner.PlaneAsync(f, actorCtx.CancellationToken);
            case FristAuftrag f:
                throw new InvalidOperationException($"[Pipeline:{_logic.PipelineId}] Frist '{f.Kontext}' geplant, aber kein FristPlaner registriert.");
            default:
                throw new NotSupportedException($"Unbekannte Planung {planung.GetType().Name}.");
        }
    }

    private PipelineContext CreatePipelineContext(
        IContext actorCtx,
        string? correlationId = null,
        Guid? sourceAggregateId = null,
        string? sourceAggregateType = null,
        int? sourceAggregateVersion = null)
    {
        return new PipelineContext
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
    /// Verarbeitet eine Self-Message (geplant über einen Selbst&lt;T&gt;-Ausgang, oder <see cref="PipelineGestartet"/>).
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
                te => BroadcastTransientAsync(te, ctx),
                p => PlaneAsync(p, context));
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
    private Task SendTriggerAsync(IPipelineTrigger trigger, string correlationId)
        => PipelineTriggerSender.SendAsync(_cluster, trigger, _logger);

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
            // Kausalkette: auch ein verlierbarer Hinweis trägt den Akteur, in dessen Auftrag die Pipeline gerade handelt.
            UserId = ImAuftrag.Akteur ?? ImAuftrag.Ohne,
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
        Func<ITransientEvent, Task> broadcastTransient,
        Func<IPlanung, Task> plane);

    /// <summary>Dispatch für Events (PubSub).</summary>
    protected abstract Task DispatchEventAsync(
        IAggregateEnvelope envelope,
        PipelineContext ctx,
        Func<ICommand, Task> sendCommand,
        Func<IPipelineTrigger, Task> sendTrigger,
        Func<ITransientEvent, Task> broadcastTransient,
        Func<IPlanung, Task> plane);

    /// <summary>Dispatch für Self-Messages (Selbst&lt;T&gt;, PipelineGestartet).</summary>
    protected abstract Task DispatchSelfAsync(
        IPipelineSelfMessage selfMsg,
        PipelineContext ctx,
        Func<ICommand, Task> sendCommand,
        Func<IPipelineTrigger, Task> sendTrigger,
        Func<ITransientEvent, Task> broadcastTransient,
        Func<IPlanung, Task> plane);
}