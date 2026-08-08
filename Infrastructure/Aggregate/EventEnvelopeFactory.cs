using Abstractions;

namespace Infrastructure.Aggregate;

/// <summary>
/// Baut die EventEnvelopes für den Publish-Pfad — mit Version PRO Event (Spec 4.2).
///
/// WARUM pur/statisch: Die Versions-Arithmetik ist der Anker für Reihenfolge, Dedup
/// und (später) deterministische Prozess-Identitäten. Als reine Funktion ist sie
/// ohne Cluster/Proto testbar — genau das braucht das Phase-1-Tor.
///
/// Ein Command kann MEHRERE Events erzeugen. Der Stream stand vor dem Batch auf
/// <paramref name="baseVersion"/> (= ExpectedVersion); Event i (0-basiert) liegt damit
/// auf baseVersion + i + 1 (1-basiert, konsistent mit dem Store-Append und
/// IEventStoreRepository.ReadStreamAsync). NIE der Endwert des Batches für alle Events.
/// </summary>
public static class EventEnvelopeFactory
{
    public static List<EventEnvelope> BuildPerEvent(
        IReadOnlyList<IEvent> events,
        Guid aggregateId,
        string aggregateType,
        int baseVersion,
        string correlationId,
        string causationId,
        string userId)
    {
        var result = new List<EventEnvelope>(events.Count);
        for (int i = 0; i < events.Count; i++)
        {
            result.Add(new EventEnvelope
            {
                AggregateId      = aggregateId,
                AggregateVersion = baseVersion + i + 1,   // Version PRO Event
                AggregateType    = aggregateType,
                CorrelationId    = correlationId,
                CausationId      = causationId,
                UserId           = userId,
                Payload          = events[i]
                // TargetSubscriberId bleibt null = Broadcast
            });
        }
        return result;
    }
}
