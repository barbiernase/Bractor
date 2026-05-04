using Proto.Cluster;

namespace Infrastructure.PubSub.Actors;

using Abstractions;
using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.Logging;
using Proto;

/// <summary>
/// Verwaltet Subscriptions und leitet Envelopes an Subscriber weiter.
/// Identity: "{MessageTypeName}_{ShardIndex}"
/// 
/// Unterstützt zwei Delivery-Modi:
/// - Broadcast (default): Event geht an alle Subscriber
/// - Targeted: Event geht nur an einen spezifischen Subscriber (via TargetSubscriberId)
/// </summary>
public class BrokerShardActor : IActor
{
    private readonly ILogger<BrokerShardActor>? _logger;
    private readonly SubscriberRegistry _registry = new();
    
    private string _typeName = string.Empty;
    private int _shardIndex;

    public BrokerShardActor(ILogger<BrokerShardActor>? logger = null)
    {
        _logger = logger;
    }

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                OnStarted(context);
                break;
                
            case Subscribe msg:
                OnSubscribe(context, msg);
                break;
                
            case Unsubscribe msg:
                OnUnsubscribe(context, msg);
                break;
                
            case Publish msg:
                OnPublish(context, msg);
                break;
                
            case GetSubscriberCount:
                context.Respond(new SubscriberCountResponse(_registry.Count));
                break;
        }
        
        return Task.CompletedTask;
    }

    private void OnStarted(IContext context)
    {
        var identity = context.ClusterIdentity()?.Identity ?? "unknown_0";
        (_typeName, _shardIndex) = BrokerIdentity.ParseShardIdentity(identity);
        
        _logger?.LogInformation(
            "[Shard {Type}_{Index}] Started", 
            _typeName, _shardIndex);
    }

    private void OnSubscribe(IContext context, Subscribe msg)
    {
        _registry.Add(msg.SubscriberId, msg.Subscriber);
        
        _logger?.LogDebug(
            "[Shard {Type}_{Index}] +Subscriber '{Id}' (Total: {Count})",
            _typeName, _shardIndex, msg.SubscriberId, _registry.Count);
        
        context.Respond(new Ack());
    }

    private void OnUnsubscribe(IContext context, Unsubscribe msg)
    {
        if (_registry.Remove(msg.SubscriberId))
        {
            _logger?.LogDebug(
                "[Shard {Type}_{Index}] -Subscriber '{Id}' (Total: {Count})",
                _typeName, _shardIndex, msg.SubscriberId, _registry.Count);
        }
        
        context.Respond(new Ack());
    }

    private void OnPublish(IContext context, Publish msg)
    {
        if (_registry.Count == 0)
        {
            context.Respond(new Ack());
            return;
        }

        // Targeted Delivery: Event nur an einen spezifischen Subscriber senden
        if (msg.Envelope is EventEnvelope { TargetSubscriberId: not null } targeted)
        {
            if (_registry.TryGet(targeted.TargetSubscriberId, out var pid))
            {
                context.Send(pid, msg.Envelope);
                _logger?.LogDebug(
                    "[Shard {Type}_{Index}] Targeted delivery to '{Id}'",
                    _typeName, _shardIndex, targeted.TargetSubscriberId);
            }
            else
            {
                // Subscriber nicht auf diesem Shard - das ist OK, 
                // der Publisher sendet nur an den korrekten Shard
                _logger?.LogDebug(
                    "[Shard {Type}_{Index}] Targeted subscriber '{Id}' not on this shard",
                    _typeName, _shardIndex, targeted.TargetSubscriberId);
            }
        }
        else
        {
            // Broadcast: an alle Subscriber wie bisher
            _logger?.LogDebug(
                "[Shard {Type}_{Index}] Broadcasting to {Count} subscribers",
                _typeName, _shardIndex, _registry.Count);

            foreach (var subscriber in _registry.Subscribers)
            {
                context.Send(subscriber, msg.Envelope);
            }
        }

        context.Respond(new Ack());
    }
}