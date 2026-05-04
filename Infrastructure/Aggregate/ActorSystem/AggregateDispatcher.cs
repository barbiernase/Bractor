using Abstractions;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Dispatcht Commands an Aggregate-Actors.
/// 
/// FIRE-AND-FORGET: Wartet NICHT auf Ergebnis!
/// Feedback kommt ausschließlich über Events (PubSub):
/// - Erfolg: Domain-Events (Broadcast)
/// - Fehler: CommandFailed Event (Targeted)
/// </summary>
public class ProtoActorAggregateDispatcher : IAggregateDispatcher
{
    private readonly Proto.ActorSystem _actorSystem;
    
    public ProtoActorAggregateDispatcher(Proto.ActorSystem actorSystem)
    {
        _actorSystem = actorSystem;
    }
    
    /// <summary>
    /// Dispatcht einen Command an den zuständigen Aggregate-Actor.
    /// Fire-and-Forget: Kehrt sofort zurück, wartet nicht auf Verarbeitung.
    /// </summary>
    public void Dispatch(CommandEnvelope envelope)
    {
        Console.WriteLine($"[Dispatcher] Fire-and-forget: {envelope.Payload.GetType().Name} to {envelope.AggregateType}/{envelope.AggregateId}");
        
        var identity = ClusterIdentity.Create(
            envelope.AggregateId.ToString(),
            envelope.AggregateType
        );
        
        // Fire-and-Forget: Kein await, kein Wait, kein Result!
        // Feedback kommt ausschließlich über Events (PubSub)
        _ = _actorSystem.Cluster().RequestAsync<CommandResult>(
            identity,
            envelope,
            CancellationToken.None
        );
    }
}