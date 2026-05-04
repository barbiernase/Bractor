using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;

namespace Core
{
    public class Session<TAggregate> where TAggregate : IState, new()
    {
        private readonly TAggregate _aggregateState;
        private readonly IAggregateHandler _handler;
        private readonly IAggregateMessenger _messenger;
        private List<IEvent> _uncommittedEvents = new();
        private readonly bool _isNew;

        private Session(TAggregate initialState, IAggregateHandler handler, IAggregateMessenger messenger, bool isNew)
        {
            _aggregateState = initialState;
            _handler = handler;
            _messenger = messenger;
            _isNew = isNew;
        }

        public static async Task<Session<TAggregate>> LoadAsync(
            Guid aggregateId,
            IAggregateMessenger messenger,
            IAggregateRepository repository,
            IAggregateHandlerFactory handlerFactory)
        {
            var state = await repository.Load<TAggregate>(aggregateId) ?? new TAggregate();
            var isNew = state.Version == 0;
            var handler = handlerFactory.CreateHandler(state);
            return new Session<TAggregate>(state, handler, messenger, isNew);
        }

        public void Dispatch<TCommand>(CommandEnvelope envelope) where TCommand : ICommand
        {
            if (_aggregateState.Version != envelope.ExpectedVersion)
            {
                throw new ConcurrencyException(
                    $"Befehl '{envelope.Payload.GetType().Name}' konnte nicht verarbeitet werden. " +
                    $"Erwartete Version war {envelope.ExpectedVersion}, aber die aktuelle Version ist {_aggregateState.Version}."
                );
            }

            if (_isNew && envelope.Payload is not ICreationCommand)
            {
                throw new InvalidOperationException("Ein neues Aggregat kann nur mit einem CreationCommand erstellt werden.");
            }

            var newEvents = _handler.HandleCommand(envelope.Payload).ToList();

            if (newEvents.Any())
            {
                foreach (var evt in newEvents)
                {
                    _handler.ApplyEvent(evt);
                    _aggregateState.Version++;
                }
                _uncommittedEvents.AddRange(newEvents);
            }
        }

        public async Task<CommandResult> SaveChangesAsync()
        {
            if (!_uncommittedEvents.Any())
            {
                return new CommandResult { Success = true, AggregateId = _aggregateState.Id };
            }
        
            int versionBeforeCommit = _aggregateState.Version - _uncommittedEvents.Count;
            var eventsToCommit = _uncommittedEvents;
            _uncommittedEvents = new List<IEvent>();

            return await _messenger.CommitAsync<TAggregate>(_aggregateState.Id, versionBeforeCommit, eventsToCommit);
        }
    }
}