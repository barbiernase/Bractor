using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure
{
    public class InMemoryAggregateMessenger : IAggregateMessenger
    {
        private readonly InMemoryAggregateRepository _repository;
        private readonly IAggregateHandlerFactory _handlerFactory;
        
        public InMemoryAggregateMessenger(InMemoryAggregateRepository repository, IAggregateHandlerFactory handlerFactory)
        {
            _repository = repository;
            _handlerFactory = handlerFactory;
        }

        public async Task<CommandResult> CommitAsync<TAggregate>(
            Guid aggregateId, 
            int expectedVersion, 
            IReadOnlyList<IEvent> events) 
            where TAggregate : IState, new()
        {
            try
            {
                var currentState = await _repository.Load<TAggregate>(aggregateId) ?? new TAggregate();

                if (currentState.Version != expectedVersion)
                {
                    throw new ConcurrencyException($"Concurrency conflict: Aggregate has version {currentState.Version}, but expected {expectedVersion}.");
                }
                
                var handler = _handlerFactory.CreateHandler(currentState);
                
                foreach (var @event in events)
                {
                    handler.ApplyEvent(@event);
                    currentState.Version++;
                }
                
                _repository.Save(aggregateId, currentState);

                return new CommandResult { Success = true, AggregateId = aggregateId, Events = events };
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, AggregateId = aggregateId, ErrorMessage = ex.Message };
            }
        }
    }
}