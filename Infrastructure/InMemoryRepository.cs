using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure
{
    public class InMemoryAggregateRepository : IAggregateRepository
    {
        private readonly ConcurrentDictionary<Guid, IState> _store = new();
        
        public Task<TState?> Load<TState>(Guid aggregateId) where TState : IState, new()
        {
            _store.TryGetValue(aggregateId, out var state);
            return Task.FromResult((TState?)state?.DeepClone()); 
        }

        public void Save(Guid aggregateId, IState state)
        {
            _store[aggregateId] = state.DeepClone();
        }
    }
    
    public static class StateCloning
    {
        public static IState DeepClone(this IState state)
        {
            if (state == null) return null;
            var options = new System.Text.Json.JsonSerializerOptions();
            
            // Serialize with the object's actual concrete type
            var json = System.Text.Json.JsonSerializer.Serialize(state, state.GetType(), options);
            
            // Deserialize back to the same concrete type
            return (IState)System.Text.Json.JsonSerializer.Deserialize(json, state.GetType(), options)!;
        }

        // Overload for generic use if needed, but the one above fixes the immediate issue.
        public static T DeepClone<T>(this T obj)
        {
            if (obj == null) return default;
            var options = new System.Text.Json.JsonSerializerOptions();
            return System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(obj, options), options)!;
        }
    }
}