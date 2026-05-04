using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions; // Ihre Abstractions-Namespace

public class InMemoryEventStore : IEventStoreRepository
{
    private readonly ConcurrentDictionary<Guid, List<IEvent>> _eventStreams = new();
    private readonly IAggregateHandlerFactory _handlerFactory;

    // Wir brauchen die Factory, um den Zustand aus den Events wiederherzustellen.
    public InMemoryEventStore(IAggregateHandlerFactory handlerFactory)
    {
        _handlerFactory = handlerFactory;
    }

    public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events)
    {
        // Hole den bestehenden Stream oder erstelle einen neuen.
        var stream = _eventStreams.GetOrAdd(aggregateId, _ => new List<IEvent>());

        // Optimistic Concurrency Check
        // Die aktuelle Version ist die Anzahl der bereits vorhandenen Events.
        if (stream.Count != expectedVersion)
        {
            throw new ConcurrencyException($"Concurrency conflict for aggregate {aggregateId}. Expected version {expectedVersion}, but current version is {stream.Count}.");
        }

        // Füge die neuen Events hinzu.
        stream.AddRange(events);

        return Task.CompletedTask;
    }

    public Task<TState?> LoadStateAsync<TState>(Guid aggregateId) where TState : class, IState, new()
    {
        _eventStreams.TryGetValue(aggregateId, out var stream);

        if (stream == null || !stream.Any())
        {
            return Task.FromResult<TState?>(default);
        }

        // Replay: Erstelle einen leeren Zustand und wende alle Events an.
        var state = new TState();
        var handler = _handlerFactory.CreateHandler(state);

        foreach (var @event in stream)
        {
            handler.ApplyEvent(@event);
            state.Version++;
        }

        state.Id = aggregateId;
        return Task.FromResult<TState?>(state);
    }
}