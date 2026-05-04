using Abstractions;
using JasperFx.Events;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Logging;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Persistence;

/// <summary>
/// EventStore-Implementierung auf Basis von MartenDB (PostgreSQL).
/// 
/// Ersetzt den InMemoryEventStore. Keine Marten-Projektionen –
/// Projektionen laufen über das eigene PubSub-System.
/// 
/// Concurrency-Sicherheit:
/// - StartStream wirft wenn Stream bereits existiert (Schutz gegen doppelte Erstellung)
/// - Append mit Zielversion wirft bei Konflikt (Optimistic Concurrency)
/// - Beide werden als ConcurrencyException nach oben gegeben
/// </summary>
public class MartenEventStore : IEventStoreRepository
{
    private readonly IDocumentStore _store;
    private readonly IAggregateHandlerFactory _handlerFactory;
    private readonly ILogger<MartenEventStore> _logger;

    public MartenEventStore(
        IDocumentStore store,
        IAggregateHandlerFactory handlerFactory,
        ILogger<MartenEventStore> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Persistiert Events in PostgreSQL mit Optimistic Concurrency.
    /// 
    /// expectedVersion == 0: Neuer Stream (StartStream – wirft wenn bereits vorhanden)
    /// expectedVersion > 0:  Append mit Zielversion (expectedVersion + events.Count)
    /// expectedVersion < 0:  Ungültig (Guard gegen CommandEnvelope-Default von -1)
    /// </summary>
    public async Task AppendEventsAsync(
        Guid aggregateId,
        int expectedVersion,
        IReadOnlyList<IEvent> events)
    {
        // Guard: expectedVersion muss >= 0 sein.
        // Der Actor prüft bereits gegen seinen State, aber falls
        // AppendEventsAsync ohne Actor-Kontext aufgerufen wird,
        // fangen wir ungültige Werte defensiv ab.
        if (expectedVersion < 0)
            throw new ArgumentException(
                $"ExpectedVersion must be >= 0, was {expectedVersion}.",
                nameof(expectedVersion));

        if (events.Count == 0)
            return;

        await using var session = _store.LightweightSession();

        if (expectedVersion == 0)
        {
            // Neuer Stream: StartStream garantiert, dass der Stream
            // noch nicht existiert. Schärfer als Append mit Version 0.
            session.Events.StartStream(aggregateId, events.ToArray());
        }
        else
        {
            // Bestehender Stream: Append mit Versionsprüfung.
            // Marten erwartet die Ziel-Version NACH dem Append.
            // Bei Version 5 mit 2 Events → Zielversion 7.
            session.Events.Append(
                aggregateId,
                expectedVersion + events.Count,
                events.ToArray());
        }

        try
        {
            await session.SaveChangesAsync();

            _logger.LogDebug(
                "Appended {EventCount} events to stream {AggregateId}, " +
                "expected version {ExpectedVersion}",
                events.Count, aggregateId, expectedVersion);
        }
        catch (EventStreamUnexpectedMaxEventIdException ex)
        {
            _logger.LogWarning(
                "Concurrency conflict for aggregate {AggregateId}. " +
                "Expected version {ExpectedVersion}, but stream has diverged.",
                aggregateId, expectedVersion);

            throw new ConcurrencyException(
                $"Concurrency conflict for aggregate {aggregateId}. " +
                $"Expected version {expectedVersion}, but stream has diverged.",
                ex);
        }
        catch (ExistingStreamIdCollisionException ex)
        {
            // StartStream wirft dies wenn der Stream bereits existiert
            _logger.LogWarning(
                "Stream {AggregateId} already exists. Duplicate creation attempt.",
                aggregateId);

            throw new ConcurrencyException(
                $"Stream {aggregateId} already exists. " +
                $"Cannot create aggregate that already has events.",
                ex);
        }
    }

    /// <summary>
    /// Rekonstruiert den State eines Aggregats durch Event-Replay aus PostgreSQL.
    /// 
    /// Identisch zum bisherigen Replay-Muster: Events laden, Handler erstellen,
    /// jedes Event applizieren, Version hochzählen.
    /// 
    /// Unterschied zu InMemoryEventStore: Id wird VOR dem Replay gesetzt,
    /// nicht danach. Dokumentierte, bewusste Entscheidung.
    /// </summary>
    public async Task<TState?> LoadStateAsync<TState>(Guid aggregateId)
        where TState : class, IState, new()
    {
        await using var session = _store.LightweightSession();

        var eventStream = await session.Events.FetchStreamAsync(aggregateId);

        if (eventStream == null || eventStream.Count == 0)
            return null;

        // Id VOR Replay setzen – falls ein Applier die Id während des Replays braucht
        var state = new TState { Id = aggregateId };
        var handler = _handlerFactory.CreateHandler(state);

        foreach (var @event in eventStream)
        {
            // Marten wrappet unsere Events in seine eigene Event-Hülle.
            // Das .Data-Property enthält das deserialisierte Domain-Event.
            if (@event.Data is IEvent domainEvent)
            {
                handler.ApplyEvent(domainEvent);
                state.Version++;
            }
            else
            {
                _logger.LogWarning(
                    "Event at version {Version} in stream {AggregateId} " +
                    "could not be cast to IEvent. Type: {EventType}",
                    @event.Version, aggregateId, @event.Data?.GetType().FullName ?? "null");
            }
        }

        _logger.LogDebug(
            "Loaded state for {AggregateId}: Version={Version}",
            aggregateId, state.Version);

        return state;
    }
}