using Abstractions;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Basis-Klasse für Aggregate-Actors.
/// 
/// WICHTIG: Der Handler wird NACH dem Laden des States erstellt,
/// damit Handler, Decider und Applier alle auf das gleiche State-Objekt zeigen.
/// 
/// WICHTIG: Muss IMMER context.Respond() aufrufen, sonst macht Proto.Actor Retries!
/// 
/// ★ OneOf-Migration: Decider wirft keine Exceptions mehr für Domänen-Ablehnungen.
///   Stattdessen yieldet er ITransientEvent (z.B. BestandNichtAusreichend).
///   Der Actor trennt persistent/transient und sendet Ablehnungen per Targeted Delivery.
///   CommandFailed bleibt für technische Fehler (catch-Block).
///
/// ★ Phase 4: IVersionTracker (optional) für Redis-basierte Stale Detection.
/// ★ Phase 4: CommandResult.NewVersion wird nach Apply gesetzt.
/// </summary>
public abstract class AggregateActorBase<TState> : IActor
    where TState : class, IState, new()
{
    private readonly IAggregateHandlerFactory _handlerFactory;
    private readonly IEventStoreRepository _eventStore;
    private readonly IVersionTracker? _versionTracker;
    private readonly BrokerPublisher? _publisher;
    private readonly ILogger? _logger;
    
    private IAggregateHandler? _handler;  // Wird nach State-Load erstellt!
    private TState? _state;
    private Guid _id;
    private bool _initialized = false;

    protected AggregateActorBase(
        IAggregateHandlerFactory handlerFactory,
        IEventStoreRepository eventStore,
        IVersionTracker? versionTracker = null,
        BrokerPublisher? publisher = null,
        ILogger? logger = null)
    {
        _handlerFactory = handlerFactory;
        _eventStore = eventStore;
        _versionTracker = versionTracker;
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
                    Console.WriteLine($"[Actor] Started");
                    break;

                case CommandEnvelope envelope:
                    if (!_initialized)
                    {
                        await InitializeAsync(envelope.AggregateId);
                    }
                    await HandleCommandAsync(context, envelope);
                    break;

                case Stopping:
                    Console.WriteLine($"[Actor] Stopping for ID: {_id}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Actor] ERROR: {ex.Message}");
            
            // WICHTIG: Auch bei Fehler antworten, sonst Retry!
            if (context.Message is CommandEnvelope envelope)
            {
                // CommandFailed für technische Fehler
                await TryPublishCommandFailedAsync(envelope, ex.Message);
                
                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message,
                    AggregateId = _id
                });
            }
        }
    }

    private async Task InitializeAsync(Guid aggregateId)
    {
        _id = aggregateId;
        
        // 1. State laden (oder neu erstellen)
        _state = await _eventStore.LoadStateAsync<TState>(_id);
        if (_state == null)
        {
            _state = new TState { Id = _id, Version = 0 };
        }
        
        // 2. Handler JETZT erstellen - mit dem geladenen State!
        //    Dadurch zeigen Handler, Decider und Applier alle auf _state
        _handler = _handlerFactory.CreateHandler(_state);
        
        _initialized = true;
        Console.WriteLine($"[Actor] Initialized with ID: {_id}, Version: {_state.Version}");
    }

    private async Task HandleCommandAsync(IContext context, CommandEnvelope cmdEnvelope)
    {
        try
        {
            Console.WriteLine($"[Actor] Processing {cmdEnvelope.Payload.GetType().Name}");

            // Validierung: Falsche AggregateId
            if (cmdEnvelope.AggregateId != _id)
            {
                Console.WriteLine($"[Actor] Command for wrong aggregate");
                await TryPublishCommandFailedAsync(cmdEnvelope, "Command sent to wrong aggregate");
                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = "Command sent to wrong aggregate",
                    AggregateId = _id
                });
                return;
            }

            // Validierung: Concurrency Check (Actor-seitig, schnell)
            if (_state!.Version != cmdEnvelope.ExpectedVersion)
            {
                var errorMsg = $"Concurrency conflict: expected version {cmdEnvelope.ExpectedVersion}, actual {_state.Version}";
                Console.WriteLine($"[Actor] {errorMsg}");
                await TryPublishCommandFailedAsync(cmdEnvelope, errorMsg);
                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = errorMsg,
                    AggregateId = _id,
                    NewVersion = _state.Version  // Pipeline braucht die aktuelle Version für Retry
                });
                return;
            }

            // 1. Events erzeugen (Decider) — wirft keine Exceptions mehr für Domänen-Ablehnungen
            var allEvents = _handler!.HandleCommand(cmdEnvelope.Payload).ToList();

            // 2. Noop — keine Events = idempotent (z.B. DeaktiviereLagerartikel bei bereits inaktivem Artikel)
            if (!allEvents.Any())
            {
                context.Respond(new CommandResult 
                { 
                    Success = true, 
                    AggregateId = _id,
                    Events = allEvents,
                    NewVersion = _state.Version
                });
                return;
            }

            // 3. ★ Trennung: persistente Events vs. Ablehnungen (ITransientEvent)
            var persistentEvents = allEvents.Where(e => e is not ITransientEvent).ToList();
            var rejections = allEvents.Where(e => e is ITransientEvent).ToList();

            // 4. ★ Reine Ablehnung — nur transiente Events, keine persistenten
            //    Kein Append, kein Apply, keine Version-Änderung.
            //    Targeted Delivery an den aufrufenden Client.
            if (rejections.Any() && !persistentEvents.Any())
            {
                var rejection = rejections.First();
                Console.WriteLine($"[Actor] Rejected: {rejection.GetType().Name}");

                // Targeted Delivery — nur an den Aufrufer
                await TryPublishRejectionAsync(cmdEnvelope, rejection);

                context.Respond(new CommandResult 
                { 
                    Success = false, 
                    AggregateId = _id,
                    RejectionEvent = rejection,
                    ErrorMessage = rejection.ToString()
                });
                return;
            }

            // 5. Erfolg — persistente Events verarbeiten
            await _eventStore.AppendEventsAsync(_id, cmdEnvelope.ExpectedVersion, persistentEvents);

            // 6. Events applyen (Applier) — mutiert _state
            foreach (var evt in persistentEvents)
            {
                _handler.ApplyEvent(evt);
                _state.Version++;
            }

            // 7. ★ Version-Tracking in Redis (nicht-kritisch)
            await TryTrackVersionAsync();

            // 8. Events broadcast-publishen (wenn Publisher vorhanden)
            if (_publisher != null)
            {
                await PublishEventsAsync(persistentEvents, cmdEnvelope);
            }

            Console.WriteLine($"[Actor] ✔ Processed. New version: {_state.Version}");

            // 9. WICHTIG: Erfolg antworten mit NewVersion!
            context.Respond(new CommandResult 
            { 
                Success = true, 
                AggregateId = _id,
                Events = persistentEvents,
                NewVersion = _state.Version
            });
        }
        catch (Exception ex)
        {
            // Catch-Block ist jetzt nur noch für technische Fehler
            // (DB-Timeout, Null-Referenz, etc. — nicht für Domänen-Ablehnungen)
            Console.WriteLine($"[Actor] Technical error: {ex.Message}");
            
            await TryPublishCommandFailedAsync(cmdEnvelope, ex.Message);
            
            context.Respond(new CommandResult 
            { 
                Success = false, 
                ErrorMessage = ex.Message,
                AggregateId = _id
            });
        }
    }

    /// <summary>
    /// ★ Phase 4: Aktualisiert die Version in Redis.
    /// Nicht-kritisch: Redis-Fehler unterbrechen den Command-Flow NICHT.
    /// </summary>
    private async Task TryTrackVersionAsync()
    {
        if (_versionTracker == null)
            return;

        try
        {
            await _versionTracker.TrackAsync(
                _id,
                typeof(TState).Name,
                _state!.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Version tracking failed for {AggregateType}/{AggregateId}. " +
                "Redis will catch up on next successful command.",
                typeof(TState).Name, _id);
        }
    }

    private async Task PublishEventsAsync(List<IEvent> events, CommandEnvelope cmdEnvelope)
    {
        foreach (var evt in events)
        {
            var eventEnvelope = new EventEnvelope
            {
                AggregateId = _id,
                AggregateVersion = _state!.Version,
                AggregateType = typeof(TState).Name,
                CorrelationId = cmdEnvelope.CorrelationId,
                CausationId = cmdEnvelope.CommandId.ToString(),
                UserId = cmdEnvelope.UserId,
                Payload = evt
                // TargetSubscriberId bleibt null = Broadcast
            };
            
            await _publisher!.PublishAsync(eventEnvelope);
            Console.WriteLine($"[Actor] Published {evt.GetType().Name}");
        }
    }

    /// <summary>
    /// ★ NEU: Publiziert ein Ablehnungs-Event per Targeted Delivery an den Aufrufer.
    /// Verwendet OriginSessionId für die direkte Zustellung.
    /// Das Ablehnungs-Event ist das typisierte Domänen-Event (z.B. BestandNichtAusreichend).
    /// </summary>
    private async Task TryPublishRejectionAsync(CommandEnvelope cmdEnvelope, IEvent rejection)
    {
        if (_publisher == null || string.IsNullOrEmpty(cmdEnvelope.OriginSessionId))
        {
            Console.WriteLine($"[Actor] Cannot publish rejection: no publisher or no OriginSessionId");
            return;
        }

        try
        {
            var envelope = new EventEnvelope
            {
                AggregateId = _id,
                AggregateVersion = _state?.Version ?? 0,
                AggregateType = typeof(TState).Name,
                CorrelationId = cmdEnvelope.CorrelationId,
                CausationId = cmdEnvelope.CommandId.ToString(),
                UserId = cmdEnvelope.UserId,
                Payload = rejection,
                TargetSubscriberId = cmdEnvelope.OriginSessionId  // Targeted Delivery!
            };

            await _publisher.PublishAsync(envelope);
            Console.WriteLine($"[Actor] Published rejection {rejection.GetType().Name} to '{cmdEnvelope.OriginSessionId}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Actor] Failed to publish rejection: {ex.Message}");
        }
    }

    /// <summary>
    /// Publiziert ein CommandFailed Event für technische Fehler.
    /// Verwendet Targeted Delivery über die OriginSessionId.
    /// ★ Jetzt nur noch für technische Fehler — Domänen-Ablehnungen nutzen TryPublishRejectionAsync.
    /// </summary>
    private async Task TryPublishCommandFailedAsync(CommandEnvelope cmdEnvelope, string reason)
    {
        if (_publisher == null || string.IsNullOrEmpty(cmdEnvelope.OriginSessionId))
        {
            Console.WriteLine($"[Actor] Cannot publish CommandFailed: no publisher or no OriginSessionId");
            return;
        }

        try
        {
            var failedEvent = new EventEnvelope
            {
                AggregateId = _id,
                AggregateVersion = _state?.Version ?? 0,
                AggregateType = typeof(TState).Name,
                CorrelationId = cmdEnvelope.CorrelationId,
                CausationId = cmdEnvelope.CommandId.ToString(),
                UserId = cmdEnvelope.UserId,
                Payload = new CommandFailed(
                    cmdEnvelope.Payload.GetType().Name,
                    reason,
                    _id.ToString()),
                TargetSubscriberId = cmdEnvelope.OriginSessionId
            };

            await _publisher.PublishAsync(failedEvent);
            Console.WriteLine($"[Actor] Published CommandFailed to '{cmdEnvelope.OriginSessionId}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Actor] Failed to publish CommandFailed: {ex.Message}");
        }
    }
}