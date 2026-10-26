namespace Infrastructure.PubSub.Messages;

using Abstractions;
using Proto;

// Data Plane
public record Subscribe(string SubscriberId, PID Subscriber) : IWireMessage;
public record Unsubscribe(string SubscriberId) : IWireMessage;
public record Publish(IMessageEnvelope Envelope) : IWireMessage;  // Geändert: Envelope statt Payload

// Responses
public record Ack : IWireMessage;

// Control Plane
public record Activate : IWireMessage;

// Internal
public record GetSubscriberCount : IWireMessage;
public record SubscriberCountResponse(int Count) : IWireMessage;