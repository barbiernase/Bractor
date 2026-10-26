using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.PubSub;

/// <summary>
/// Was nach einem Commit gemeldet wird — für JEDEN Schreiber gleich (Aggregat, Quelle, festgehaltene Ausgabe einer Funktion): je Event der
/// Envelope (Push an die Sitzungen, die ihn hören) und sein typisiertes Signal (Weckruf der Projektionen und Funktionen, Invariante 2). Verlierbar,
/// ohne Warten — die durablen Konsumenten heilen über Log-Read und Takt. Bis zum Wecker am Flush (Plan Pub/Sub-Neustrukturierung Phase 6) ist das
/// die eine Stelle, über die ein Commit weckt; vorher sahen Events aus Quellen und Funktionen ihre Hörer erst mit dem Takt (docs/13 FB-3).
/// </summary>
public static class Commitmeldung
{
    /// <summary>Meldet die Envelopes eines Commits (Version je Event, <see cref="Infrastructure.Aggregate.EventEnvelopeFactory"/>).</summary>
    public static void Melde(BrokerPublisher publisher, IReadOnlyList<EventEnvelope> envelopes)
    {
        foreach (var e in envelopes)
        {
            publisher.Melde(e);
            if (Anwendung.Signale.Create(e.Payload, e.AggregateId, e.AggregateVersion) is { } signal)
                publisher.Melde(new SignalEnvelope { Signal = signal, CorrelationId = e.CorrelationId, UserId = e.UserId });
        }
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
