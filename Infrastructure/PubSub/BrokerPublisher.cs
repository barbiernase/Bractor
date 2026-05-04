namespace Infrastructure.PubSub;

using Abstractions;
using Infrastructure.PubSub.Messages;
using Proto.Cluster;

/// <summary>
/// Publiziert IMessageEnvelope an Subscriber.
/// 
/// Zwei Modi:
/// - Broadcast (default): Fan-out an alle Shards
/// - Targeted: Nur an den Shard der den Ziel-Subscriber hält
/// </summary>
public class BrokerPublisher
{
    private readonly Cluster _cluster;

    public BrokerPublisher(Cluster cluster)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
    }

    /// <summary>
    /// Publiziert ein Envelope an Subscriber.
    /// Bei TargetSubscriberId: Targeted Delivery (1 Shard)
    /// Sonst: Broadcast (alle Shards)
    /// </summary>
    public async Task PublishAsync(IMessageEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Payload);

        // Targeted Delivery: nur an den Shard der den Subscriber hält
        if (envelope is EventEnvelope { TargetSubscriberId: not null } targeted)
        {
            await PublishTargetedAsync(targeted, ct);
            return;
        }

        // Broadcast: an alle Shards (wie bisher)
        var messageType = envelope.Payload.GetType();
        var shards = BrokerIdentity.AllShards(messageType);
        var message = new Publish(envelope);

        var tasks = new Task<Ack?>[shards.Length];
        for (int i = 0; i < shards.Length; i++)
        {
            tasks[i] = _cluster.RequestAsync<Ack>(shards[i], message, ct);
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Targeted Publish: Sendet nur an den Shard der den Ziel-Subscriber hält.
    /// Shard wird über Hash der SubscriberId berechnet (gleicher Algo wie beim Subscribe).
    /// </summary>
    private async Task PublishTargetedAsync(EventEnvelope envelope, CancellationToken ct)
    {
        var messageType = envelope.Payload.GetType();
        var targetId = envelope.TargetSubscriberId!;

        // Shard berechnen: gleicher Hash-Algorithmus wie beim Subscribe!
        var shard = BrokerIdentity.ShardFor(messageType, targetId);
        var message = new Publish(envelope);

        Console.WriteLine($"[BrokerPublisher] Targeted publish to shard {shard.Identity} for subscriber '{targetId}'");

        await _cluster.RequestAsync<Ack>(shard, message, ct);
    }

    /// <summary>
    /// Fire-and-Forget Publish (keine Garantie, wartet nicht auf Antwort)
    /// </summary>
    public void PublishFireAndForget(IMessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Payload);

        // Targeted Delivery
        if (envelope is EventEnvelope { TargetSubscriberId: not null } targeted)
        {
            var messageType = envelope.Payload.GetType();
            var shard = BrokerIdentity.ShardFor(messageType, targeted.TargetSubscriberId);
            
            Console.WriteLine($"[BrokerPublisher] Targeted fire-and-forget to shard {shard.Identity}");
            
            _ = _cluster.RequestAsync<Ack>(shard, new Publish(envelope), CancellationToken.None);
            return;
        }

        // Broadcast: an alle Shards (wie bisher)
        var msgType = envelope.Payload.GetType();
        var shards = BrokerIdentity.AllShards(msgType);
        var message = new Publish(envelope);

        foreach (var shard in shards)
        {
            _ = _cluster.RequestAsync<Ack>(shard, message, CancellationToken.None);
        }
    }
}