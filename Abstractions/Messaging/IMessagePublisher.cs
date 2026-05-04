namespace Abstractions.Messaging;

/// <summary>
/// Publiziert Messages (Events und Commands) über das PubSub-System
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publiziert eine Message an alle Subscriber des entsprechenden Typs.
    /// Wartet bis alle Shards die Message bestätigt haben.
    /// </summary>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default) 
        where TMessage : IMessagePayload;
    
    /// <summary>
    /// Publiziert eine Message an alle Subscriber des entsprechenden Typs.
    /// Wartet bis alle Shards die Message bestätigt haben.
    /// </summary>
    Task PublishAsync(IMessagePayload message, CancellationToken ct = default);
    
    /// <summary>
    /// Publiziert eine Message ohne auf Bestätigung zu warten (Fire-and-Forget).
    /// Keine Garantie dass die Message ankommt.
    /// </summary>
    void PublishFireAndForget<TMessage>(TMessage message) 
        where TMessage : IMessagePayload;
    
    /// <summary>
    /// Publiziert eine Message ohne auf Bestätigung zu warten (Fire-and-Forget).
    /// Keine Garantie dass die Message ankommt.
    /// </summary>
    void PublishFireAndForget(IMessagePayload message);
}