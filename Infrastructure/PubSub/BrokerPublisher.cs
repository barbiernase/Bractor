namespace Infrastructure.PubSub;

using Abstractions;
using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger? _logger;

    /// <summary>Bounded-Frist für fire-and-forget-Publishes (kein Infinit-Hang bei nicht-routbarer Topologie).</summary>
    private static readonly TimeSpan FireForgetFrist = TimeSpan.FromSeconds(10);

    public BrokerPublisher(Cluster cluster, ILogger? logger = null)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _logger = logger;
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

        await _cluster.RequestAsync<Ack>(shard, message, ct);
    }

    /// <summary>
    /// Fire-and-Forget Publish (keine Garantie, wartet nicht auf Antwort).
    ///
    /// ★ Härtung (Multi-Node): der Send läuft mit BESCHRÄNKTEM Token und wird BEOBACHTBAR gemacht.
    /// Vorher war es ein nacktes <c>_ = RequestAsync(…)</c> — eine cross-node-Ausnahme (Node weg, Timeout,
    /// Serialisierung) landete als unbeobachtete Task-Exception, still, ohne Log. Jetzt: gefangen + geloggt.
    /// BEWUSST KEIN Dead-Letter: ein Signal/Event-Publish ist per Design verlierbar (Invariante 2) — der
    /// durable Konsument heilt über den Poll-Backstop; ein Dead-Letter würde fälschlich Durabilität
    /// implizieren. Es geht hier nur um Beobachtbarkeit, nicht um Wiederzustellung.
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
            SendeBeobachtet(shard, new Publish(envelope));
            return;
        }

        // Broadcast: an alle Shards (wie bisher)
        var msgType = envelope.Payload.GetType();
        var shards = BrokerIdentity.AllShards(msgType);
        var message = new Publish(envelope);

        foreach (var shard in shards)
        {
            SendeBeobachtet(shard, message);
        }
    }

    /// <summary>Fire-and-forget aus Aufrufersicht, aber der Send-Fehler wird gefangen und geloggt (nicht still).</summary>
    private void SendeBeobachtet(ClusterIdentity shard, Publish message)
    {
        _ = SendeBeobachtetAsync(shard, message);
    }

    private async Task SendeBeobachtetAsync(ClusterIdentity shard, Publish message)
    {
        try
        {
            using var cts = new CancellationTokenSource(FireForgetFrist);
            await _cluster.RequestAsync<Ack>(shard, message, cts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "[BrokerPublisher] Fire-and-forget-Publish an {Shard} fehlgeschlagen — Signal verlierbar, " +
                "Poll-Backstop des Konsumenten heilt (Invariante 2).", shard.Identity);
        }
    }
}