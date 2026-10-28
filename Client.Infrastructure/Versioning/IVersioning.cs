namespace Client.Infrastructure.Versioning;

/// <summary>
/// Die Version je Aggregat, wie der Client sie kennt (eine Version, Plan Pub/Sub-Neustrukturierung Phase 5): ConnectionModule nutzt
/// <see cref="GetVersion"/> als ExpectedVersion eines Commands, die QueryBridge als Ziel für Lesen nach Schreiben.
/// </summary>
public interface IVersioningModule
{
    /// <summary>
    /// Die höchste bekannte Version eines Aggregats (aus Event-Pushes und Antworten). null, wenn das Aggregat dem Client nicht bekannt ist.
    /// </summary>
    int? GetVersion(Guid aggregateId);

    /// <summary>
    /// Übernimmt die Versionen der Aggregate, die eine Antwort zeigt (Query-Response-Dependencies) — auch sie sind die ExpectedVersion des
    /// nächsten Commands. Wird von der QueryBridge aufgerufen.
    /// </summary>
    void TrackFromDeps(IEnumerable<AggregateDep> deps);

    /// <summary>
    /// Merkt sich ein Aggregat, das der Client GERADE beschrieben hat (beim Command-Senden aufgerufen). Read-your-writes: es geht mit seiner
    /// Version (aus der Quittung, also dem Push danach) als „mindestens S auf v“ mit jeder Query mit — die Leseseite wartet, bis ihre Projektion
    /// so weit ist. Begrenzt auf die zuletzt beschriebenen (die ältesten fallen heraus).
    /// </summary>
    void MarkWritten(Guid aggregateId);

    /// <summary>„Mindestens S auf v“ für die zuletzt beschriebenen Aggregate, deren Version der Client kennt.</summary>
    IReadOnlyList<global::Abstractions.StreamStand> Mindestens { get; }
}

/// <summary>
/// Aggregate-Abhängigkeit aus einer Query-Response.
/// Korrespondiert zu Abstractions.AggregateMeta auf dem Server.
/// </summary>
public record AggregateDep(Guid Id, string AggregateType, int Version);