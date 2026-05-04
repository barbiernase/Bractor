using System.Collections.Generic;
using Abstractions;

namespace Core
{
    public abstract class AggregateHandlerBase<TState, TDecider, TApplier> : IAggregateHandler
        where TState : IState
        where TDecider : IDecider<TState>
        where TApplier : IApplier<TState>
    {
        protected readonly TDecider _decider;
        protected readonly TApplier _applier;

        protected AggregateHandlerBase(TState state, TDecider decider, TApplier applier)
        {
            _decider = decider;
            _applier = applier;
        }

        public abstract IEnumerable<IEvent> HandleCommand(ICommand command);
        public abstract void ApplyEvent(IEvent @event);
    }
}