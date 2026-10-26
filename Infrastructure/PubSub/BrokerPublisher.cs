namespace Infrastructure.PubSub;

using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

/// <summary>
/// Veröffentlicht IMessageEnvelope an Subscriber — immer <b>verlierbar</b> (<see cref="Bote.Melde"/>): Signale, Event-Pushes, Ablehnungen und
/// <c>CommandFailed</c> an eine Sitzung, Hinweise. Ein Signal ist nur ein Weckruf (Invariante 2), alles andere schneller Kanal (Invariante 6);
/// die durablen Konsumenten heilen über Log-Read und Poll. Der Absender wartet nie auf die Shards.
///
/// Zwei Modi:
/// - Broadcast (default): an alle Shards des Typs
/// - Targeted: nur an den Shard, der den Ziel-Subscriber hält
/// </summary>
public class BrokerPublisher
{
    private readonly Bote _bote;

    public BrokerPublisher(Cluster cluster, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        _bote = new Bote(cluster, logger);
    }

    /// <summary>
    /// Meldet ein Envelope an seine Subscriber. Bei TargetSubscriberId: nur an den Shard des Ziels (gleicher Hash wie beim Subscribe),
    /// sonst an alle Shards des Typs. Kehrt sofort zurück.
    /// </summary>
    public void Melde(IMessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Payload);

        var messageType = envelope.Payload.GetType();
        var message = new Publish(envelope);
        if (envelope is EventEnvelope { TargetSubscriberId: not null } targeted)
        {
            _bote.Melde(BrokerIdentity.ShardFor(messageType, targeted.TargetSubscriberId), message);
            return;
        }
        foreach (var shard in BrokerIdentity.AllShards(messageType))
            _bote.Melde(shard, message);
    }
}
