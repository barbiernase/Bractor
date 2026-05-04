namespace Infrastructure.PubSub.Messages;

using Abstractions;
using Proto;

// Data Plane
public record Subscribe(string SubscriberId, PID Subscriber);
public record Unsubscribe(string SubscriberId);
public record Publish(IMessageEnvelope Envelope);  // Geändert: Envelope statt Payload

// Responses
public record Ack;

// Control Plane
public record Activate;
public record GetStatus;
public record BrokerStatus(Type MessageType, int ShardCount, IReadOnlyList<ShardInfo> Shards);
public record ShardInfo(int Index, bool IsAlive, int SubscriberCount);

// Internal
public record GetSubscriberCount;
public record SubscriberCountResponse(int Count);