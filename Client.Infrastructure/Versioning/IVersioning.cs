namespace Client.Infrastructure.Versioning;

/// <summary>
/// Tracked Aggregate-Versionen für Optimistic Concurrency.
///
/// ConnectionModule nutzt GetVersion() um ExpectedVersion für Commands zu setzen.
/// VersioningModule (Phase 8) implementiert dies und subscribt auf Server-Events.
/// </summary>
public interface IVersioningModule
{
    /// <summary>
    /// Gibt die letzte bekannte Version eines Aggregats zurück.
    /// null wenn das Aggregat dem Client nicht bekannt ist.
    /// </summary>
    int? GetVersion(Guid aggregateId);

    /// <summary>
    /// Das Read-Your-Writes-Ziel: die höchste DOMAIN-Event-Version, die der Client für dieses
    /// Aggregat aus dem Event-Push gesehen hat. Anders als <see cref="GetVersion"/> (Stream-Head
    /// inkl. Marke, für OCC) zählt hier NUR die materialisierbare Domain-Version — denn genau bis
    /// dorthin muss die (asynchrone Pull-)Projektion aufgeschlossen haben, damit ein Read frisch ist.
    /// null, wenn der Client kein Event für das Aggregat gesehen hat (nichts zu erwarten).
    /// </summary>
    int? GetReadTarget(Guid aggregateId);

    /// <summary>
    /// Trackt Aggregate-Versionen aus Query-Response-Dependencies.
    /// Wird von der QueryBridge aufgerufen.
    /// </summary>
    void TrackFromDeps(IEnumerable<AggregateDep> deps);

    /// <summary>
    /// Merkt sich ein Aggregat, das der Client GERADE beschrieben hat (beim Command-Senden aufgerufen).
    /// Read-Your-Writes: diese IDs gehen als <c>expected_fresh_ids</c> mit jeder Query mit, damit die
    /// Leseseite sie als Deps trackt und der Client nachfasst, bis die Projektion sie eingeholt hat.
    /// Recency-begrenzt (die ältesten fallen heraus) — hält das Set klein.
    /// </summary>
    void MarkWritten(Guid aggregateId);

    /// <summary>Die zuletzt beschriebenen Aggregat-IDs (readModel-ID-Format) für die RYW-Deps.</summary>
    IReadOnlyList<string> RecentWrites { get; }
}

/// <summary>
/// Aggregate-Abhängigkeit aus einer Query-Response.
/// Korrespondiert zu Abstractions.AggregateMeta auf dem Server.
/// </summary>
public record AggregateDep(Guid Id, string AggregateType, int Version);