using Abstractions;

namespace Infrastructure.PubSub;

/// <summary>
/// Was nach einem Commit an die Clients geht — für JEDEN Schreiber gleich (Aggregat, Quelle, festgehaltene Ausgabe einer Funktion): je Event der
/// Envelope (Push an die Sitzungen, die ihn hören, über den Broker). Verlierbar, ohne Warten (Invariante 6). Die durablen Konsumenten weckt nicht
/// sie, sondern der Wecker am Commit (<see cref="Infrastructure.Wecken.Wecker"/>, Plan Pub/Sub-Neustrukturierung Phase 6); den Broker ersetzt in
/// Phase 7 der Verteiler je Knoten.
/// </summary>
public static class Commitmeldung
{
    /// <summary>Meldet die Envelopes eines Commits (Version je Event, <see cref="Infrastructure.Aggregate.EventEnvelopeFactory"/>).</summary>
    public static void Melde(BrokerPublisher publisher, IReadOnlyList<EventEnvelope> envelopes)
    {
        foreach (var e in envelopes) publisher.Melde(e);
    }

    /// <summary>Meldet frisch angehängte Events eines Streams (er stand vor dem Anhängen auf <paramref name="vorher"/>).</summary>
    public static void Melde(BrokerPublisher? publisher, IReadOnlyList<IEvent> events, Guid stream, string aggregateType, int vorher,
        string? korrelation, string? ursache, string? akteur)
    {
        if (publisher is null || events.Count == 0) return;
        Melde(publisher, Infrastructure.Aggregate.EventEnvelopeFactory.BuildPerEvent(events, stream, aggregateType, vorher,
            korrelation ?? stream.ToString(), ursache ?? "", ImAuftrag.IstAkteur(akteur) ? akteur! : ImAuftrag.Ohne,
            streamHeadVersion: vorher + events.Count, spur: Spur.Aktuell));
    }
}
