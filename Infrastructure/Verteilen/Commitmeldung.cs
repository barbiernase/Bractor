using Abstractions;

namespace Infrastructure.Verteilen;

/// <summary>
/// Was nach einem Commit an die Clients geht — für JEDEN Schreiber gleich (Aggregat, Quelle, festgehaltene Ausgabe einer Funktion): je Event der
/// Envelope, als eine Nachricht je Knoten an die Verteiler (<see cref="Verteilung"/>). Verlierbar, ohne Warten (Invariante 6). Die durablen
/// Konsumenten weckt nicht sie, sondern der Wecker am Commit (<see cref="Infrastructure.Wecken.Wecker"/>).
/// </summary>
public static class Commitmeldung
{
    /// <summary>Meldet die Envelopes eines Commits (Version je Event, <see cref="Infrastructure.Aggregate.EventEnvelopeFactory"/>).</summary>
    public static void Melde(Verteilung verteilung, IReadOnlyList<EventEnvelope> envelopes) => verteilung.Melde(envelopes);

    /// <summary>Meldet frisch angehängte Events eines Streams (er stand vor dem Anhängen auf <paramref name="vorher"/>).</summary>
    public static void Melde(Verteilung? verteilung, IReadOnlyList<IEvent> events, Guid stream, string aggregateType, int vorher,
        string? korrelation, string? ursache, string? akteur)
    {
        if (verteilung is null || events.Count == 0) return;
        Melde(verteilung, Infrastructure.Aggregate.EventEnvelopeFactory.BuildPerEvent(events, stream, aggregateType, vorher,
            korrelation ?? stream.ToString(), ursache ?? "", ImAuftrag.IstAkteur(akteur) ? akteur! : ImAuftrag.Ohne,
            streamHeadVersion: vorher + events.Count, spur: Spur.Aktuell));
    }
}
